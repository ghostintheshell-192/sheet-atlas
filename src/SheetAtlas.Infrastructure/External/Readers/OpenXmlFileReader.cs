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
        private readonly ISheetAnalysisOrchestrator _analysisOrchestrator;

        public OpenXmlFileReader(
            ILogService logger,
            ICellReferenceParser cellParser,
            IMergedCellProcessor mergedCellProcessor,
            ICellValueReader cellValueReader,
            ISheetAnalysisOrchestrator analysisOrchestrator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cellParser = cellParser ?? throw new ArgumentNullException(nameof(cellParser));
            _mergedCellProcessor = mergedCellProcessor ?? throw new ArgumentNullException(nameof(mergedCellProcessor));
            _cellValueReader = cellValueReader ?? throw new ArgumentNullException(nameof(cellValueReader));
            _analysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
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

                            var sheetData = ProcessSheet(Path.GetFileNameWithoutExtension(filePath), sheetName, workbookPart, worksheetPart, errors);

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

        private SASheetData? ProcessSheet(string fileName, string sheetName, WorkbookPart workbookPart, WorksheetPart worksheetPart, List<ExcelError> errors)
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

            // Populate rows (with numberFormat extraction)
            PopulateSheetRows(sheetData, workbookPart, worksheetPart, sharedStringTable, mergedCells, headerColumns);

            // Trim excess capacity to save memory
            sheetData.TrimExcess();

            // INTEGRATION: Analyze and enrich sheet data via orchestrator
            var enrichedData = _analysisOrchestrator.EnrichAsync(sheetData, fileName, errors).Result;

            return enrichedData;
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

        private void PopulateSheetRows(SASheetData sheetData, WorkbookPart workbookPart, WorksheetPart worksheetPart, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells, Dictionary<int, string> headerColumns)
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

                var rowData = CreateRowData(sheetData.ColumnCount, row, workbookPart, sharedStringTable, mergedCells, firstCol);
                if (rowData != null)
                {
                    sheetData.AddRow(rowData);
                }
            }
        }

        private SACellData[]? CreateRowData(int columnCount, Row row, WorkbookPart workbookPart, SharedStringTable? sharedStringTable, Dictionary<string, SACellValue> mergedCells, int firstCol)
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

                // Extract cell value
                SACellValue cellValue = GetCellValueWithMerge(cell, sharedStringTable, mergedCells);

                // Extract number format (for foundation services)
                string? numberFormat = GetNumberFormat(cell, workbookPart);

                // Create metadata if numberFormat is present (memory optimization)
                SheetAtlas.Core.Domain.ValueObjects.CellMetadata? metadata = null;
                if (numberFormat != null)
                {
                    metadata = new SheetAtlas.Core.Domain.ValueObjects.CellMetadata { NumberFormat = numberFormat };
                }

                rowData[columnIndex] = new SACellData(cellValue, metadata);
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

        /// <summary>
        /// Extracts Excel number format string from cell style.
        /// Returns format like "mm/dd/yyyy", "[$€-407] #,##0.00", or null if General/no format.
        /// </summary>
        private string? GetNumberFormat(Cell cell, WorkbookPart workbookPart)
        {
            // No StyleIndex = default style (General format)
            if (cell.StyleIndex == null)
                return null;

            var stylesPart = workbookPart.WorkbookStylesPart;
            if (stylesPart?.Stylesheet == null)
                return null;

            // Get CellFormat from StyleIndex
            var cellFormats = stylesPart.Stylesheet.CellFormats;
            if (cellFormats == null)
                return null;

            var styleIndex = (int)cell.StyleIndex.Value;
            if (styleIndex < 0 || styleIndex >= cellFormats.Count())
                return null;

            var cellFormat = cellFormats.ElementAt(styleIndex) as CellFormat;
            if (cellFormat?.NumberFormatId == null)
                return null;

            var numberFormatId = cellFormat.NumberFormatId.Value;

            // Built-in formats (0-163): use predefined mapping
            if (numberFormatId < 164)
            {
                return GetBuiltInNumberFormat(numberFormatId);
            }

            // Custom formats (164+): lookup in NumberingFormats collection
            var numberingFormats = stylesPart.Stylesheet.NumberingFormats;
            if (numberingFormats == null)
                return null;

            var customFormat = numberingFormats.Elements<NumberingFormat>()
                .FirstOrDefault(nf => nf.NumberFormatId?.Value == numberFormatId);

            return customFormat?.FormatCode?.Value;
        }

        /// <summary>
        /// Maps built-in Excel number format IDs to format strings.
        /// Only includes commonly used formats; returns null for General/uncommon formats.
        /// </summary>
        private string? GetBuiltInNumberFormat(uint formatId)
        {
            // Common built-in formats
            // Full list: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.numberingformat
            return formatId switch
            {
                0 => null, // General
                1 => "0",
                2 => "0.00",
                3 => "#,##0",
                4 => "#,##0.00",
                9 => "0%",
                10 => "0.00%",
                11 => "0.00E+00",
                12 => "# ?/?",
                13 => "# ??/??",
                14 => "mm/dd/yyyy", // Date
                15 => "d-mmm-yy",
                16 => "d-mmm",
                17 => "mmm-yy",
                18 => "h:mm AM/PM",
                19 => "h:mm:ss AM/PM",
                20 => "h:mm",
                21 => "h:mm:ss",
                22 => "m/d/yy h:mm",
                37 => "#,##0 ;(#,##0)",
                38 => "#,##0 ;[Red](#,##0)",
                39 => "#,##0.00;(#,##0.00)",
                40 => "#,##0.00;[Red](#,##0.00)",
                45 => "mm:ss",
                46 => "[h]:mm:ss",
                47 => "mmss.0",
                48 => "##0.0E+0",
                49 => "@", // Text
                _ => null // Unknown/uncommon format
            };
        }
    }
}
