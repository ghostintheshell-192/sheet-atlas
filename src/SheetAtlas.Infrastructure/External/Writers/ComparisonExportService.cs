using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Infrastructure.External.Writers
{
    /// <summary>
    /// Service for exporting row comparison results to Excel and CSV formats.
    /// Excel exports include metadata sheet with search terms, files, and timestamp.
    /// </summary>
    public class ComparisonExportService : IComparisonExportService
    {
        private readonly ILogService _logger;

        public ComparisonExportService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExportResult> ExportToExcelAsync(
            RowComparison comparison,
            string outputPath,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(comparison);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInfo($"Starting comparison Excel export to {outputPath}", "ComparisonExportService");

                await Task.Run(() =>
                {
                    using var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook);
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    // Add stylesheet
                    var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                    stylesPart.Stylesheet = CreateStylesheet();
                    stylesPart.Stylesheet.Save();

                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());

                    // Create Info sheet
                    cancellationToken.ThrowIfCancellationRequested();
                    CreateInfoSheet(workbookPart, sheets, comparison, 1);

                    // Create Comparison sheet
                    cancellationToken.ThrowIfCancellationRequested();
                    CreateComparisonSheet(workbookPart, sheets, comparison, 2, includedColumns, semanticNames);

                    workbookPart.Workbook.Save();
                }, cancellationToken);

                stopwatch.Stop();
                var fileInfo = new FileInfo(outputPath);

                _logger.LogInfo(
                    $"Comparison Excel export completed: {comparison.Rows.Count} rows, {fileInfo.Length} bytes",
                    "ComparisonExportService");

                return ExportResult.Success(
                    outputPath,
                    comparison.Rows.Count,
                    comparison.GetAllColumnHeaders().Count + 3, // +3 for Source File, Sheet, Row columns
                    0,
                    fileInfo.Length,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogWarning("Comparison Excel export cancelled", "ComparisonExportService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"Comparison Excel export failed: {ex.Message}", ex, "ComparisonExportService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return ExportResult.Failure(ex.Message, stopwatch.Elapsed);
            }
        }

        public async Task<ExportResult> ExportToCsvAsync(
            RowComparison comparison,
            string outputPath,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(comparison);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInfo($"Starting comparison CSV export to {outputPath}", "ComparisonExportService");

                await Task.Run(() =>
                {
                    // UTF-8 with BOM for Excel compatibility
                    using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true));

                    // Group headers by semantic name (merge columns that map to the same semantic name)
                    var headerGroups = GroupHeadersBySemanticName(
                        comparison.GetAllColumnHeaders(),
                        includedColumns,
                        semanticNames);

                    // Write header row: Source File, Sheet, Row, then data columns
                    var headerFields = new List<string> { "Source File", "Sheet", "Row" };
                    headerFields.AddRange(headerGroups.Select(g => g.DisplayName));
                    writer.WriteLine(string.Join(",", headerFields.Select(h => EscapeCsvField(h))));

                    // Write data rows
                    foreach (var row in comparison.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fields = new List<string>
                        {
                            EscapeCsvField(row.FileName),
                            EscapeCsvField(row.SheetName),
                            EscapeCsvField($"R{row.RowIndex + 1}")
                        };

                        // For each header group, find value from any of the original headers
                        foreach (var group in headerGroups)
                        {
                            var cellValue = GetCellValueFromGroup(row, group.OriginalHeaders);
                            fields.Add(EscapeCsvField(cellValue));
                        }

                        writer.WriteLine(string.Join(",", fields));
                    }
                }, cancellationToken);

                stopwatch.Stop();
                var fileInfo = new FileInfo(outputPath);

                _logger.LogInfo(
                    $"Comparison CSV export completed: {comparison.Rows.Count} rows, {fileInfo.Length} bytes",
                    "ComparisonExportService");

                return ExportResult.Success(
                    outputPath,
                    comparison.Rows.Count,
                    comparison.GetAllColumnHeaders().Count + 3,
                    0,
                    fileInfo.Length,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                _logger.LogWarning("Comparison CSV export cancelled", "ComparisonExportService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"Comparison CSV export failed: {ex.Message}", ex, "ComparisonExportService");
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return ExportResult.Failure(ex.Message, stopwatch.Elapsed);
            }
        }

        public string GenerateFilename(RowComparison comparison, string extension)
        {
            ArgumentNullException.ThrowIfNull(comparison);
            if (string.IsNullOrWhiteSpace(extension))
                extension = "xlsx";

            // Format: {YYYY-MM-DD}_{HHmm}_{keyword1}_{keyword2}.{ext}
            var timestamp = comparison.CreatedAt.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);

            // Get first two search terms, sanitized
            var keywords = comparison.SearchTerms
                .Take(2)
                .Select(SanitizeForFilename)
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            var keywordPart = keywords.Count > 0
                ? "_" + string.Join("_", keywords)
                : string.Empty;

            // Remove leading dot from extension if present
            extension = extension.TrimStart('.');

            return $"{timestamp}{keywordPart}.{extension}";
        }

        private void CreateInfoSheet(WorkbookPart workbookPart, Sheets sheets, RowComparison comparison, uint sheetId)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId,
                Name = "Info"
            });

            uint rowIndex = 1;

            // Search Terms
            AddInfoRow(sheetData, rowIndex++, "Search Terms",
                comparison.SearchTerms.Count > 0
                    ? string.Join(", ", comparison.SearchTerms)
                    : "(none)");

            // Files Compared
            var files = comparison.Rows
                .Select(r => r.FileName)
                .Distinct()
                .ToList();
            AddInfoRow(sheetData, rowIndex++, "Files Compared", string.Join(", ", files));

            // Created timestamp
            AddInfoRow(sheetData, rowIndex++, "Created", comparison.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC");

            // Rows Compared
            AddInfoRow(sheetData, rowIndex++, "Rows Compared", comparison.Rows.Count.ToString());

            // Comparison Name
            AddInfoRow(sheetData, rowIndex++, "Comparison Name", comparison.Name);

            // Add freeze pane for header column
            var sheetViews = new SheetViews();
            var sheetView = new SheetView { WorkbookViewId = 0 };
            sheetViews.Append(sheetView);
            worksheetPart.Worksheet.InsertAt(sheetViews, 0);
        }

        private void CreateComparisonSheet(
            WorkbookPart workbookPart,
            Sheets sheets,
            RowComparison comparison,
            uint sheetId,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId,
                Name = "Comparison"
            });

            // Get stylesheet for applying number formats
            var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet
                ?? throw new InvalidOperationException("Stylesheet not found in workbook");
            var formatCache = new Dictionary<string, uint>();

            // Group headers by semantic name (merge columns that map to the same semantic name)
            var headerGroups = GroupHeadersBySemanticName(
                comparison.GetAllColumnHeaders(),
                includedColumns,
                semanticNames);
            uint rowIndex = 1;

            // Header row: Source File, Sheet, Row, then data columns
            var headerRow = new Row { RowIndex = rowIndex };
            int colIndex = 0;
            headerRow.Append(CreateTextCell("Source File", GetColumnReference(colIndex++), rowIndex));
            headerRow.Append(CreateTextCell("Sheet", GetColumnReference(colIndex++), rowIndex));
            headerRow.Append(CreateTextCell("Row", GetColumnReference(colIndex++), rowIndex));
            foreach (var group in headerGroups)
            {
                headerRow.Append(CreateTextCell(group.DisplayName, GetColumnReference(colIndex++), rowIndex));
            }
            sheetData.Append(headerRow);
            rowIndex++;

            // Data rows
            foreach (var row in comparison.Rows)
            {
                var dataRow = new Row { RowIndex = rowIndex };
                colIndex = 0;
                dataRow.Append(CreateTextCell(row.FileName, GetColumnReference(colIndex++), rowIndex));
                dataRow.Append(CreateTextCell(row.SheetName, GetColumnReference(colIndex++), rowIndex));
                dataRow.Append(CreateTextCell($"R{row.RowIndex + 1}", GetColumnReference(colIndex++), rowIndex));

                // For each header group, find value from any of the original headers
                foreach (var group in headerGroups)
                {
                    var cellValue = GetTypedCellValueFromGroup(row, group.OriginalHeaders);
                    dataRow.Append(CreateTypedCell(cellValue, GetColumnReference(colIndex++), rowIndex, stylesheet, formatCache));
                }

                sheetData.Append(dataRow);
                rowIndex++;
            }

            // Save stylesheet changes after adding formats
            workbookPart.WorkbookStylesPart?.Stylesheet.Save();

            // Freeze header row
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

        private static void AddInfoRow(SheetData sheetData, uint rowIndex, string field, string value)
        {
            var row = new Row { RowIndex = rowIndex };
            row.Append(CreateTextCell(field, "A", rowIndex));
            row.Append(CreateTextCell(value, "B", rowIndex));
            sheetData.Append(row);
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
        /// Gets the display name for a column header, using semantic name if available.
        /// </summary>
        private static string GetDisplayName(string originalName, IReadOnlyDictionary<string, string>? semanticNames)
        {
            if (semanticNames != null && semanticNames.TryGetValue(originalName, out var semanticName))
            {
                return semanticName;
            }
            return originalName;
        }

        /// <summary>
        /// Groups original headers by their semantic name, merging columns that map to the same name.
        /// </summary>
        private static List<HeaderGroup> GroupHeadersBySemanticName(
            IReadOnlyList<string> allHeaders,
            IEnumerable<string>? includedColumns,
            IReadOnlyDictionary<string, string>? semanticNames)
        {
            var includedSet = includedColumns != null
                ? new HashSet<string>(includedColumns, StringComparer.OrdinalIgnoreCase)
                : null;

            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var originalHeader in allHeaders)
            {
                // Skip if not in included set
                if (includedSet != null && !includedSet.Contains(originalHeader))
                    continue;

                var displayName = GetDisplayName(originalHeader, semanticNames);

                if (!groups.TryGetValue(displayName, out var originalHeaders))
                {
                    originalHeaders = new List<string>();
                    groups[displayName] = originalHeaders;
                }
                originalHeaders.Add(originalHeader);
            }

            return groups.Select(g => new HeaderGroup(g.Key, g.Value)).ToList();
        }

        /// <summary>
        /// Gets cell value as string from any of the original headers in the group.
        /// </summary>
        private static string GetCellValueFromGroup(ExcelRow row, IReadOnlyList<string> originalHeaders)
        {
            foreach (var header in originalHeaders)
            {
                var value = row.GetCellAsStringByHeader(header);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets typed cell value from any of the original headers in the group.
        /// </summary>
        private static ExportCellValue GetTypedCellValueFromGroup(ExcelRow row, IReadOnlyList<string> originalHeaders)
        {
            foreach (var header in originalHeaders)
            {
                var value = row.GetTypedCellByHeader(header);
                if (!value.Value.IsEmpty)
                    return value;
            }
            return default;
        }

        /// <summary>
        /// Represents a group of original headers that map to the same semantic display name.
        /// </summary>
        private sealed record HeaderGroup(string DisplayName, IReadOnlyList<string> OriginalHeaders);

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            bool needsQuoting = field.Contains(',') ||
                               field.Contains('"') ||
                               field.Contains('\n') ||
                               field.Contains('\r');

            if (!needsQuoting)
                return field;

            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        private static string SanitizeForFilename(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove/replace invalid filename characters
            var sanitized = Regex.Replace(input.Trim(), @"[<>:""/\\|?*\s]+", "_");

            // Remove leading/trailing underscores
            sanitized = sanitized.Trim('_');

            // Truncate to reasonable length
            if (sanitized.Length > 20)
                sanitized = sanitized.Substring(0, 20).TrimEnd('_');

            return sanitized.ToLowerInvariant();
        }

        private static Stylesheet CreateStylesheet()
        {
            return new Stylesheet(
                new NumberingFormats() { Count = 0 },
                new Fonts(new Font()) { Count = 1 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })
                )
                { Count = 2 },
                new Borders(new Border()) { Count = 1 },
                new CellFormats(
                    new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }
                )
                { Count = 1 }
            );
        }

        private static uint GetOrCreateCellFormatIndex(
            string? numberFormat,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache)
        {
            if (string.IsNullOrEmpty(numberFormat))
                return 0;  // Default format

            if (formatCache.TryGetValue(numberFormat, out var cached))
                return cached;

            var numberingFormats = stylesheet.NumberingFormats!;
            uint formatId = 164 + (uint)numberingFormats.Count();

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

        private static Cell CreateTypedCell(
            ExportCellValue cellValue,
            string columnRef,
            uint rowIndex,
            Stylesheet stylesheet,
            Dictionary<string, uint> formatCache)
        {
            var value = cellValue.Value;

            if (value.IsInteger)
            {
                var cell = new Cell
                {
                    CellReference = $"{columnRef}{rowIndex}",
                    DataType = CellValues.Number,
                    CellValue = new CellValue(value.AsInteger().ToString(CultureInfo.InvariantCulture))
                };

                if (!string.IsNullOrEmpty(cellValue.NumberFormat))
                {
                    cell.StyleIndex = GetOrCreateCellFormatIndex(cellValue.NumberFormat, stylesheet, formatCache);
                }

                return cell;
            }

            if (value.IsFloatingPoint)
            {
                var cell = new Cell
                {
                    CellReference = $"{columnRef}{rowIndex}",
                    DataType = CellValues.Number,
                    CellValue = new CellValue(value.AsFloatingPoint().ToString(CultureInfo.InvariantCulture))
                };

                if (!string.IsNullOrEmpty(cellValue.NumberFormat))
                {
                    cell.StyleIndex = GetOrCreateCellFormatIndex(cellValue.NumberFormat, stylesheet, formatCache);
                }

                return cell;
            }

            if (value.IsDateTime)
            {
                // Excel stores dates as OLE Automation date (double)
                var cell = new Cell
                {
                    CellReference = $"{columnRef}{rowIndex}",
                    DataType = CellValues.Number,
                    CellValue = new CellValue(value.AsDateTime().ToOADate().ToString(CultureInfo.InvariantCulture))
                };

                // Apply date format (use source format or default)
                var dateFormat = cellValue.NumberFormat ?? "yyyy-mm-dd";
                cell.StyleIndex = GetOrCreateCellFormatIndex(dateFormat, stylesheet, formatCache);

                return cell;
            }

            if (value.IsBoolean)
            {
                return new Cell
                {
                    CellReference = $"{columnRef}{rowIndex}",
                    DataType = CellValues.Boolean,
                    CellValue = new CellValue(value.AsBoolean())
                };
            }

            // Default: text
            return CreateTextCell(value.ToString(), columnRef, rowIndex);
        }
    }
}
