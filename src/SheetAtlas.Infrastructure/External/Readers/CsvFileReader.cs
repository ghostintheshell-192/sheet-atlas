using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Logging.Services;
using CsvHelper;
using CsvHelper.Configuration;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Reader for CSV (Comma-Separated Values) files
    /// </summary>
    /// <remarks>
    /// Uses CsvHelper library with auto-detection for delimiter.
    /// CSV files are converted to ExcelFile with a single sheet named "Data".
    /// Supports configuration via CsvReaderOptions for delimiter, encoding, and culture.
    /// </remarks>
    public class CsvFileReader : IConfigurableFileReader
    {
        private readonly FileReaderContext _context;
        private readonly INumberFormatInferenceService _formatInferenceService;
        private CsvReaderOptions _options;

        public CsvFileReader(
            FileReaderContext context,
            INumberFormatInferenceService formatInferenceService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _formatInferenceService = formatInferenceService ?? throw new ArgumentNullException(nameof(formatInferenceService));
            _options = CsvReaderOptions.Default;
        }

        private static readonly string[] _supportedExtensions = new[] { ".csv" };

        public IReadOnlyList<string> SupportedExtensions => _supportedExtensions.AsReadOnly();

        public void Configure(IReaderOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options is not CsvReaderOptions csvOptions)
                throw new ArgumentException($"Expected CsvReaderOptions but got {options.GetType().Name}", nameof(options));

            _options = csvOptions;
            _context.Logger.LogInfo($"Configured CSV reader with delimiter '{_options.Delimiter}', encoding '{_options.Encoding.WebName}'", "CsvFileReader");
        }

        public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var errors = new List<ExcelError>();
            var sheets = new Dictionary<string, SASheetData>();

            // Validation: Fail fast for invalid input
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            try
            {
                // Check file size limit
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > _context.SecuritySettings.MaxFileSizeBytes)
                {
                    var maxMb = _context.SecuritySettings.MaxFileSizeBytes / (1024 * 1024);
                    var fileMb = fileInfo.Length / (1024 * 1024);
                    errors.Add(ExcelError.Critical("Security",
                        $"File size ({fileMb} MB) exceeds maximum allowed ({maxMb} MB)"));
                    return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                }

                return await Task.Run(async () =>
                {
                    // Auto-detect delimiter if using default comma
                    char delimiter = _options.Delimiter;
                    if (_options == CsvReaderOptions.Default)
                    {
                        delimiter = DetectDelimiter(filePath);
                        _context.Logger.LogInfo($"Auto-detected delimiter: '{delimiter}'", "CsvFileReader");
                    }

                    var config = new CsvConfiguration(_options.Culture)
                    {
                        Delimiter = delimiter.ToString(),
                        HasHeaderRecord = _options.HasHeaderRow,
                        Encoding = _options.Encoding,
                        BadDataFound = null, // Ignore malformed rows instead of throwing
                        MissingFieldFound = null, // Ignore missing fields
                        TrimOptions = TrimOptions.Trim,
                        DetectDelimiter = false // We handle detection manually
                    };

                    using var reader = new StreamReader(filePath, _options.Encoding);
                    using var csv = new CsvReader(reader, config);

                    // Stream records directly without materializing entire dataset
                    SASheetData sheetData;
                    try
                    {
                        sheetData = await ConvertToSASheetDataStreamingAsync(Path.GetFileNameWithoutExtension(filePath), csv, errors);
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.LogError($"Error reading CSV records from {filePath}", ex, "CsvFileReader");
                        errors.Add(ExcelError.Critical("File", $"Error parsing CSV: {ex.Message}", ex));
                        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                    }

                    if (sheetData.RowCount == 0)
                    {
                        _context.Logger.LogInfo($"CSV file {filePath} contains no data rows", "CsvFileReader");
                        errors.Add(ExcelError.Info("File", "CSV file is empty (no data rows)"));
                    }

                    sheets["Data"] = sheetData;

                    _context.Logger.LogInfo($"Read CSV file with {sheetData.RowCount} rows and {sheetData.ColumnCount} columns", "CsvFileReader");

                    var status = DetermineLoadStatus(sheets, errors);
                    return new ExcelFile(filePath, status, sheets, errors);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _context.Logger.LogInfo($"File read cancelled: {filePath}", "CsvFileReader");
                throw; // Propagate cancellation
            }
            catch (IOException ex)
            {
                _context.Logger.LogError($"I/O error reading CSV file: {filePath}", ex, "CsvFileReader");
                errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (UnauthorizedAccessException ex)
            {
                _context.Logger.LogError($"Access denied reading CSV file: {filePath}", ex, "CsvFileReader");
                errors.Add(ExcelError.Critical("File", $"Access denied: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (Exception ex)
            {
                _context.Logger.LogError($"Unexpected error reading CSV file: {filePath}", ex, "CsvFileReader");
                errors.Add(ExcelError.Critical("File", $"Error reading file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
        }

        private async Task<SASheetData> ConvertToSASheetDataStreamingAsync(string fileName, CsvReader csv, List<ExcelError> errors)
        {
            var sheetName = "Data";

            // String pool for deduplicating text values (categories, repeated strings, etc.)
            var stringPool = new StringPool(initialCapacity: 2048);

            // Read records lazily - no ToList()!
            var records = csv.GetRecords<dynamic>();

            SASheetData? sheetData = null;
            var columnNameCounts = new Dictionary<string, int>();
            List<string>? columnNames = null;
            int rowCount = 0;
            int totalStrings = 0;

            foreach (var record in records)
            {
                rowCount++;

                var recordDict = record as IDictionary<string, object>;
                if (recordDict == null)
                {
                    _context.Logger.LogWarning($"Skipping non-dictionary record at row {rowCount}", "CsvFileReader");
                    continue;
                }

                // Initialize column names from first record
                if (columnNames == null)
                {
                    columnNames = new List<string>();
                    int colIndex = 0;

                    foreach (var kvp in recordDict)
                    {
                        string columnName = kvp.Key;
                        if (string.IsNullOrWhiteSpace(columnName))
                        {
                            columnName = $"Column_{colIndex}";
                        }

                        string uniqueColumnName = EnsureUniqueColumnName(columnName, columnNameCounts);
                        // Intern column names (often repeated across sheets)
                        columnNames.Add(stringPool.Intern(uniqueColumnName));
                        colIndex++;
                    }

                    sheetData = new SASheetData(sheetName, columnNames.ToArray());

                    // IMPORTANT: CsvHelper behavior differs from XLS/XLSX readers
                    // - XLS/XLSX: DataTable/Worksheet INCLUDE header row → we iterate from row 0
                    // - CSV: CsvHelper with HasHeaderRecord=true SKIPS header → GetRecords() returns only data
                    //
                    // Solution: Manually reconstruct header row from columnNames
                    // Trade-off: Header cells are always Text (acceptable - headers are typically text anyway)
                    // Benefit: Simpler than switching to HasHeaderRecord=false and parsing manually
                    //
                    // Row indexing after this: absolute 0-based (row 0 = header, row 1+ = data)
                    var headerRow = new SACellData[columnNames.Count];
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        headerRow[i] = new SACellData(SACellValue.FromString(columnNames[i], stringPool));
                    }
                    sheetData.AddRow(headerRow);
                }

                // Process row data
                var rowData = new SACellData[columnNames.Count];
                int columnIndex = 0;

                foreach (var kvp in recordDict)
                {
                    if (columnIndex < columnNames.Count)
                    {
                        // Use FromString for auto-type detection with string interning
                        string cellText = kvp.Value?.ToString() ?? string.Empty;

                        // Sanitize potential formula injection (=, +, -, @, etc.)
                        cellText = SanitizeCellValue(cellText);

                        // FIX: Treat empty/whitespace strings as Empty cells, not Text
                        if (string.IsNullOrWhiteSpace(cellText))
                        {
                            rowData[columnIndex] = new SACellData(SACellValue.Empty);
                        }
                        else
                        {
                            // Try to infer number format from CSV text (percentages, scientific notation, decimals)
                            var inference = _formatInferenceService.InferFormat(cellText);

                            if (inference != null)
                            {
                                // Format was inferred - create cell with metadata
                                var metadata = new CellMetadata
                                {
                                    NumberFormat = inference.InferredFormat
                                };
                                rowData[columnIndex] = new SACellData(inference.ParsedValue, metadata);
                            }
                            else
                            {
                                // No format inferred - use standard parsing
                                SACellValue cellValue = SACellValue.FromString(cellText, stringPool);
                                rowData[columnIndex] = new SACellData(cellValue);
                            }

                            totalStrings++;
                        }

                        columnIndex++;
                    }
                }

                sheetData!.AddRow(rowData);
            }

            // Handle empty file
            if (sheetData == null)
            {
                _context.Logger.LogWarning("CSV file contains no valid records", "CsvFileReader");
                return new SASheetData(sheetName, Array.Empty<string>());
            }

            // Log interning statistics
            var memorySaved = stringPool.EstimatedMemorySaved(totalStrings);
            _context.Logger.LogInfo($"String interning: {stringPool.Count} unique from {totalStrings} total (~{memorySaved / 1024} KB saved)", "CsvFileReader");

            // Set header row count from user settings
            var headerRowCount = _context.Settings.Current.DataProcessing.DefaultHeaderRowCount;
            sheetData.SetHeaderRowCount(headerRowCount);

            // Trim excess capacity to save memory
            sheetData.TrimExcess();
            _context.Logger.LogInfo($"Sheet trimmed to exact size: {sheetData.RowCount} rows × {sheetData.ColumnCount} cols = {sheetData.CellCount} cells", "CsvFileReader");

            // INTEGRATION: Analyze and enrich sheet data via orchestrator
            var enrichedData = await _context.AnalysisOrchestrator.EnrichAsync(sheetData, errors);

            return enrichedData;
        }

        private char DetectDelimiter(string filePath)
        {
            // Read first few lines to detect delimiter
            var delimiters = new[] { ',', ';', '\t', '|' };
            var delimiterCounts = new Dictionary<char, int>();

            try
            {
                using var reader = new StreamReader(filePath, _options.Encoding);

                // Read first 5 lines for analysis
                var linesToAnalyze = new List<string>();
                for (int i = 0; i < 5 && !reader.EndOfStream; i++)
                {
                    var line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        linesToAnalyze.Add(line);
                    }
                }

                if (linesToAnalyze.Count == 0)
                {
                    _context.Logger.LogWarning("CSV file is empty, using default delimiter ','", "CsvFileReader");
                    return ',';
                }

                // Count each delimiter occurrence and check consistency
                foreach (var delimiter in delimiters)
                {
                    var counts = linesToAnalyze.Select(line => line.Count(c => c == delimiter)).ToList();

                    // Check if delimiter appears consistently across lines
                    if (counts.All(c => c > 0) && counts.Distinct().Count() == 1)
                    {
                        delimiterCounts[delimiter] = counts[0];
                    }
                }

                // Return delimiter with highest consistent count
                if (delimiterCounts.Count != 0)
                {
                    var bestDelimiter = delimiterCounts.OrderByDescending(kvp => kvp.Value).First().Key;
                    return bestDelimiter;
                }

                // Default to comma if no consistent delimiter found
                _context.Logger.LogInfo("No consistent delimiter found, using default ','", "CsvFileReader");
                return ',';
            }
            catch (IOException ex)
            {
                // File I/O errors (locked, permission denied, etc.) - expected, use fallback
                _context.Logger.LogWarning($"Cannot read file to detect delimiter: {ex.Message}, using default ','", "CsvFileReader");
                return ',';
            }
        }

        private static string EnsureUniqueColumnName(string baseName, Dictionary<string, int> columnNameCounts)
        {
            if (!columnNameCounts.TryGetValue(baseName, out int value))
            {
                value = 1;
                columnNameCounts[baseName] = value;
                return baseName;
            }

            columnNameCounts[baseName] = ++value;
            return $"{baseName}_{value}";
        }

        private static LoadStatus DetermineLoadStatus(Dictionary<string, SASheetData> sheets, List<ExcelError> errors)
        {
            var hasErrors = errors.Any(e => e.Level == LogSeverity.Error || e.Level == LogSeverity.Critical);

            if (!hasErrors)
                return LoadStatus.Success;

            return sheets.Count != 0 ? LoadStatus.PartialSuccess : LoadStatus.Failed;
        }

        /// <summary>
        /// Sanitizes cell value to prevent CSV/formula injection attacks.
        /// Prefixes dangerous characters with apostrophe so Excel treats them as text.
        /// Dangerous characters: =, @, tab, carriage return (can trigger formula execution).
        /// Note: +/- are NOT sanitized if followed by digits (valid numbers like -123, +45.67).
        /// </summary>
        private string SanitizeCellValue(string cellText)
        {
            if (!_context.SecuritySettings.SanitizeCsvFormulas)
                return cellText;

            if (string.IsNullOrEmpty(cellText))
                return cellText;

            char firstChar = cellText[0];

            // Always dangerous: formula start, external reference, control characters
            if (firstChar == '=' || firstChar == '@' || firstChar == '\t' || firstChar == '\r')
            {
                return "'" + cellText;
            }

            // +/- are only dangerous if NOT followed by a digit (i.e., not a number)
            // Examples: "-123" is safe (number), "-cmd" is dangerous (potential formula)
            if (firstChar == '+' || firstChar == '-')
            {
                if (cellText.Length > 1 && char.IsDigit(cellText[1]))
                {
                    // Looks like a number, don't sanitize
                    return cellText;
                }
                // Not a number, sanitize
                return "'" + cellText;
            }

            return cellText;
        }
    }
}
