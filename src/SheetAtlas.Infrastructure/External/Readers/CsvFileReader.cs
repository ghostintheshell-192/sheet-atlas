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
        private readonly ILogService _logger;
        private readonly ISheetAnalysisOrchestrator _analysisOrchestrator;
        private CsvReaderOptions _options;

        public CsvFileReader(ILogService logger, ISheetAnalysisOrchestrator analysisOrchestrator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _analysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
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
            _logger.LogInfo($"Configured CSV reader with delimiter '{_options.Delimiter}', encoding '{_options.Encoding.WebName}'", "CsvFileReader");
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
                return await Task.Run(async () =>
                {
                    // Auto-detect delimiter if using default comma
                    char delimiter = _options.Delimiter;
                    if (_options == CsvReaderOptions.Default)
                    {
                        delimiter = DetectDelimiter(filePath);
                        _logger.LogInfo($"Auto-detected delimiter: '{delimiter}'", "CsvFileReader");
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
                        _logger.LogError($"Error reading CSV records from {filePath}", ex, "CsvFileReader");
                        errors.Add(ExcelError.Critical("File", $"Error parsing CSV: {ex.Message}", ex));
                        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                    }

                    if (sheetData.RowCount == 0)
                    {
                        _logger.LogInfo($"CSV file {filePath} contains no data rows", "CsvFileReader");
                        errors.Add(ExcelError.Info("File", "CSV file is empty (no data rows)"));
                    }

                    sheets["Data"] = sheetData;

                    _logger.LogInfo($"Read CSV file with {sheetData.RowCount} rows and {sheetData.ColumnCount} columns", "CsvFileReader");

                    var status = DetermineLoadStatus(sheets, errors);
                    return new ExcelFile(filePath, status, sheets, errors);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"File read cancelled: {filePath}", "CsvFileReader");
                throw; // Propagate cancellation
            }
            catch (IOException ex)
            {
                _logger.LogError($"I/O error reading CSV file: {filePath}", ex, "CsvFileReader");
                errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Access denied reading CSV file: {filePath}", ex, "CsvFileReader");
                errors.Add(ExcelError.Critical("File", $"Access denied: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error reading CSV file: {filePath}", ex, "CsvFileReader");
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
                    _logger.LogWarning($"Skipping non-dictionary record at row {rowCount}", "CsvFileReader");
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

                        // FIX: Treat empty/whitespace strings as Empty cells, not Text
                        SACellValue cellValue = string.IsNullOrWhiteSpace(cellText)
                            ? SACellValue.Empty
                            : SACellValue.FromString(cellText, stringPool);

                        if (!string.IsNullOrWhiteSpace(cellText))
                            totalStrings++;

                        rowData[columnIndex] = new SACellData(cellValue);
                        columnIndex++;
                    }
                }

                sheetData!.AddRow(rowData);
            }

            // Handle empty file
            if (sheetData == null)
            {
                _logger.LogWarning("CSV file contains no valid records", "CsvFileReader");
                return new SASheetData(sheetName, Array.Empty<string>());
            }

            // Log interning statistics
            var memorySaved = stringPool.EstimatedMemorySaved(totalStrings);
            _logger.LogInfo($"String interning: {stringPool.Count} unique from {totalStrings} total (~{memorySaved / 1024} KB saved)", "CsvFileReader");

            // Set header row count (currently single-row headers only)
            const int headerRowCount = 1;
            sheetData.SetHeaderRowCount(headerRowCount);

            // Trim excess capacity to save memory
            sheetData.TrimExcess();
            _logger.LogInfo($"Sheet trimmed to exact size: {sheetData.RowCount} rows × {sheetData.ColumnCount} cols = {sheetData.CellCount} cells", "CsvFileReader");

            // INTEGRATION: Analyze and enrich sheet data via orchestrator
            var enrichedData = await _analysisOrchestrator.EnrichAsync(sheetData, errors);

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
                    _logger.LogWarning("CSV file is empty, using default delimiter ','", "CsvFileReader");
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
                _logger.LogInfo("No consistent delimiter found, using default ','", "CsvFileReader");
                return ',';
            }
            catch (IOException ex)
            {
                // File I/O errors (locked, permission denied, etc.) - expected, use fallback
                _logger.LogWarning($"Cannot read file to detect delimiter: {ex.Message}, using default ','", "CsvFileReader");
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
    }
}
