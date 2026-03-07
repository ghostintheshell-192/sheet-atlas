using System.Diagnostics;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SACellType = SheetAtlas.Core.Domain.ValueObjects.CellType;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Infrastructure.External.Writers
{
    /// <summary>
    /// Service for exporting sheet data to Excel and CSV. Preserves types and number formats from source files.
    /// </summary>
    public class ExcelWriterService : IExcelWriterService
    {
        private readonly ILogService _logger;

        private static readonly string[] _supportedExcelExtensions = new[] { ".xlsx" };

        // Default style index for dates without custom format
        private const uint DefaultDateStyleIndex = 1;

        public ExcelWriterService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<string> SupportedExcelExtensions => _supportedExcelExtensions.AsReadOnly();

        public async Task<ExportResult> WriteToExcelAsync(
            SASheetData sheetData,
            string outputPath,
            ExcelExportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sheetData);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            options ??= new ExcelExportOptions();
            var stopwatch = Stopwatch.StartNew();
            int normalizedCellCount = 0;

            try
            {
                _logger.LogInfo($"Starting Excel export to {outputPath}", "ExcelWriterService");

                await Task.Run(() =>
                {
                    using var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook);
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    // Add stylesheet for number formatting (dates, currency, percentages)
                    var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                    stylesPart.Stylesheet = CreateStylesheet();

                    // Cache for dynamically created number formats
                    var formatCache = new Dictionary<string, uint>();

                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData2 = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(sheetData2);

                    // Create the sheet reference
                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    sheets.Append(new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = sheetData.SheetName.Length > 31
                            ? sheetData.SheetName.Substring(0, 31)
                            : sheetData.SheetName
                    });

                    // Build list of column indices to include (scoped to region when set)
                    var region = options.Region;
                    var columnIndicesToInclude = BuildIncludedColumnIndices(sheetData, options.IncludedColumns, region);

                    uint rowIndex = 1;

                    // Write header row if requested
                    if (options.IncludeHeaders)
                    {
                        var headerRow = new Row { RowIndex = rowIndex };
                        int outputColIndex = 0;
                        for (int col = 0; col < sheetData.ColumnCount; col++)
                        {
                            if (!columnIndicesToInclude.Contains(col))
                                continue;

                            cancellationToken.ThrowIfCancellationRequested();

                            // When region has explicit header rows, read header from the region's
                            // first header row instead of the sheet's global ColumnNames.
                            string originalName;
                            if (region?.HeaderStartRow != null)
                            {
                                var cellValue = sheetData.GetCellValue(region.HeaderStartRow.Value, col);
                                originalName = cellValue.IsEmpty ? sheetData.ColumnNames[col] : cellValue.ToString();
                            }
                            else
                            {
                                originalName = sheetData.ColumnNames[col];
                            }

                            var headerName = options.SemanticNames?.TryGetValue(originalName, out var semantic) == true
                                ? semantic
                                : originalName;
                            var cell = CreateTextCell(headerName, GetColumnReference(outputColIndex), rowIndex);
                            headerRow.Append(cell);
                            outputColIndex++;
                        }
                        sheetData2.Append(headerRow);
                        rowIndex++;
                    }

                    // Write data rows (scoped to region when set)
                    var dataRows = region != null
                        ? sheetData.EnumerateDataRows(region)
                        : sheetData.EnumerateDataRows();
                    foreach (var row in dataRows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var dataRow = new Row { RowIndex = rowIndex };
                        int outputColIndex = 0;
                        for (int col = 0; col < row.ColumnCount; col++)
                        {
                            if (!columnIndicesToInclude.Contains(col))
                                continue;

                            var cellData = row[col];
                            var cell = CreateCellFromCellData(
                                cellData,
                                outputColIndex,
                                rowIndex,
                                options.UseOriginalValues,
                                stylesPart.Stylesheet,
                                formatCache,
                                ref normalizedCellCount);
                            dataRow.Append(cell);
                            outputColIndex++;
                        }
                        sheetData2.Append(dataRow);
                        rowIndex++;
                    }

                    // Save stylesheet after all formats have been added
                    stylesPart.Stylesheet.Save();

                    // Freeze header row if requested
                    if (options.FreezeHeaderRow && options.IncludeHeaders)
                    {
                        var sheetViews = new SheetViews();
                        var sheetView = new SheetView { WorkbookViewId = 0 };
                        var pane = new Pane
                        {
                            VerticalSplit = 1,
                            TopLeftCell = "A2",
                            ActivePane = PaneValues.BottomLeft,
                            State = PaneStateValues.Frozen
                        };
                        sheetView.Append(pane);
                        sheetViews.Append(sheetView);
                        worksheetPart.Worksheet.InsertAt(sheetViews, 0);
                    }

                    workbookPart.Workbook.Save();
                }, cancellationToken);

                stopwatch.Stop();
                var fileInfo = new FileInfo(outputPath);

                _logger.LogInfo(
                    $"Excel export completed: {sheetData.DataRowCount} rows, {normalizedCellCount} normalized cells, {fileInfo.Length} bytes",
                    "ExcelWriterService");

                return ExportResult.Success(
                    outputPath,
                    sheetData.DataRowCount,
                    sheetData.ColumnCount,
                    normalizedCellCount,
                    fileInfo.Length,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogWarning("Excel export cancelled", "ExcelWriterService");
                // Clean up partial file
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"Excel export failed: {ex.Message}", ex, "ExcelWriterService");
                // Clean up partial file
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return ExportResult.Failure(ex.Message, stopwatch.Elapsed);
            }
        }

        public async Task<ExportResult> NormalizeToExcelAsync(
            string sourcePath,
            IReadOnlyDictionary<string, SASheetData> sheetsData,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));
            ArgumentNullException.ThrowIfNull(sheetsData);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            var stopwatch = Stopwatch.StartNew();
            int normalizedCellCount = 0;

            try
            {
                _logger.LogInfo($"Starting normalize-in-place export from {sourcePath} to {outputPath}", "ExcelWriterService");

                // Copy original file to preserve all formatting, merged cells, styles
                File.Copy(sourcePath, outputPath, overwrite: true);

                await Task.Run(() =>
                {
                    using var document = SpreadsheetDocument.Open(outputPath, isEditable: true);
                    var workbookPart = document.WorkbookPart;
                    if (workbookPart == null) return;

                    // Get or create stylesheet for format updates
                    var stylesPart = workbookPart.WorkbookStylesPart
                        ?? workbookPart.AddNewPart<WorkbookStylesPart>();
                    stylesPart.Stylesheet ??= CreateStylesheet();

                    // Build a date style index in the existing stylesheet
                    uint dateStyleIndex = GetOrCreateDateStyleIndex(stylesPart.Stylesheet);

                    var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();

                    foreach (var sheet in sheets)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var sheetName = sheet.Name?.Value;
                        if (sheetName == null) continue;

                        // Find matching enriched data for this sheet
                        if (!sheetsData.TryGetValue(sheetName, out var saSheet)) continue;

                        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                        var sheetDataElement = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                        if (sheetDataElement == null) continue;

                        // Determine which cells to normalize: region-scoped or all
                        // Scope is in EXCEL 0-based coordinates (absolute position in spreadsheet)
                        var regions = saSheet.DataRegions.Values.ToList();
                        var cellsToNormalize = BuildNormalizationScope(saSheet, regions);

                        // Update cells in-place
                        foreach (var row in sheetDataElement.Elements<Row>())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // OpenXml RowIndex is 1-based; convert to 0-based Excel coordinate
                            var excelRow = (int)row.RowIndex!.Value - 1;

                            foreach (var cell in row.Elements<Cell>())
                            {
                                var excelCol = ParseColumnIndex(cell.CellReference!);
                                if (!cellsToNormalize.Contains((excelRow, excelCol))) continue;

                                // Convert Excel coordinates to SASheetData local indices
                                var localRow = saSheet.ToLocalRow(excelRow);
                                var localCol = saSheet.ToLocalColumn(excelCol);

                                // Skip if outside SASheetData bounds
                                if (localRow < 0 || localRow >= saSheet.RowCount) continue;
                                if (localCol < 0 || localCol >= saSheet.ColumnCount) continue;

                                var cellData = saSheet.GetCellData(localRow, localCol);
                                if (cellData.Metadata?.CleanedValue == null) continue;

                                // Update cell value and style when type changed
                                var cleanedValue = cellData.Metadata.CleanedValue.Value;
                                UpdateCellValue(cell, cleanedValue, dateStyleIndex);
                                normalizedCellCount++;
                            }
                        }

                        worksheetPart.Worksheet.Save();
                    }

                    stylesPart.Stylesheet.Save();
                    workbookPart.Workbook.Save();
                }, cancellationToken);

                stopwatch.Stop();
                var fileInfo = new FileInfo(outputPath);

                _logger.LogInfo(
                    $"Normalize export completed: {normalizedCellCount} cells normalized, {fileInfo.Length} bytes",
                    "ExcelWriterService");

                return ExportResult.Success(
                    outputPath,
                    0, // row count not meaningful for in-place normalization
                    0,
                    normalizedCellCount,
                    fileInfo.Length,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogWarning("Normalize export cancelled", "ExcelWriterService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"Normalize export failed: {ex.Message}", ex, "ExcelWriterService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return ExportResult.Failure(ex.Message, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Builds the set of (row, col) positions that should be normalized.
        /// All coordinates are in EXCEL 0-based space (absolute spreadsheet position).
        /// When regions exist, only data cells within regions are included.
        /// When no regions exist, all rows are included.
        /// </summary>
        private static HashSet<(int row, int col)> BuildNormalizationScope(
            SASheetData saSheet, IReadOnlyList<DataRegion> regions)
        {
            var scope = new HashSet<(int row, int col)>();

            if (regions.Count == 0)
            {
                // No regions: normalize all cells (convert local to Excel coordinates)
                for (int localRow = 0; localRow < saSheet.RowCount; localRow++)
                    for (int localCol = 0; localCol < saSheet.ColumnCount; localCol++)
                        scope.Add((saSheet.ToExcelRow(localRow), saSheet.ToExcelColumn(localCol)));
            }
            else
            {
                // Regions store LOCAL coordinates; convert to Excel coordinates
                foreach (var region in regions)
                {
                    int startRow = region.DataStartRow;
                    int endRow = region.DataEndRow ?? saSheet.RowCount - 1;
                    int startCol = region.StartColumn ?? 0;
                    int endCol = region.EndColumn ?? saSheet.ColumnCount - 1;

                    for (int localRow = startRow; localRow <= endRow; localRow++)
                        for (int localCol = startCol; localCol <= endCol; localCol++)
                            scope.Add((saSheet.ToExcelRow(localRow), saSheet.ToExcelColumn(localCol)));
                }
            }

            return scope;
        }

        /// <summary>
        /// Updates an OpenXml cell's value and style to match the normalized type.
        /// DateTime → dateStyleIndex (so Excel shows a date, not a serial number).
        /// Other types → StyleIndex 0 (General) to clear any stale format.
        /// </summary>
        private static void UpdateCellValue(Cell cell, SACellValue cleanedValue, uint dateStyleIndex)
        {
            // Remove existing inline string if present
            cell.InlineString = null;

            switch (cleanedValue.Type)
            {
                case SACellType.FloatingPoint:
                    cell.DataType = CellValues.Number;
                    cell.CellValue = new CellValue(cleanedValue.AsFloatingPoint().ToString(CultureInfo.InvariantCulture));
                    cell.StyleIndex = 0; // General — clear stale date/currency format
                    break;

                case SACellType.Integer:
                    cell.DataType = CellValues.Number;
                    cell.CellValue = new CellValue(cleanedValue.AsInteger().ToString(CultureInfo.InvariantCulture));
                    cell.StyleIndex = 0;
                    break;

                case SACellType.DateTime:
                    cell.DataType = CellValues.Number;
                    cell.CellValue = new CellValue(cleanedValue.AsDateTime().ToOADate().ToString(CultureInfo.InvariantCulture));
                    cell.StyleIndex = dateStyleIndex;
                    break;

                case SACellType.Boolean:
                    cell.DataType = CellValues.Boolean;
                    cell.CellValue = new CellValue(cleanedValue.AsBoolean());
                    cell.StyleIndex = 0;
                    break;

                case SACellType.Text:
                    cell.DataType = CellValues.InlineString;
                    cell.CellValue = null;
                    cell.InlineString = new InlineString { Text = new Text(cleanedValue.AsText()) };
                    cell.StyleIndex = 0;
                    break;

                case SACellType.Empty:
                    break;

                default:
                    cell.DataType = CellValues.InlineString;
                    cell.CellValue = null;
                    cell.InlineString = new InlineString { Text = new Text(cleanedValue.ToString()) };
                    cell.StyleIndex = 0;
                    break;
            }
        }

        /// <summary>
        /// Parses the column index from an Excel cell reference (e.g., "B3" → 1, "AA1" → 26).
        /// </summary>
        private static int ParseColumnIndex(string cellReference)
        {
            int col = 0;
            foreach (char c in cellReference)
            {
                if (!char.IsLetter(c)) break;
                col = col * 26 + (char.ToUpper(c) - 'A' + 1);
            }
            return col - 1; // 0-based
        }

        public async Task<ExportResult> WriteToCsvAsync(
            SASheetData sheetData,
            string outputPath,
            CsvExportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sheetData);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            options ??= new CsvExportOptions();
            var stopwatch = Stopwatch.StartNew();
            int normalizedCellCount = 0;

            try
            {
                _logger.LogInfo($"Starting CSV export to {outputPath}", "ExcelWriterService");

                await Task.Run(() =>
                {
                    using var writer = new StreamWriter(outputPath, false, options.Encoding);

                    // Write BOM if requested (helps Excel open UTF-8 correctly)
                    if (options.IncludeBom && options.Encoding == Encoding.UTF8)
                    {
                        // StreamWriter with UTF8 already includes BOM by default
                        // But we use Encoding.UTF8 which doesn't include BOM
                        // So we need to write it manually if using new UTF8Encoding(false)
                    }

                    // Build list of column indices to include (scoped to region when set)
                    var region = options.Region;
                    var columnIndicesToInclude = BuildIncludedColumnIndices(sheetData, options.IncludedColumns, region);

                    // Write header row if requested
                    if (options.IncludeHeaders)
                    {
                        var headerNames = new List<string>();
                        for (int col = 0; col < sheetData.ColumnCount; col++)
                        {
                            if (!columnIndicesToInclude.Contains(col))
                                continue;

                            string originalName;
                            if (region?.HeaderStartRow != null)
                            {
                                var cellValue = sheetData.GetCellValue(region.HeaderStartRow.Value, col);
                                originalName = cellValue.IsEmpty ? sheetData.ColumnNames[col] : cellValue.ToString();
                            }
                            else
                            {
                                originalName = sheetData.ColumnNames[col];
                            }

                            var headerName = options.SemanticNames?.TryGetValue(originalName, out var semantic) == true
                                ? semantic
                                : originalName;
                            headerNames.Add(headerName);
                        }
                        var headerLine = string.Join(options.Delimiter,
                            headerNames.Select(name => EscapeCsvField(name, options.Delimiter)));
                        writer.WriteLine(headerLine);
                    }

                    // Write data rows (scoped to region when set)
                    var dataRows = region != null
                        ? sheetData.EnumerateDataRows(region)
                        : sheetData.EnumerateDataRows();
                    foreach (var row in dataRows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fields = new List<string>();
                        for (int col = 0; col < row.ColumnCount; col++)
                        {
                            if (!columnIndicesToInclude.Contains(col))
                                continue;

                            var cellData = row[col];
                            var value = GetCellValueForCsv(cellData, options, ref normalizedCellCount);
                            fields.Add(EscapeCsvField(value, options.Delimiter));
                        }
                        writer.WriteLine(string.Join(options.Delimiter, fields));
                    }
                }, cancellationToken);

                stopwatch.Stop();
                var fileInfo = new FileInfo(outputPath);

                _logger.LogInfo(
                    $"CSV export completed: {sheetData.DataRowCount} rows, {normalizedCellCount} normalized cells, {fileInfo.Length} bytes",
                    "ExcelWriterService");

                return ExportResult.Success(
                    outputPath,
                    sheetData.DataRowCount,
                    sheetData.ColumnCount,
                    normalizedCellCount,
                    fileInfo.Length,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogWarning("CSV export cancelled", "ExcelWriterService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"CSV export failed: {ex.Message}", ex, "ExcelWriterService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return ExportResult.Failure(ex.Message, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Builds a set of column indices to include in export.
        /// If includedColumns is null, all columns are included.
        /// </summary>
        private static HashSet<int> BuildIncludedColumnIndices(
            SASheetData sheetData,
            IReadOnlyCollection<string>? includedColumns,
            DataRegion? region = null)
        {
            var result = new HashSet<int>();

            // Determine column range (region bounds when set, otherwise all columns)
            int startCol = region?.StartColumn ?? 0;
            int endCol = Math.Min(region?.EndColumn ?? (sheetData.ColumnCount - 1), sheetData.ColumnCount - 1);

            if (includedColumns == null)
            {
                for (int i = startCol; i <= endCol; i++)
                    result.Add(i);
            }
            else
            {
                var includedSet = new HashSet<string>(includedColumns, StringComparer.OrdinalIgnoreCase);
                for (int i = startCol; i <= endCol; i++)
                {
                    if (includedSet.Contains(sheetData.ColumnNames[i]))
                        result.Add(i);
                }
            }

            return result;
        }

        /// <summary>
        /// Creates an Excel cell from SACellData, using CleanedValue if available.
        /// Preserves number format from source file metadata.
        /// </summary>
        private Cell CreateCellFromCellData(
            SACellData cellData,
            int columnIndex,
            uint rowIndex,
            bool useOriginalValues,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache,
            ref int normalizedCellCount)
        {
            var colRef = GetColumnReference(columnIndex);
            var numberFormat = cellData.Metadata?.NumberFormat;

            // Determine which value to use
            SACellValue valueToWrite;
            if (useOriginalValues || cellData.Metadata?.CleanedValue == null)
            {
                valueToWrite = cellData.Value;
            }
            else
            {
                valueToWrite = cellData.Metadata.CleanedValue.Value;
                normalizedCellCount++;
            }

            // Create cell based on type, applying number format where applicable
            return valueToWrite.Type switch
            {
                SACellType.FloatingPoint => CreateNumberCell(
                    valueToWrite.AsFloatingPoint(), colRef, rowIndex, numberFormat, stylesheet, formatCache),
                SACellType.Integer => CreateNumberCell(
                    valueToWrite.AsInteger(), colRef, rowIndex, numberFormat, stylesheet, formatCache),
                SACellType.DateTime => CreateDateCell(
                    valueToWrite.AsDateTime(), colRef, rowIndex, numberFormat, stylesheet, formatCache),
                SACellType.Boolean => CreateBooleanCell(valueToWrite.AsBoolean(), colRef, rowIndex),
                SACellType.Text => CreateTextCell(valueToWrite.AsText(), colRef, rowIndex),
                SACellType.Empty => CreateTextCell(string.Empty, colRef, rowIndex),
                _ => CreateTextCell(valueToWrite.ToString(), colRef, rowIndex)
            };
        }

        private static Cell CreateNumberCell(
            double value,
            string columnRef,
            uint rowIndex,
            string? numberFormat,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache)
        {
            var cell = new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
            };

            // Apply number format if available (currency, percentage, etc.)
            if (!string.IsNullOrEmpty(numberFormat))
            {
                cell.StyleIndex = GetOrCreateCellFormatIndex(numberFormat, stylesheet, formatCache);
            }

            return cell;
        }

        private static Cell CreateDateCell(
            DateTime value,
            string columnRef,
            uint rowIndex,
            string? numberFormat,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache)
        {
            // Excel stores dates as serial numbers (days since 1899-12-30)
            double serialDate = value.ToOADate();
            var cell = new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(serialDate.ToString(CultureInfo.InvariantCulture))
            };

            // Apply original date format or default
            if (!string.IsNullOrEmpty(numberFormat))
            {
                cell.StyleIndex = GetOrCreateCellFormatIndex(numberFormat, stylesheet, formatCache);
            }
            else
            {
                cell.StyleIndex = DefaultDateStyleIndex;
            }

            return cell;
        }

        private static Cell CreateBooleanCell(bool value, string columnRef, uint rowIndex)
        {
            return new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.Boolean,
                CellValue = new CellValue(value)
            };
        }

        /// <summary>
        /// Gets or creates a cell format index for the given number format.
        /// Uses cache to avoid duplicate formats.
        /// </summary>
        private static uint GetOrCreateCellFormatIndex(
            string numberFormat,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache)
        {
            if (formatCache.TryGetValue(numberFormat, out var cached))
                return cached;

            var numberingFormats = stylesheet.NumberingFormats!;

            // Custom format IDs start at 164; account for existing formats
            uint formatId = 165 + (uint)numberingFormats.Count();

            numberingFormats.Append(new NumberingFormat
            {
                NumberFormatId = formatId,
                FormatCode = numberFormat
            });
            numberingFormats.Count = (uint)numberingFormats.Count();

            var cellFormats = stylesheet.CellFormats!;
            uint styleIndex = (uint)cellFormats.Count();

            cellFormats.Append(new CellFormat
            {
                NumberFormatId = formatId,
                ApplyNumberFormat = true,
                FontId = 0,
                FillId = 0,
                BorderId = 0
            });
            cellFormats.Count = (uint)cellFormats.Count();

            formatCache[numberFormat] = styleIndex;
            return styleIndex;
        }

        private static Cell CreateTextCell(string value, string columnRef, uint rowIndex)
        {
            return new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.InlineString,
                InlineString = new InlineString { Text = new Text(value ?? string.Empty) }
            };
        }

        /// <summary>
        /// Converts column index to Excel column reference (A, B, ... Z, AA, AB, etc.)
        /// </summary>
        private static string GetColumnReference(int columnIndex)
        {
            var result = new StringBuilder();
            while (columnIndex >= 0)
            {
                result.Insert(0, (char)('A' + (columnIndex % 26)));
                columnIndex = (columnIndex / 26) - 1;
            }
            return result.ToString();
        }

        /// <summary>
        /// Gets the string value for CSV export from cell data.
        /// </summary>
        private static string GetCellValueForCsv(
            SACellData cellData,
            CsvExportOptions options,
            ref int normalizedCellCount)
        {
            // Determine which value to use
            SACellValue valueToWrite;
            if (options.UseOriginalValues || cellData.Metadata?.CleanedValue == null)
            {
                valueToWrite = cellData.Value;
            }
            else
            {
                valueToWrite = cellData.Metadata.CleanedValue.Value;
                normalizedCellCount++;
            }

            // Format based on type
            return valueToWrite.Type switch
            {
                SACellType.DateTime => valueToWrite.AsDateTime().ToString(options.DateFormat, CultureInfo.InvariantCulture),
                SACellType.FloatingPoint => valueToWrite.AsFloatingPoint().ToString(CultureInfo.InvariantCulture),
                SACellType.Integer => valueToWrite.AsInteger().ToString(CultureInfo.InvariantCulture),
                SACellType.Boolean => valueToWrite.AsBoolean() ? "TRUE" : "FALSE",
                SACellType.Text => valueToWrite.AsText(),
                SACellType.Empty => string.Empty,
                _ => valueToWrite.ToString()
            };
        }

        /// <summary>
        /// Escapes a field for CSV format (RFC 4180).
        /// </summary>
        private static string EscapeCsvField(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // Check if escaping is needed
            bool needsQuoting = field.Contains(delimiter) ||
                               field.Contains('"') ||
                               field.Contains('\n') ||
                               field.Contains('\r');

            if (!needsQuoting)
                return field;

            // Escape double quotes by doubling them, then wrap in quotes
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// Finds or creates a date style index in an existing stylesheet.
        /// Searches for an existing CellFormat with a date NumberFormatId;
        /// if none found, appends a new "yyyy-mm-dd" format.
        /// </summary>
        private static uint GetOrCreateDateStyleIndex(Stylesheet stylesheet)
        {
            // Well-known built-in date format IDs (Excel reserves 0-163)
            var builtInDateFormatIds = new HashSet<uint> { 14, 15, 16, 17, 22 };

            var cellFormats = stylesheet.CellFormats;
            if (cellFormats != null)
            {
                // Check existing CellFormats for one that references a date NumberFormatId
                uint index = 0;
                foreach (var cf in cellFormats.Elements<CellFormat>())
                {
                    if (cf.NumberFormatId != null && builtInDateFormatIds.Contains(cf.NumberFormatId.Value))
                        return index;
                    index++;
                }

                // Also check custom formats in NumberingFormats
                var customDateFormatIds = new HashSet<uint>();
                var numberingFormats = stylesheet.NumberingFormats;
                if (numberingFormats != null)
                {
                    foreach (var nf in numberingFormats.Elements<NumberingFormat>())
                    {
                        if (nf.FormatCode?.Value != null &&
                            NumberFormatLooksLikeDate(nf.FormatCode.Value))
                        {
                            customDateFormatIds.Add(nf.NumberFormatId!.Value);
                        }
                    }
                }

                if (customDateFormatIds.Count > 0)
                {
                    index = 0;
                    foreach (var cf in cellFormats.Elements<CellFormat>())
                    {
                        if (cf.NumberFormatId != null && customDateFormatIds.Contains(cf.NumberFormatId.Value))
                            return index;
                        index++;
                    }
                }
            }

            // No existing date format found — create one
            var numFormats = stylesheet.NumberingFormats;
            if (numFormats == null)
            {
                numFormats = new NumberingFormats { Count = 0 };
                stylesheet.InsertAt(numFormats, 0);
            }

            uint newFormatId = 164 + (uint)numFormats.Elements<NumberingFormat>().Count();
            numFormats.Append(new NumberingFormat
            {
                NumberFormatId = newFormatId,
                FormatCode = "yyyy-mm-dd"
            });
            numFormats.Count = (uint)numFormats.Elements<NumberingFormat>().Count();

            if (cellFormats == null)
            {
                cellFormats = new CellFormats(new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 };
                stylesheet.Append(cellFormats);
            }

            uint dateStyleIndex = (uint)cellFormats.Elements<CellFormat>().Count();
            cellFormats.Append(new CellFormat
            {
                NumberFormatId = newFormatId,
                ApplyNumberFormat = true,
                FontId = 0,
                FillId = 0,
                BorderId = 0
            });
            cellFormats.Count = (uint)cellFormats.Elements<CellFormat>().Count();

            return dateStyleIndex;
        }

        /// <summary>
        /// Quick check if a number format code looks like a date format.
        /// </summary>
        private static bool NumberFormatLooksLikeDate(string formatCode)
        {
            var lower = formatCode.ToLowerInvariant();
            return lower.Contains("mm") || lower.Contains("dd") ||
                   lower.Contains("yyyy") || lower.Contains("yy") ||
                   lower.Contains("m/d") || lower.Contains("d/m");
        }

        /// <summary>
        /// Creates the stylesheet for the workbook with date formatting.
        /// NumberingFormats is initialized with default date format; additional formats
        /// are added dynamically as cells are written.
        /// </summary>
        private static Stylesheet CreateStylesheet()
        {
            // Built-in format ID 14 = "mm-dd-yy" (or localized short date)
            // We use a custom format for better control: "yyyy-mm-dd"
            const uint DefaultDateFormatId = 164; // Custom formats start at 164

            return new Stylesheet(
                // Number formats - starts with default date, others added dynamically
                new NumberingFormats(
                    new NumberingFormat
                    {
                        NumberFormatId = DefaultDateFormatId,
                        FormatCode = "yyyy-mm-dd"
                    }
                )
                { Count = 1 },

                // Fonts (required, at least one default)
                new Fonts(
                    new Font() // Default font
                )
                { Count = 1 },

                // Fills (required, at least two: none and gray125)
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })
                )
                { Count = 2 },

                // Borders (required, at least one default)
                new Borders(
                    new Border() // Default border (none)
                )
                { Count = 1 },

                // Cell formats - Index 0 = default, Index 1 = default date
                // Additional formats added dynamically via GetOrCreateCellFormatIndex
                new CellFormats(
                    // Index 0: Default format (no specific formatting)
                    new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },
                    // Index 1: Default date format (DefaultDateStyleIndex = 1)
                    new CellFormat
                    {
                        FontId = 0,
                        FillId = 0,
                        BorderId = 0,
                        NumberFormatId = DefaultDateFormatId,
                        ApplyNumberFormat = true
                    }
                )
                { Count = 2 }
            );
        }
    }
}
