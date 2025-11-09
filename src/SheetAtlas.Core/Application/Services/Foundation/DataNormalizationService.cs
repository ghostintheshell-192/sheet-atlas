using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Utilities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Normalizes cell values: dates, numbers, text, booleans.
    /// Implements IDataNormalizationService interface.
    /// </summary>
    public partial class DataNormalizationService : IDataNormalizationService
    {
        // Date system epochs
        private static readonly DateTime _epoch1900 = new DateTime(1899, 12, 30);
        private static readonly DateTime _epoch1904 = new DateTime(1904, 1, 1);

        // Excel's 1900 leap year bug: serial 60 = Feb 29, 1900 (doesn't exist)
        private const double Excel1900LeapYearBugSerial = 60.0;

        public NormalizationResult Normalize(
            object? rawValue,
            string? numberFormat = null,
            CellDataType cellDataType = CellDataType.General,
            DateSystem dateSystem = DateSystem.Date1900)
        {
            // Handle null/empty
            if (rawValue == null)
                return NormalizationResult.Empty;

            // Convert raw value to SACellValue
            var original = ConvertToSACellValue(rawValue);

            if (original.IsEmpty)
                return NormalizationResult.Empty;

            // Detect if value is a date based on format or type
            bool isPotentialDate = NumberFormatHelper.IsDateFormat(numberFormat);

            // Normalize based on detected type
            if (isPotentialDate && original.IsNumber)
            {
                return NormalizeExcelSerialDate(original, dateSystem);
            }

            if (original.IsText)
            {
                return NormalizeText(original, numberFormat);
            }

            if (original.IsNumber)
            {
                return NormalizeNumber(original, numberFormat);
            }

            if (original.IsBoolean)
            {
                return NormalizationResult.Success(original, original, DataType.Boolean);
            }

            if (original.IsDateTime)
            {
                return NormalizationResult.Success(original, original, DataType.Date);
            }

            // Unknown type
            return NormalizationResult.Success(original, original, DataType.Unknown);
        }

        public IReadOnlyList<NormalizationResult> NormalizeBatch(
            IEnumerable<(object? Value, string? Format)> cellValues,
            CellDataType dataType = CellDataType.General,
            DateSystem dateSystem = DateSystem.Date1900)
        {
            var results = new List<NormalizationResult>();

            foreach (var (value, format) in cellValues)
            {
                var result = Normalize(value, format, dataType, dateSystem);
                results.Add(result);
            }

            return results.AsReadOnly();
        }

        #region Date Normalization

        private NormalizationResult NormalizeExcelSerialDate(SACellValue original, DateSystem dateSystem)
        {
            double serial = original.AsNumber();

            // Validate serial range
            if (serial < 0)
            {
                return NormalizationResult.Failure(
                    original,
                    DataQualityIssue.OutOfRange,
                    "Negative date serial number");
            }

            if (serial > 2958465) // Year 9999
            {
                return NormalizationResult.Failure(
                    original,
                    DataQualityIssue.OutOfRange,
                    "Date serial number exceeds maximum");
            }

            DateTime result = dateSystem == DateSystem.Date1904
                ? ConvertSerial1904(serial)
                : ConvertSerial1900(serial);

            var cleaned = SACellValue.FromDateTime(result);
            return NormalizationResult.Success(original, cleaned, DataType.Date);
        }

        private static DateTime ConvertSerial1900(double serial)
        {
            // Handle Excel 1900 leap year bug
            if (serial == Excel1900LeapYearBugSerial)
            {
                // Feb 29, 1900 doesn't exist - skip to March 1
                return new DateTime(1900, 3, 1);
            }

            // Serials after the bug need adjustment
            if (serial > Excel1900LeapYearBugSerial)
                serial -= 1;

            return _epoch1900.AddDays(serial);
        }

        private static DateTime ConvertSerial1904(double serial)
        {
            return _epoch1904.AddDays(serial);
        }

        #endregion

        #region Number Normalization

        private static NormalizationResult NormalizeNumber(SACellValue original, string? numberFormat)
        {
            double value = original.AsNumber();

            // Detect if it's currency
            if (NumberFormatHelper.IsCurrencyFormat(numberFormat))
            {
                var cleaned = SACellValue.FromNumber(value);
                return NormalizationResult.Success(original, cleaned, DataType.Currency);
            }

            // Detect if it's percentage
            if (NumberFormatHelper.IsPercentageFormat(numberFormat))
            {
                var cleaned = SACellValue.FromNumber(value);
                return NormalizationResult.Success(original, cleaned, DataType.Percentage);
            }

            // Regular number
            var cleanedValue = SACellValue.FromNumber(value);
            return NormalizationResult.Success(original, cleanedValue, DataType.Number);
        }

        #endregion

        #region Text Normalization

        private NormalizationResult NormalizeText(SACellValue original, string? numberFormat)
        {
            string text = original.AsText();

            // Handle empty/whitespace-only strings
            if (string.IsNullOrWhiteSpace(text))
            {
                return NormalizationResult.Empty;
            }

            // Try parse as boolean (explicit values like "true", "yes", "y")
            // Note: "1" and "0" are NOT treated as boolean - too ambiguous with numeric data
            if (TryParseBoolean(text, out bool boolValue))
            {
                var cleaned = SACellValue.FromBoolean(boolValue);
                return NormalizationResult.Success(original, cleaned, DataType.Boolean);
            }

            // Check if format suggests a number (contains #,##0 or 0.00 patterns)
            bool formatSuggestsNumber = numberFormat != null &&
                (numberFormat.Contains('#') || numberFormat.Contains('0')) &&
                !NumberFormatHelper.IsDateFormat(numberFormat);

            // Check if it's a percentage
            bool isPercentage = text.Contains('%') || NumberFormatHelper.IsPercentageFormat(numberFormat);

            // If format suggests number, try number BEFORE date to avoid false date parsing (e.g., "1,234" → Jan 1, 234 AD)
            if (formatSuggestsNumber && TryParseNumber(text, out double numberValue))
            {
                // Convert percentage to decimal if needed (50 → 0.5 for percentages without %)
                if (isPercentage && !text.Contains('%') && numberValue > 1)
                    numberValue /= 100.0;

                var cleaned = SACellValue.FromNumber(numberValue);
                var dataType = isPercentage ? DataType.Percentage : DataType.Number;
                return NormalizationResult.Success(original, cleaned, dataType);
            }

            // Try parse as date string
            if (TryParseDate(text, out DateTime dateValue))
            {
                var cleaned = SACellValue.FromDateTime(dateValue);
                return NormalizationResult.Success(original, cleaned, DataType.Date);
            }

            // Try parse as number string (if not already tried)
            if (!formatSuggestsNumber && TryParseNumber(text, out numberValue))
            {
                // Convert percentage to decimal if needed (50 → 0.5 for percentages without %)
                if (isPercentage && !text.Contains('%') && numberValue > 1)
                    numberValue /= 100.0;

                var cleaned = SACellValue.FromNumber(numberValue);
                var dataType = isPercentage ? DataType.Percentage : DataType.Number;
                return NormalizationResult.Success(original, cleaned, dataType);
            }

            // Clean text
            var cleanedText = CleanText(text);
            bool hasQualityIssue = cleanedText != text;

            var cleanedValue = SACellValue.FromText(cleanedText);

            if (hasQualityIssue)
            {
                return NormalizationResult.SuccessWithWarning(
                    original,
                    cleanedValue,
                    DataType.Text,
                    DataQualityIssue.ExtraWhitespace);
            }

            return NormalizationResult.Success(original, cleanedValue, DataType.Text);
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove zero-width characters
            text = RemoveZeroWidthCharacters(text);

            // Remove control characters (U+0000 to U+001F except tab, CR, LF)
            text = MyRegex().Replace(text, "");

            // Normalize line endings
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Trim whitespace
            text = text.Trim();

            return text;
        }

        private static string RemoveZeroWidthCharacters(string text)
        {
            // Remove common zero-width characters
            // U+200B: Zero-width space
            // U+FEFF: Zero-width no-break space (BOM)
            // U+200C: Zero-width non-joiner
            // U+200D: Zero-width joiner
            return text
                .Replace("\u200B", "")
                .Replace("\uFEFF", "")
                .Replace("\u200C", "")
                .Replace("\u200D", "");
        }

        #endregion

        #region Boolean Normalization

        private static bool TryParseBoolean(string text, out bool result)
        {
            text = text.Trim().ToLowerInvariant();

            // True values (NOTE: "1" removed - too ambiguous, often used as numeric IDs)
            if (text == "true" || text == "yes" || text == "y" ||
                text == "x" || text == "✓" || text == "✔" || text == "☑")
            {
                result = true;
                return true;
            }

            // False values (NOTE: "0" removed - too ambiguous, often used as numeric values)
            if (text == "false" || text == "no" || text == "n" ||
                text == "✗" || text == "✘" || text == "☐")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        #endregion

        #region Parsing Helpers

        private static bool TryParseDate(string text, out DateTime result)
        {
            // Only validate SYNTACTIC correctness, not semantic validity
            // Let ColumnAnalysisService handle semantic validation using context from adjacent cells

            // Try standard formats
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

            // Try ISO format
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

            // Try US format
            if (DateTime.TryParseExact(text, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

            // Try EU format
            if (DateTime.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;

            result = DateTime.MinValue;
            return false;
        }

        private static bool TryParseNumber(string text, out double result)
        {
            // Remove currency symbols and codes
            text = text.Trim();
            text = Regex.Replace(text, @"[$€£¥₹₽₩₴]", "");
            // Remove currency codes (USD, EUR, GBP, etc.)
            text = Regex.Replace(text, @"\b[A-Z]{3}\b", "");
            text = text.Trim();

            // Remove percentage sign and remember to divide by 100
            bool isPercentage = text.Contains('%');
            text = text.Replace("%", "");
            text = text.Trim();

            // Try parse US format (1,234.56)
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out result))
            {
                if (isPercentage && result > 1)
                    result /= 100.0; // Convert 50 → 0.5
                return true;
            }

            // Try parse EU format with spaces (1 234,56 → 1234.56)
            string normalized = text.Replace(" ", "").Replace(".", "").Replace(",", ".");
            if (double.TryParse(normalized, NumberStyles.Float,
                CultureInfo.InvariantCulture, out result))
            {
                if (isPercentage && result > 1)
                    result /= 100.0;
                return true;
            }

            // Try scientific notation
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out result))
            {
                if (isPercentage && result > 1)
                    result /= 100.0;
                return true;
            }

            result = 0;
            return false;
        }

        #endregion


        #region Conversion Helpers

        private static SACellValue ConvertToSACellValue(object rawValue)
        {
            return rawValue switch
            {
                SACellValue sacValue => sacValue,
                double d => SACellValue.FromNumber(d),
                float f => SACellValue.FromNumber(f),
                int i => SACellValue.FromInteger(i),
                long l => SACellValue.FromInteger(l),
                bool b => SACellValue.FromBoolean(b),
                DateTime dt => SACellValue.FromDateTime(dt),
                string s => SACellValue.FromText(s),
                _ => SACellValue.Empty
            };
        }

        [GeneratedRegex(@"[\u0000-\u0008\u000B-\u000C\u000E-\u001F]")]
        private static partial Regex MyRegex();

        #endregion
    }
}
