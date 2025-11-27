using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Configuration;
using SheetAtlas.Logging.Services;
using ExcelDataReader;
using Microsoft.Extensions.Options;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Reader for legacy Excel binary formats (.xls, .xlt)
    /// </summary>
    /// <remarks>
    /// Uses ExcelDataReader library for BIFF8 format support.
    /// Limitations: Does not support merged cell detection or formula extraction.
    /// </remarks>
    public class XlsFileReader : IFileFormatReader
    {
        private readonly ILogService _logger;
        private readonly ISheetAnalysisOrchestrator _analysisOrchestrator;
        private readonly SecuritySettings _securitySettings;
        private static bool _encodingProviderRegistered = false;
        private static readonly object _encodingLock = new object();

        public XlsFileReader(
            ILogService logger,
            ISheetAnalysisOrchestrator analysisOrchestrator,
            IOptions<AppSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _analysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
            _securitySettings = settings?.Value?.Security ?? new SecuritySettings();
            RegisterEncodingProvider();
        }

        private static readonly string[] _supportedExtensions = new[] { ".xls", ".xlt" };

        public IReadOnlyList<string> SupportedExtensions => _supportedExtensions.AsReadOnly();

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
                if (fileInfo.Length > _securitySettings.MaxFileSizeBytes)
                {
                    var maxMb = _securitySettings.MaxFileSizeBytes / (1024 * 1024);
                    var fileMb = fileInfo.Length / (1024 * 1024);
                    errors.Add(ExcelError.Critical("Security",
                        $"File size ({fileMb} MB) exceeds maximum allowed ({maxMb} MB)"));
                    return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                }

                return await Task.Run(async () =>
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    // ExcelDataReader can auto-detect format, but we specify .xls explicitly
                    using var reader = ExcelReaderFactory.CreateBinaryReader(stream);

                    if (reader == null)
                    {
                        errors.Add(ExcelError.Critical("File", "Failed to create Excel reader for .xls file"));
                        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                    }

                    // Read all sheets into a DataSet
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = (tableReader) => new ExcelDataTableConfiguration
                        {
                            // We'll handle headers manually to match OpenXML behavior
                            UseHeaderRow = false
                        }
                    });

                    if (dataSet == null || dataSet.Tables.Count == 0)
                    {
                        _logger.LogWarning($"File {filePath} contains no sheets", "XlsFileReader");
                        errors.Add(ExcelError.Warning("File", "File contains no data sheets"));
                        return new ExcelFile(filePath, LoadStatus.Success, sheets, errors);
                    }

                    _logger.LogInfo($"Reading .xls file with {dataSet.Tables.Count} sheets", "XlsFileReader");

                    // Convert each DataTable to our format
                    foreach (System.Data.DataTable table in dataSet.Tables)
                    {
                        // Check cancellation before processing each sheet
                        cancellationToken.ThrowIfCancellationRequested();

                        var sheetName = table.TableName;
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            errors.Add(ExcelError.Warning("File", "Found sheet with empty name, skipping"));
                            continue;
                        }

                        try
                        {
                            var processedSheet = await ProcessSheetAsync(Path.GetFileNameWithoutExtension(filePath), table, errors);

                            // Skip empty sheets (no rows or no columns) - this is normal, not an error
                            if (processedSheet == null)
                            {
                                errors.Add(ExcelError.Info("File", $"Sheet '{sheetName}' is empty and was skipped"));
                                _logger.LogInfo($"Sheet {sheetName} is empty, skipping", "XlsFileReader");
                                continue;
                            }

                            sheets[sheetName] = processedSheet;
                            _logger.LogInfo($"Sheet {sheetName} read successfully with {processedSheet.RowCount} rows", "XlsFileReader");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing sheet {sheetName}", ex, "XlsFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Error reading sheet: {ex.Message}", ex));
                        }
                    }

                    var status = DetermineLoadStatus(sheets, errors);
                    return new ExcelFile(filePath, status, sheets, errors);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"File read cancelled: {filePath}", "XlsFileReader");
                throw; // Propagate cancellation
            }
            catch (IOException ex)
            {
                _logger.LogError($"I/O error reading .xls file: {filePath}", ex, "XlsFileReader");
                errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Access denied reading .xls file: {filePath}", ex, "XlsFileReader");
                errors.Add(ExcelError.Critical("File", $"Access denied: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected errors (includes ExcelDataReader errors)
                _logger.LogError($"Error reading .xls file: {filePath}", ex, "XlsFileReader");
                errors.Add(ExcelError.Critical("File", $"Error reading file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
        }

        private async Task<SASheetData?> ProcessSheetAsync(string fileName, System.Data.DataTable sourceTable, List<ExcelError> errors)
        {
            var sheetName = sourceTable.TableName;

            // Return null for empty sheets (no rows or no columns) - caller will handle as Info
            if (sourceTable.Rows.Count == 0 || sourceTable.Columns.Count == 0)
            {
                return null;
            }

            // First row is treated as header (matching OpenXML behavior)
            var headerRow = sourceTable.Rows[0];
            var columnNameCounts = new Dictionary<string, int>();
            var columnNames = new List<string>();

            // Create column names from first row
            for (int i = 0; i < sourceTable.Columns.Count; i++)
            {
                string headerValue = headerRow[i]?.ToString()?.Trim() ?? string.Empty;

                // Use "Column_N" if header is empty or whitespace
                if (string.IsNullOrWhiteSpace(headerValue))
                {
                    headerValue = $"Column_{i}";
                }

                string uniqueColumnName = EnsureUniqueColumnName(headerValue, columnNameCounts);
                columnNames.Add(uniqueColumnName);
            }

            var sheetData = new SASheetData(sheetName, columnNames.ToArray());

            // Populate ALL rows (including header row at index 0)
            // NOTE: ExcelDataReader returns DataTable with ALL rows (header included at index 0)
            // This is simpler than CSV where CsvHelper skips the header when HasHeaderRecord=true
            // Row indexing: absolute 0-based (row 0 = header, row 1+ = data)
            for (int rowIndex = 0; rowIndex < sourceTable.Rows.Count; rowIndex++)
            {
                var sourceRow = sourceTable.Rows[rowIndex];
                var rowData = new SACellData[columnNames.Count];
                bool hasData = false;

                for (int colIndex = 0; colIndex < columnNames.Count && colIndex < sourceTable.Columns.Count; colIndex++)
                {
                    var cellValue = sourceRow[colIndex];

                    // OPTIMIZATION: Use native types from ExcelDataReader instead of converting to string
                    // This preserves type information and avoids locale-dependent string formatting
                    SACellValue sacValue = cellValue switch
                    {
                        null => SACellValue.Empty,
                        DBNull => SACellValue.Empty,
                        DateTime dt => SACellValue.FromDateTime(dt),
                        double d => SACellValue.FromNumber(d),
                        float f => SACellValue.FromNumber(f),
                        int i => SACellValue.FromNumber(i),
                        long l => SACellValue.FromNumber(l),
                        decimal dec => SACellValue.FromNumber((double)dec),
                        bool b => SACellValue.FromBoolean(b),
                        string s => string.IsNullOrWhiteSpace(s) ? SACellValue.Empty : SACellValue.FromString(s),
                        _ => string.IsNullOrWhiteSpace(cellValue.ToString()) ? SACellValue.Empty : SACellValue.FromString(cellValue.ToString()!)
                    };

                    rowData[colIndex] = new SACellData(sacValue);

                    if (!sacValue.IsEmpty)
                    {
                        hasData = true;
                    }
                }

                // Only add row if it contains at least some data
                if (hasData)
                {
                    sheetData.AddRow(rowData);
                }
            }

            // Set header row count (currently single-row headers only)
            const int headerRowCount = 1;
            sheetData.SetHeaderRowCount(headerRowCount);

            // Trim excess capacity to save memory
            sheetData.TrimExcess();

            // INTEGRATION: Analyze and enrich sheet data via orchestrator
            var enrichedData = await _analysisOrchestrator.EnrichAsync(sheetData, errors);

            return enrichedData;
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

        private void RegisterEncodingProvider()
        {
            // ExcelDataReader requires this for legacy encodings in .xls files
            // Only register once per application lifetime (thread-safe)
            if (!_encodingProviderRegistered)
            {
                lock (_encodingLock)
                {
                    if (!_encodingProviderRegistered)
                    {
                        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                        _encodingProviderRegistered = true;
                        _logger.LogInfo("Registered CodePagesEncodingProvider for legacy .xls encoding support", "XlsFileReader");
                    }
                }
            }
        }
    }
}
