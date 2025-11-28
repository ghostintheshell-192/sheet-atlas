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
    /// Service for exporting enriched sheet data to Excel and CSV formats.
    /// Uses CleanedValue from cell metadata for proper type preservation.
    /// </summary>
    public class ExcelWriterService : IExcelWriterService
    {
        private readonly ILogService _logger;

        private static readonly string[] _supportedExcelExtensions = new[] { ".xlsx" };

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

                    uint rowIndex = 1;

                    // Write header row if requested
                    if (options.IncludeHeaders)
                    {
                        var headerRow = new Row { RowIndex = rowIndex };
                        for (int col = 0; col < sheetData.ColumnCount; col++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var cell = CreateTextCell(sheetData.ColumnNames[col], GetColumnReference(col), rowIndex);
                            headerRow.Append(cell);
                        }
                        sheetData2.Append(headerRow);
                        rowIndex++;
                    }

                    // Write data rows
                    foreach (var row in sheetData.EnumerateDataRows())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var dataRow = new Row { RowIndex = rowIndex };
                        for (int col = 0; col < row.ColumnCount; col++)
                        {
                            var cellData = row[col];
                            var cell = CreateCellFromCellData(cellData, col, rowIndex, options.UseOriginalValues, ref normalizedCellCount);
                            dataRow.Append(cell);
                        }
                        sheetData2.Append(dataRow);
                        rowIndex++;
                    }

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

                    // Write header row if requested
                    if (options.IncludeHeaders)
                    {
                        var headerLine = string.Join(options.Delimiter,
                            sheetData.ColumnNames.Select(name => EscapeCsvField(name, options.Delimiter)));
                        writer.WriteLine(headerLine);
                    }

                    // Write data rows
                    foreach (var row in sheetData.EnumerateDataRows())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fields = new List<string>();
                        for (int col = 0; col < row.ColumnCount; col++)
                        {
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
        /// Creates an Excel cell from SACellData, using CleanedValue if available.
        /// </summary>
        private Cell CreateCellFromCellData(
            SACellData cellData,
            int columnIndex,
            uint rowIndex,
            bool useOriginalValues,
            ref int normalizedCellCount)
        {
            var colRef = GetColumnReference(columnIndex);
            var cellRef = $"{colRef}{rowIndex}";

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

            // Create cell based on type
            return valueToWrite.Type switch
            {
                SACellType.Number => CreateNumberCell(valueToWrite.AsNumber(), colRef, rowIndex),
                SACellType.Integer => CreateNumberCell(valueToWrite.AsInteger(), colRef, rowIndex),
                SACellType.DateTime => CreateDateCell(valueToWrite.AsDateTime(), colRef, rowIndex),
                SACellType.Boolean => CreateBooleanCell(valueToWrite.AsBoolean(), colRef, rowIndex),
                SACellType.Text => CreateTextCell(valueToWrite.AsText(), colRef, rowIndex),
                SACellType.Empty => CreateTextCell(string.Empty, colRef, rowIndex),
                _ => CreateTextCell(valueToWrite.ToString(), colRef, rowIndex)
            };
        }

        private static Cell CreateNumberCell(double value, string columnRef, uint rowIndex)
        {
            return new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
            };
        }

        private static Cell CreateDateCell(DateTime value, string columnRef, uint rowIndex)
        {
            // Excel stores dates as serial numbers (days since 1899-12-30)
            // We write as number with date format
            double serialDate = value.ToOADate();
            return new Cell
            {
                CellReference = $"{columnRef}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(serialDate.ToString(CultureInfo.InvariantCulture)),
                // Note: Would need a StyleIndex referencing a date format for proper display
                // For simplicity, we just write the serial number
            };
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
                SACellType.Number => valueToWrite.AsNumber().ToString(CultureInfo.InvariantCulture),
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
    }
}
