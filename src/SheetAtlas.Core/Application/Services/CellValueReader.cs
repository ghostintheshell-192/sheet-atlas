using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection.Metadata;
using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Core.Application.Interfaces;
using SACellValue = SheetAtlas.Core.Domain.ValueObjects.SACellValue;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Reads and parses cell values from Excel worksheets with type preservation.
    /// Handles different cell data types: shared strings, booleans, numbers, dates.
    /// Returns CellValue struct with native types (double, long, bool) instead of all-string.
    /// Uses string interning for text values to reduce memory footprint.
    /// </summary>
    public class CellValueReader : ICellValueReader
    {
        private readonly ConcurrentDictionary<string, string> _stringPool = new();
        private const int MaxPoolSize = 50000;
        private const int MaxInternLength = 100;

        public SACellValue GetCellValue(Cell cell, SharedStringTable? sharedStringTable)
        {
            if (cell == null)
                return SACellValue.Empty;

            string rawValue = cell.InnerText;

            // Handle empty cells
            if (string.IsNullOrWhiteSpace(rawValue))
                return SACellValue.Empty;

            // Handle different cell types based on DataType attribute
            if (cell.DataType != null)
            {
                var cellType = cell.DataType.Value;

                // SharedString: lookup in string table
                if (cellType == CellValues.SharedString)
                {
                    if (int.TryParse(rawValue, out int index) && sharedStringTable != null)
                    {
                        // Use ElementAtOrDefault for safe bounds checking (single iteration)
                        // Corrupted files may have invalid shared string indices
                        var element = sharedStringTable.ElementAtOrDefault(index);
                        rawValue = element != null
                            ? element.InnerText
                            : $"[Invalid SST ref: {index}]";
                    }
                    return CellValueFromText(rawValue);
                }

                // Boolean: "1" = true, "0" = false
                if (cellType == CellValues.Boolean)
                {
                    return SACellValue.FromBoolean(rawValue == "1");
                }

                // Explicit string types
                if (cellType == CellValues.InlineString || cellType == CellValues.String)
                {
                    return CellValueFromText(rawValue);
                }

                // Error values (like #DIV/0!, #REF!)
                if (cellType == CellValues.Error)
                {
                    return SACellValue.FromText($"#ERROR: {rawValue}");
                }

                // Explicit date type (rare, usually handled as number)
                if (cellType == CellValues.Date)
                {
                    if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                        return SACellValue.FromDateTime(dateValue);
                    return CellValueFromText(rawValue);
                }
            }

            // No DataType attribute = numeric or formula
            // Try parse as number (most common for numeric cells)
            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double numericValue))
            {
                // Check if it's an integer
                if (numericValue == Math.Floor(numericValue) && numericValue >= long.MinValue && numericValue <= long.MaxValue)
                {
                    return SACellValue.FromInteger((long)numericValue);
                }
                return SACellValue.FromFloatingPoint(numericValue);
            }

            // Fallback: treat as text
            return CellValueFromText(rawValue);
        }

        /// <summary>
        /// Create CellValue from text with string interning for memory efficiency.
        /// </summary>
        private SACellValue CellValueFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return SACellValue.Empty;

            // Intern short strings to reduce memory duplication
            if (text.Length <= MaxInternLength && _stringPool.Count < MaxPoolSize)
            {
                text = _stringPool.GetOrAdd(text, text);
            }

            return SACellValue.FromText(text);
        }
    }
}
