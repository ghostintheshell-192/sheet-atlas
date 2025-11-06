using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Logging.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Logging.Models;
using System.Xml;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Reader for OpenXML Excel formats (.xlsx, .xlsm, .xltx, .xltm)
    /// </summary>
    public class OpenXmlFileReader : IFileFormatReader
    {
        private readonly ILogService _logger;
        private readonly ICellReferenceParser _cellParser;
        private readonly IMergedCellProcessor _mergedCellProcessor;
        private readonly ICellValueReader _cellValueReader;

        public OpenXmlFileReader(
            ILogService logger,
            ICellReferenceParser cellParser,
            IMergedCellProcessor mergedCellProcessor,
            ICellValueReader cellValueReader)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cellParser = cellParser ?? throw new ArgumentNullException(nameof(cellParser));
            _mergedCellProcessor = mergedCellProcessor ?? throw new ArgumentNullException(nameof(mergedCellProcessor));
            _cellValueReader = cellValueReader ?? throw new ArgumentNullException(nameof(cellValueReader));
        }

        public IReadOnlyList<string> SupportedExtensions =>
            new[] { ".xlsx", ".xlsm", ".xltx", ".xltm" }.AsReadOnly();

        public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var errors = new List<ExcelError>();
            var sheets = new Dictionary<string, SASheetData>();

            // Validation: Fail fast for invalid input
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            try
            {
                return await Task.Run(() =>
                {
                    using var document = OpenDocument(filePath);
                    var workbookPart = document.WorkbookPart;

                    if (workbookPart == null)
                    {
                        errors.Add(ExcelError.Critical("Workbook", $"Workbook part missing in {Path.GetFileName(filePath)}"));
                        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                    }

                    // Detect date system (1900 vs 1904)
                    var dateSystem = DetectDateSystem(workbookPart);
                    _logger.LogInfo($"Detected date system: {dateSystem}", "OpenXmlFileReader");

                    var sheetElements = GetSheets(workbookPart);
                    _logger.LogInfo($"Reading Excel file with {sheetElements.Count()} sheets", "OpenXmlFileReader");

                    foreach (var sheet in sheetElements)
                    {
                        // Check cancellation before processing each sheet
                        cancellationToken.ThrowIfCancellationRequested();

                        var sheetName = sheet.Name?.Value;
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            errors.Add(ExcelError.Warning("File", "Found sheet with empty name, skipping"));
                            continue;
                        }

                        try
                        {
                            var sheetId = sheet.Id?.Value;
                            if (sheetId == null)
                            {
                                errors.Add(ExcelError.SheetError(sheetName, "Sheet ID is null, skipping sheet"));
                                continue;
                            }

                            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheetId);

                            if (worksheetPart is null)
                            {
                                errors.Add(ExcelError.SheetError(sheetName, "Worksheet part not found, skipping sheet"));
                                continue;
                            }

                            var sheetData = ProcessSheet(Path.GetFileNameWithoutExtension(filePath), sheetName, workbookPart, worksheetPart);

                            // Skip empty sheets (no columns means no meaningful data)
                            if (sheetData == null)
                            {
                                errors.Add(ExcelError.Info("File", $"Sheet '{sheetName}' is empty and was skipped"));
                                _logger.LogInfo($"Sheet {sheetName} is empty, skipping", "OpenXmlFileReader");
                                continue;
                            }

                            sheets[sheetName] = sheetData;
                            _logger.LogInfo($"Sheet {sheetName} read successfully", "OpenXmlFileReader");
                        }
                        catch (InvalidCastException ex)
                        {
                            // GetPartById could return different type
                            _logger.LogError($"Invalid sheet part type for {sheetName}", ex, "OpenXmlFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Invalid sheet structure", ex));
                        }
                        catch (XmlException ex)
                        {
                            // XML parsing errors: malformed XML in worksheet
                            _logger.LogError($"Malformed XML in sheet {sheetName}", ex, "OpenXmlFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Sheet contains invalid XML: {ex.Message}", ex));
                        }
                        catch (OpenXmlPackageException ex)
                        {
                            // OpenXML-specific errors for single sheet
                            _logger.LogError($"Corrupted sheet {sheetName}", ex, "OpenXmlFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Sheet corrupted: {ex.Message}", ex));
                        }
                    }

                    var status = DetermineLoadStatus(sheets, errors);
                    return new ExcelFile(filePath, status, sheets, errors, dateSystem);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"File read cancelled: {filePath}", "OpenXmlFileReader");
                throw; // Propagate cancellation
            }
            catch (FileFormatException ex)
            {
                // Corrupted .xlsx file
                _logger.LogError($"Corrupted file format: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File",
                    $"Corrupted or invalid .xlsx file: {ex.Message}",
                    ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }

            catch (IOException ex)
            {
                // File I/O errors: locked, permission denied, network issues
                _logger.LogError($"I/O error reading Excel file: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (InvalidOperationException ex)
            {
                // OpenXml-specific errors: corrupted file structure
                _logger.LogError($"Invalid Excel file format: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Invalid Excel file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (OpenXmlPackageException ex)
            {
                // OpenXml package errors: file corrupted or not a valid Excel file
                _logger.LogError($"Excel file is corrupted or invalid: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Corrupted Excel file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
        }

        private SpreadsheetDocument OpenDocument(string filePath)
        {
            return SpreadsheetDocument.Open(filePath, false);
        }

        private IEnumerable<Sheet> GetSheets(WorkbookPart workbookPart)
        {
            return workbookPart.Workbook.Descendants<Sheet>();
        }

        private DateSystem DetectDateSystem(WorkbookPart workbookPart)
        {
            // Access WorkbookProperties to detect Date1904 property
            // If Date1904 is true → 1904 system, otherwise → 1900 system (default)
            var workbookProperties = workbookPart.Workbook.WorkbookProperties;

            if (workbookProperties?.Date1904?.Value == true)
            {
                return DateSystem.Date1904;
            }

            // Default: 1900 system (Windows Excel default)
            return DateSystem.Date1900;
        }

        private SASheetData? ProcessSheet(string fileName, string sheetName, WorkbookPart workbookPart, WorksheetPart worksheetPart)
        {
            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

            var mergedCells = _mergedCellProcessor.ProcessMergedCells(worksheetPart, sharedStringTable);
            var headerColumns = ProcessHeaderRow(worksheetPart, sharedStringTable, mergedCells);

            // If no headers found, return null - caller will handle as empty sheet
            if (!headerColumns.Any())
            {
                _logger.LogWarning($"Sheet {sheetName} has no header row", "OpenXmlFileReader");
                return null;
            }

            // Create SASheetData with column names
            var columnNames = CreateColumnNamesArray(headerColumns);
            var sheetData = new SASheetData(sheetName, columnNames);

            // Populate rows
            PopulateSheetRows(sheetData, worksheetPart, sharedStringTable, mergedCells, headerColumns);

            // Trim excess capacity to save memory
            sheetData.TrimExcess();

            return sheetData;
        }

        private Dictionary<int, string> ProcessHeaderRow(WorksheetPart worksheetPart, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells)
        {
            var firstRow = worksheetPart.Worksheet.Descendants<Row>().FirstOrDefault();
            if (firstRow == null)
                return new Dictionary<int, string>();

            var headerValues = new Dictionary<int, string>();
            foreach (var cell in firstRow.Elements<Cell>())
            {
                var cellRef = cell.CellReference?.Value;
                if (cellRef == null) continue;

                int columnIndex = _cellParser.GetColumnIndex(cellRef);
                string cellValue = GetCellValueWithMerge(cell, sharedStringTable, mergedCells).ToString();
                headerValues[columnIndex] = cellValue;
            }

            return headerValues;
        }

        private string[] CreateColumnNamesArray(Dictionary<int, string> headerColumns)
        {
            if (!headerColumns.Any())
                return Array.Empty<string>();

            int firstCol = headerColumns.Keys.Min();
            int lastCol = headerColumns.Keys.Max();
            int columnCount = lastCol - firstCol + 1;

            var columnNames = new string[columnCount];
            var columnNameCounts = new Dictionary<string, int>();

            for (int i = firstCol; i <= lastCol; i++)
            {
                string headerValue = headerColumns.TryGetValue(i, out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : $"Column_{i}";

                string uniqueColumnName = EnsureUniqueColumnName(headerValue, columnNameCounts);
                columnNames[i - firstCol] = uniqueColumnName;
            }

            return columnNames;
        }

        private string EnsureUniqueColumnName(string baseName, Dictionary<string, int> columnNameCounts)
        {
            if (!columnNameCounts.ContainsKey(baseName))
            {
                columnNameCounts[baseName] = 1;
                return baseName;
            }

            columnNameCounts[baseName]++;
            return $"{baseName}_{columnNameCounts[baseName]}";
        }

        private void PopulateSheetRows(SASheetData sheetData, WorksheetPart worksheetPart, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells, Dictionary<int, string> headerColumns)
        {
            int firstCol = headerColumns.Keys.Min();
            bool isFirstRow = true;

            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                if (isFirstRow)
                {
                    isFirstRow = false;
                    continue; // Skip header row
                }

                var rowData = CreateRowData(sheetData.ColumnCount, row, sharedStringTable, mergedCells, firstCol);
                if (rowData != null)
                {
                    sheetData.AddRow(rowData);
                }
            }
        }

        private SACellData[]? CreateRowData(int columnCount, Row row, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells, int firstCol)
        {
            var rowData = new SACellData[columnCount];
            bool hasData = false;

            // Initialize all cells with Empty
            for (int i = 0; i < columnCount; i++)
            {
                rowData[i] = new SACellData(SACellValue.Empty);
            }

            foreach (var cell in row.Elements<Cell>())
            {
                var cellRef = cell.CellReference?.Value;
                if (cellRef == null) continue;

                int columnIndex = _cellParser.GetColumnIndex(cellRef) - firstCol;
                if (columnIndex < 0 || columnIndex >= columnCount)
                    continue;

                SACellValue cellValue = GetCellValueWithMerge(cell, sharedStringTable, mergedCells);
                rowData[columnIndex] = new SACellData(cellValue);
                hasData = true;
            }

            return hasData ? rowData : null;
        }

        private SACellValue GetCellValueWithMerge(Cell cell, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells)
        {
            var cellRef = cell.CellReference?.Value;
            if (cellRef != null && mergedCells.TryGetValue(cellRef, out SACellValue mergedValue))
            {
                return mergedValue;
            }

            return GetCellValue(cell, sharedStringTable);
        }

        private LoadStatus DetermineLoadStatus(Dictionary<string, SASheetData> sheets, List<ExcelError> errors)
        {
            var hasErrors = errors.Any(e => e.Level == LogSeverity.Error || e.Level == LogSeverity.Critical);

            if (!hasErrors)
                return LoadStatus.Success;

            return sheets.Any() ? LoadStatus.PartialSuccess : LoadStatus.Failed;
        }

        private SACellValue GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
        {
            return _cellValueReader.GetCellValue(cell, sharedStringTable);
        }
    }
}
