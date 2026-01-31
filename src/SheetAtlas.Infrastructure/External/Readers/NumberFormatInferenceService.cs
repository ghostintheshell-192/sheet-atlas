using System.Globalization;
using System.Text.RegularExpressions;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Service for inferring Excel NumberFormat from CSV text values.
    /// Handles percentages, scientific notation, and decimal precision detection.
    /// </summary>
    public interface INumberFormatInferenceService
    {
        /// <summary>
        /// Analyzes text and infers Excel NumberFormat + parsed numeric value.
        /// Returns null inference if text is not numeric or doesn't match any pattern.
        /// </summary>
        NumberFormatInference? InferFormat(string cellText);
    }

    /// <summary>
    /// Result of number format inference from CSV text.
    /// Contains parsed value, inferred Excel format, and storage strategy.
    /// </summary>
    public record NumberFormatInference(
        SACellValue ParsedValue,
        string? InferredFormat);

    /// <summary>
    /// Default implementation of number format inference for CSV files.
    /// Recognizes percentages, scientific notation, and decimal precision.
    /// </summary>
    public class NumberFormatInferenceService : INumberFormatInferenceService
    {
        // Pattern: 15% or 15.5%
        private static readonly Regex PercentagePattern = new(@"^(\d+\.?\d*)%$", RegexOptions.Compiled);

        // Pattern: 2.15639E+11 or 1.5e-3
        private static readonly Regex ScientificPattern = new(@"^(\d+\.?\d*)[eE]([+-]?\d+)$", RegexOptions.Compiled);

        public NumberFormatInference? InferFormat(string cellText)
        {
            if (string.IsNullOrWhiteSpace(cellText))
                return null;

            cellText = cellText.Trim();

            // 1. Try percentage pattern
            var percentageInference = TryInferPercentage(cellText);
            if (percentageInference != null)
                return percentageInference;

            // 2. Try scientific notation pattern
            var scientificInference = TryInferScientific(cellText);
            if (scientificInference != null)
                return scientificInference;

            // 3. Try decimal number with precision detection
            var decimalInference = TryInferDecimal(cellText);
            if (decimalInference != null)
                return decimalInference;

            // 4. No pattern matched - return null (caller uses standard parsing)
            return null;
        }

        private NumberFormatInference? TryInferPercentage(string text)
        {
            var match = PercentagePattern.Match(text);
            if (!match.Success)
                return null;

            // Parse the numeric part (e.g., "15.5" from "15.5%")
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numericPart))
                return null;

            // Convert to fraction: 15.5% → 0.155
            double value = numericPart / 100.0;

            // Determine format based on decimal places in original text
            string numericPartStr = match.Groups[1].Value;
            string format = numericPartStr.Contains('.')
                ? "0.0%"  // Has decimals: "15.5%" → format "0.0%"
                : "0%";   // Integer: "15%" → format "0%"

            return new NumberFormatInference(
                SACellValue.FromFloatingPoint(value),
                format);
        }

        private NumberFormatInference? TryInferScientific(string text)
        {
            var match = ScientificPattern.Match(text);
            if (!match.Success)
                return null;

            // Parse as number and infer Excel scientific notation format
            // This allows proper validation and comparison while preserving display format
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double value))
                return null;

            // Determine decimal places in mantissa to generate appropriate format
            // e.g., "8.6823926503546e-05" → count decimals in "8.6823926503546" part
            string mantissa = match.Groups[1].Value;
            int decimalPlaces = CountDecimalPlaces(mantissa);

            // Generate Excel scientific format: "0.00000E+00" (based on decimal count)
            // Minimum 2 decimals for scientific notation readability
            int formatDecimals = Math.Max(decimalPlaces, 2);
            string format = "0." + new string('0', formatDecimals) + "E+00";

            return new NumberFormatInference(
                SACellValue.FromFloatingPoint(value),
                format);
        }

        private NumberFormatInference? TryInferDecimal(string text)
        {
            // Try parsing as double
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double value))
                return null;

            // Count decimal places in original text to preserve precision
            int decimalPlaces = CountDecimalPlaces(text);

            // Generate format string based on decimal places
            // 0 decimals → "0" (integer display)
            // 2 decimals → "0.00"
            // 5 decimals → "0.00000"
            string format = decimalPlaces > 0
                ? "0." + new string('0', decimalPlaces)
                : "0";

            return new NumberFormatInference(
                SACellValue.FromFloatingPoint(value),
                format);
        }

        private static int CountDecimalPlaces(string text)
        {
            int decimalPointIndex = text.IndexOf('.');
            if (decimalPointIndex == -1)
                return 0;

            // Count digits after decimal point (ignoring trailing zeros is intentional -
            // we want to preserve "0.15000" as 5 decimals to match user intent)
            string fractionalPart = text.Substring(decimalPointIndex + 1);

            // Remove any non-digit characters (e.g., thousands separators)
            int digitCount = 0;
            foreach (char c in fractionalPart)
            {
                if (char.IsDigit(c))
                    digitCount++;
            }

            return digitCount;
        }
    }
}
