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
        private readonly IMergedRangeExtractor<WorksheetPart> _mergedRangeExtractor;
        private readonly ICellValueReader _cellValueReader;
        private readonly ISheetAnalysisOrchestrator _analysisOrchestrator;

        public OpenXmlFileReader(
            ILogService logger,
            ICellReferenceParser cellParser,
            IMergedRangeExtractor<WorksheetPart> mergedRangeExtractor,
            ICellValueReader cellValueReader,
            ISheetAnalysisOrchestrator analysisOrchestrator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cellParser = cellParser ?? throw new ArgumentNullException(nameof(cellParser));
            _mergedRangeExtractor = mergedRangeExtractor ?? throw new ArgumentNullException(nameof(mergedRangeExtractor));
            _cellValueReader = cellValueReader ?? throw new ArgumentNullException(nameof(cellValueReader));
            _analysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
        }

        public IReadOnlyList<string> SupportedExtensions => list.AsReadOnly();

        private static readonly string[] list = new[] { ".xlsx", ".xlsm", ".xltx", ".xltm" };

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
                    using var document = OpenDocument(filePath);
                    var workbookPart = document.WorkbookPart;

                    if (workbookPart == null)
                    {
                        errors.Add(ExcelError.Critical("Workbook", $"Workbook part missing in {Path.GetFileName(filePath)}"));
                        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
                    }

                    var dateSystem = DetectDateSystem(workbookPart);
                    _logger.LogInfo($"Detected date system: {dateSystem}", "OpenXmlFileReader");

                    var sheetElements = GetSheets(workbookPart);
                    _logger.LogInfo($"Reading Excel file with {sheetElements.Count()} sheets", "OpenXmlFileReader");

                    foreach (var sheet in sheetElements)
                    {
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

                            var sheetData = await ProcessSheet(Path.GetFileNameWithoutExtension(filePath), sheetName, workbookPart, worksheetPart, errors);

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
                            _logger.LogError($"Invalid sheet part type for {sheetName}", ex, "OpenXmlFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Invalid sheet structure", ex));
                        }
                        catch (XmlException ex)
                        {
                            _logger.LogError($"Malformed XML in sheet {sheetName}", ex, "OpenXmlFileReader");
                            errors.Add(ExcelError.SheetError(sheetName, $"Sheet contains invalid XML: {ex.Message}", ex));
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
                _logger.LogError($"Corrupted file format: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File",
                    $"Corrupted or invalid .xlsx file: {ex.Message}",
                    ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }

            catch (IOException ex)
            {
                _logger.LogError($"I/O error reading Excel file: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"Invalid Excel file format: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Invalid Excel file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
            catch (OpenXmlPackageException ex)
            {
                _logger.LogError($"Excel file is corrupted or invalid: {filePath}", ex, "OpenXmlFileReader");
                errors.Add(ExcelError.Critical("File", $"Corrupted Excel file: {ex.Message}", ex));
                return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
            }
        }

        private static SpreadsheetDocument OpenDocument(string filePath)
        {
            return SpreadsheetDocument.Open(filePath, false);
        }

        private static IEnumerable<Sheet> GetSheets(WorkbookPart workbookPart)
        {
            return workbookPart.Workbook.Descendants<Sheet>();
        }

        private static DateSystem DetectDateSystem(WorkbookPart workbookPart)
        {
            var workbookProperties = workbookPart.Workbook.WorkbookProperties;

            if (workbookProperties?.Date1904?.Value == true)
            {
                return DateSystem.Date1904;
            }

            return DateSystem.Date1900;
        }

        private async Task<SASheetData?> ProcessSheet(string fileName, string sheetName, WorkbookPart workbookPart, WorksheetPart worksheetPart, List<ExcelError> errors)
        {
            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

            var mergedRanges = _mergedRangeExtractor.ExtractMergedRanges(worksheetPart, sheetName, errors);

            var headerColumns = ProcessHeaderRow(worksheetPart, sharedStringTable, mergedRanges);
            if (headerColumns.Count == 0)
            {
                _logger.LogWarning($"Sheet {sheetName} has no header row", "OpenXmlFileReader");
                return null;
            }

            var columnNames = CreateColumnNamesArray(headerColumns);
            var sheetData = new SASheetData(sheetName, columnNames);

            PopulateMergedCells(sheetData, mergedRanges, headerColumns.Keys.Min());

            PopulateSheetRows(sheetData, workbookPart, worksheetPart, sharedStringTable, mergedRanges, headerColumns);

            const int headerRowCount = 1;
            sheetData.SetHeaderRowCount(headerRowCount);

            sheetData.TrimExcess();
            var enrichedData = await _analysisOrchestrator.EnrichAsync(sheetData, errors);

            return enrichedData;
        }

        /// <summary>
        /// Populates SASheetData.MergedCells collection from extracted ranges.
        /// Uses absolute row indices (row 0 = header, row 1+ = data).
        /// Only adjusts column indices relative to first column.
        /// </summary>
        private static void PopulateMergedCells(SASheetData sheetData, MergedRange[] mergedRanges, int firstCol)
        {
            foreach (var range in mergedRanges)
            {
                int adjustedStartCol = range.StartCol - firstCol;
                int adjustedEndCol = range.EndCol - firstCol;

                var adjustedRange = new MergedRange(range.StartRow, adjustedStartCol, range.EndRow, adjustedEndCol);

                string rangeKey = $"R{range.StartRow}C{adjustedStartCol}:R{range.EndRow}C{adjustedEndCol}";

                sheetData.AddMergedCell(rangeKey, adjustedRange);
            }
        }

        private Dictionary<int, string> ProcessHeaderRow(WorksheetPart worksheetPart, SharedStringTable? sharedStringTable, MergedRange[] mergedRanges)
        {
            var firstRow = worksheetPart.Worksheet.Descendants<Row>().FirstOrDefault();
            if (firstRow == null)
                return new Dictionary<int, string>();

            var headerValues = new Dictionary<int, string>();

            var cellsByRef = firstRow.Elements<Cell>()
                .Where(c => c.CellReference?.Value != null)
                .ToDictionary(c => c.CellReference!.Value!, c => c);

            foreach (var cell in firstRow.Elements<Cell>())
            {
                var cellRef = cell.CellReference?.Value;
                if (cellRef == null) continue;

                int columnIndex = _cellParser.GetColumnIndex(cellRef);

                string cellValue = GetHeaderCellValue(cell, cellsByRef, sharedStringTable, mergedRanges);
                headerValues[columnIndex] = cellValue;
            }

            return headerValues;
        }

        private string[] CreateColumnNamesArray(Dictionary<int, string> headerColumns)
        {
            if (headerColumns.Count == 0)
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

        private void PopulateSheetRows(SASheetData sheetData, WorkbookPart workbookPart, WorksheetPart worksheetPart, SharedStringTable? sharedStringTable, MergedRange[] mergedRanges, Dictionary<int, string> headerColumns)
        {
            int firstCol = headerColumns.Keys.Min();

            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                var rowData = CreateRowData(sheetData.ColumnCount, row, workbookPart, sharedStringTable, firstCol);
                if (rowData != null)
                {
                    sheetData.AddRow(rowData);
                }
            }
        }

        private SACellData[]? CreateRowData(int columnCount, Row row, WorkbookPart workbookPart, SharedStringTable? sharedStringTable, int firstCol)
        {
            var rowData = new SACellData[columnCount];
            bool hasData = false;

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

                SACellValue cellValue = GetCellValue(cell, sharedStringTable);

                string? numberFormat = GetNumberFormat(cell, workbookPart);
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

        /// <summary>
        /// Gets header cell value, expanding merged cells to ensure column names are correct.
        /// For merged cells, retrieves value from top-left cell.
        /// </summary>
        private string GetHeaderCellValue(
            Cell cell,
            Dictionary<string, Cell> cellsByRef,
            SharedStringTable? sharedStringTable,
            MergedRange[] mergedRanges)
        {
            var cellRef = cell.CellReference?.Value;
            if (cellRef == null)
                return string.Empty;

            int row = _cellParser.GetRowIndex(cellRef) - 1;
            int col = _cellParser.GetColumnIndex(cellRef);
            var range = mergedRanges.FirstOrDefault(r =>
                row >= r.StartRow && row <= r.EndRow &&
                col >= r.StartCol && col <= r.EndCol);

            if (range != null && (row != range.StartRow || col != range.StartCol))
            {
                var topLeftRef = _cellParser.CreateCellReference(range.StartCol, range.StartRow + 1);
                if (cellsByRef.TryGetValue(topLeftRef, out var topLeftCell))
                {
                    return GetCellValue(topLeftCell, sharedStringTable).ToString();
                }

                return string.Empty;
            }
            return GetCellValue(cell, sharedStringTable).ToString();
        }

        private static LoadStatus DetermineLoadStatus(Dictionary<string, SASheetData> sheets, List<ExcelError> errors)
        {
            var hasErrors = errors.Any(e => e.Level == LogSeverity.Error || e.Level == LogSeverity.Critical);

            if (!hasErrors)
                return LoadStatus.Success;

            return sheets.Count != 0 ? LoadStatus.PartialSuccess : LoadStatus.Failed;
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
            if (cell.StyleIndex == null)
                return null;

            var stylesPart = workbookPart.WorkbookStylesPart;
            if (stylesPart?.Stylesheet == null)
                return null;

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

            if (numberFormatId < 164)
            {
                return GetBuiltInNumberFormat(numberFormatId);
            }
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
        private static string? GetBuiltInNumberFormat(uint formatId)
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
