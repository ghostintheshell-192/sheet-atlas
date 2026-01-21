using System.Data;
using System.Globalization;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Represents a complete row from an Excel sheet for comparison purposes
    /// </summary>
    public class ExcelRow
    {
        public ExcelFile SourceFile { get; }
        public string SheetName { get; }
        public int RowIndex { get; }
        public IReadOnlyList<object?> Cells { get; }
        public IReadOnlyList<string> ColumnHeaders { get; }

        public string FileName => SourceFile?.FileName ?? string.Empty;
        public string DisplayName => $"{FileName} - {SheetName} - Row {RowIndex + 1}";

        public ExcelRow(ExcelFile sourceFile, string sheetName, int rowIndex,
                       IReadOnlyList<object?> cells, IReadOnlyList<string> columnHeaders)
        {
            SourceFile = sourceFile ?? throw new ArgumentNullException(nameof(sourceFile));
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            RowIndex = rowIndex;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            ColumnHeaders = columnHeaders ?? throw new ArgumentNullException(nameof(columnHeaders));
        }

        /// <summary>
        /// Get cell value by column index
        /// </summary>
        public object? GetCell(int columnIndex)
        {
            return columnIndex >= 0 && columnIndex < Cells.Count ? Cells[columnIndex] : null;
        }

        /// <summary>
        /// Get typed cell value with format metadata for export
        /// </summary>
        public ExportCellValue GetTypedCell(int columnIndex)
        {
            var cell = GetCell(columnIndex);
            if (cell is ExportCellValue exportCell)
                return exportCell;
            if (cell is string s)
                return new ExportCellValue(SACellValue.FromText(s));
            return new ExportCellValue(SACellValue.Empty);
        }

        /// <summary>
        /// Get cell value as formatted string for display
        /// </summary>
        public string GetCellAsString(int columnIndex)
        {
            var cell = GetCell(columnIndex);
            if (cell is ExportCellValue exportCell)
            {
                return FormatCellValueForDisplay(exportCell);
            }
            return cell?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Format cell value for display, handling dates and decimal precision
        /// </summary>
        private static string FormatCellValueForDisplay(ExportCellValue cell)
        {
            var value = cell.Value;
            var format = cell.NumberFormat;

            // Check if numeric value should be displayed as date
            if ((value.IsFloatingPoint || value.IsInteger) && IsDateFormat(format))
            {
                double oaDate = value.IsFloatingPoint ? value.AsFloatingPoint() : value.AsInteger();
                try
                {
                    var dateTime = DateTime.FromOADate(oaDate);
                    // Use a readable format, or try to match the original format
                    return FormatDateForDisplay(dateTime, format);
                }
                catch
                {
                    // Invalid OA date, fall through to number formatting
                }
            }

            // Format floating point with reasonable precision
            if (value.IsFloatingPoint)
            {
                return FormatNumberForDisplay(value.AsFloatingPoint(), format);
            }

            // Other types use default ToString
            return value.ToString();
        }

        /// <summary>
        /// Check if number format indicates a date/time value
        /// </summary>
        private static bool IsDateFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            // Common date format indicators
            var lowerFormat = format.ToLowerInvariant();
            return lowerFormat.Contains("yy") ||
                   lowerFormat.Contains("mm") && (lowerFormat.Contains("dd") || lowerFormat.Contains("yy")) ||
                   lowerFormat.Contains("d-") ||
                   lowerFormat.Contains("-d") ||
                   lowerFormat.Contains("h:") ||
                   lowerFormat.Contains(":ss");
        }

        /// <summary>
        /// Format date for display
        /// </summary>
        private static string FormatDateForDisplay(DateTime date, string? format)
        {
            // Map common Excel formats to .NET formats
            if (!string.IsNullOrEmpty(format))
            {
                var netFormat = ConvertExcelDateFormatToNet(format);
                if (!string.IsNullOrEmpty(netFormat))
                {
                    try
                    {
                        return date.ToString(netFormat, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // Fall through to default
                    }
                }
            }

            // Default: show date only if no time component, otherwise full datetime
            return date.TimeOfDay == TimeSpan.Zero
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert Excel date format to .NET format string
        /// </summary>
        private static string? ConvertExcelDateFormatToNet(string excelFormat)
        {
            // Simple conversion for common formats
            var format = excelFormat
                .Replace("yyyy", "yyyy")
                .Replace("yy", "yy")
                .Replace("mmmm", "MMMM")
                .Replace("mmm", "MMM")
                .Replace("mm", "MM")
                .Replace("m", "M")
                .Replace("dddd", "dddd")
                .Replace("ddd", "ddd")
                .Replace("dd", "dd")
                .Replace("d", "d")
                .Replace("hh", "HH")
                .Replace("h", "H")
                .Replace("ss", "ss")
                .Replace("AM/PM", "tt")
                .Replace("am/pm", "tt");

            // Remove Excel-specific formatting like color codes [Red], etc.
            if (format.Contains('['))
            {
                format = System.Text.RegularExpressions.Regex.Replace(format, @"\[[^\]]*\]", "");
            }

            return format.Trim();
        }

        /// <summary>
        /// Format number with reasonable precision for display
        /// </summary>
        private static string FormatNumberForDisplay(double value, string? format)
        {
            // If there's a specific format, try to determine decimal places
            int decimalPlaces = GetDecimalPlacesFromFormat(format);

            if (decimalPlaces >= 0)
            {
                return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
            }

            // Default: use G format which removes trailing zeros, max 10 significant digits
            string result = value.ToString("G10", CultureInfo.InvariantCulture);

            // If scientific notation was used and number is reasonable, use fixed
            if (result.Contains('E') && Math.Abs(value) < 1e10 && Math.Abs(value) > 1e-4)
            {
                result = value.ToString("F6", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            }

            return result;
        }

        /// <summary>
        /// Extract decimal places from Excel number format
        /// </summary>
        private static int GetDecimalPlacesFromFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return -1;

            // Count zeros after decimal point in format like "0.00" or "#,##0.00"
            int dotIndex = format.IndexOf('.');
            if (dotIndex < 0)
                return 0;

            int count = 0;
            for (int i = dotIndex + 1; i < format.Length; i++)
            {
                if (format[i] == '0' || format[i] == '#')
                    count++;
                else
                    break;
            }

            return count;
        }

        /// <summary>
        /// Get cell value by header name with intelligent mapping
        /// </summary>
        public string GetCellAsStringByHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                return string.Empty;

            var headerIndex = ColumnHeaders.ToList().IndexOf(headerName);
            if (headerIndex >= 0)
            {
                return GetCellAsString(headerIndex);
            }

            var normalizedTargetHeader = headerName.Trim().ToLowerInvariant();

            for (int i = 0; i < ColumnHeaders.Count; i++)
            {
                var normalizedHeader = ColumnHeaders[i].Trim().ToLowerInvariant();
                if (normalizedHeader == normalizedTargetHeader)
                {
                    return GetCellAsString(i);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get typed cell value by header name with intelligent mapping
        /// </summary>
        public ExportCellValue GetTypedCellByHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                return new ExportCellValue(SACellValue.Empty);

            var headerIndex = ColumnHeaders.ToList().IndexOf(headerName);
            if (headerIndex >= 0)
            {
                return GetTypedCell(headerIndex);
            }

            var normalizedTargetHeader = headerName.Trim().ToLowerInvariant();

            for (int i = 0; i < ColumnHeaders.Count; i++)
            {
                var normalizedHeader = ColumnHeaders[i].Trim().ToLowerInvariant();
                if (normalizedHeader == normalizedTargetHeader)
                {
                    return GetTypedCell(i);
                }
            }

            return new ExportCellValue(SACellValue.Empty);
        }
    }

    /// <summary>
    /// Represents a comparison between multiple Excel rows
    /// </summary>
    public class RowComparison
    {
        public Guid Id { get; }
        public IReadOnlyList<ExcelRow> Rows { get; }
        public DateTime CreatedAt { get; }
        public string Name { get; set; }
        public IReadOnlyList<RowComparisonWarning> Warnings { get; private set; }
        public IReadOnlyList<string> SearchTerms { get; }

        public RowComparison(IReadOnlyList<ExcelRow> rows, IReadOnlyList<string>? searchTerms = null, string? name = null)
        {
            if (rows == null || rows.Count < 2)
                throw new ArgumentException("At least two rows are required for comparison", nameof(rows));

            Id = Guid.NewGuid();
            Rows = rows;
            CreatedAt = DateTime.UtcNow;
            Name = name ?? $"Comparison {CreatedAt:HH:mm:ss}";
            SearchTerms = searchTerms ?? Array.Empty<string>().AsReadOnly();
            Warnings = AnalyzeStructuralIssues();
        }

        /// <summary>
        /// Get all unique column headers from all rows
        /// </summary>
        public IReadOnlyList<string> GetAllColumnHeaders()
        {
            return Rows
                .SelectMany(r => r.ColumnHeaders)
                .Distinct()
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Get maximum number of columns across all rows
        /// </summary>
        public int MaxColumns => Rows.Max(r => r.Cells.Count);

        /// <summary>
        /// Get a normalized column mapping that aligns headers across all rows
        /// </summary>
        public IReadOnlyDictionary<string, int> GetNormalizedColumnMapping()
        {
            var allUniqueHeaders = GetAllColumnHeaders();
            var mapping = new Dictionary<string, int>();

            for (int i = 0; i < allUniqueHeaders.Count; i++)
            {
                mapping[allUniqueHeaders[i]] = i;
            }

            return mapping.AsReadOnly();
        }

        /// <summary>
        /// Analyze structural issues and generate warnings
        /// </summary>
        private IReadOnlyList<RowComparisonWarning> AnalyzeStructuralIssues()
        {
            var warnings = new List<RowComparisonWarning>();
            var allHeaders = GetAllColumnHeaders();

            var rowsByFile = Rows.GroupBy(r => r.FileName).ToList();

            foreach (var headerName in allHeaders)
            {
                var filesWithMissingHeader = new List<string>();
                var filesWithDifferentPosition = new List<string>();

                foreach (var fileGroup in rowsByFile)
                {
                    var sampleRow = fileGroup.First();
                    var headerIndex = sampleRow.ColumnHeaders.ToList().IndexOf(headerName);

                    if (headerIndex == -1)
                    {
                        filesWithMissingHeader.Add(sampleRow.FileName);
                    }
                    else
                    {
                        var expectedPosition = allHeaders.ToList().IndexOf(headerName);
                        if (headerIndex != expectedPosition)
                        {
                            filesWithDifferentPosition.Add(sampleRow.FileName);
                        }
                    }
                }

                if (filesWithMissingHeader.Count != 0)
                {
                    warnings.Add(RowComparisonWarning.CreateMissingHeaderWarning(headerName, filesWithMissingHeader));
                }

                if (filesWithDifferentPosition.Count != 0)
                {
                    warnings.Add(RowComparisonWarning.CreateStructureMismatchWarning(headerName, filesWithDifferentPosition));
                }
            }

            return warnings.AsReadOnly();
        }
    }

    /// <summary>
    /// Represents a request to create a row comparison from search results
    /// </summary>
    public class RowComparisonRequest
    {
        public IReadOnlyList<SearchResult> SelectedMatches { get; }
        public IReadOnlyList<string> SearchTerms { get; }
        public string? Name { get; set; }

        public RowComparisonRequest(
            IReadOnlyList<SearchResult> selectedMatches,
            IReadOnlyList<string>? searchTerms = null,
            string? name = null)
        {
            SelectedMatches = selectedMatches ?? throw new ArgumentNullException(nameof(selectedMatches));
            SearchTerms = searchTerms ?? Array.Empty<string>().AsReadOnly();
            Name = name;
        }
    }
}
