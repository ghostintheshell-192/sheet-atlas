namespace SheetAtlas.Core.Application.Utilities
{
    /// <summary>
    /// Utility class for detecting number format patterns in Excel number format strings.
    /// Used by ColumnAnalysisService and DataNormalizationService for type detection.
    /// </summary>
    public static class NumberFormatHelper
    {
        /// <summary>
        /// Determines if a number format string represents a date/time format.
        /// Checks for common date patterns (mm, dd, yyyy, yy, m/d, d/m) and time patterns (h:, am/pm).
        /// </summary>
        /// <param name="format">Excel number format string (e.g., "mm/dd/yyyy", "h:mm:ss")</param>
        /// <returns>True if format contains date or time patterns</returns>
        public static bool IsDateFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            var lower = format.ToLowerInvariant();

            return lower.Contains("mm") || lower.Contains("dd") ||
                   lower.Contains("yyyy") || lower.Contains("yy") ||
                   lower.Contains("m/d") || lower.Contains("d/m") ||
                   lower.Contains("h:") || lower.Contains("am/pm");
        }

        /// <summary>
        /// Determines if a number format string represents a currency format.
        /// Checks for common currency symbols ($, €, £, ¥, ₹, ₽).
        /// </summary>
        /// <param name="format">Excel number format string (e.g., "$#,##0.00")</param>
        /// <returns>True if format contains currency symbols</returns>
        public static bool IsCurrencyFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Contains("$") || format.Contains("€") ||
                   format.Contains("£") || format.Contains("¥") ||
                   format.Contains("₹") || format.Contains("₽");
        }

        /// <summary>
        /// Determines if a number format string represents a percentage format.
        /// Checks for the percentage symbol (%).
        /// </summary>
        /// <param name="format">Excel number format string (e.g., "0.00%")</param>
        /// <returns>True if format contains percentage symbol</returns>
        public static bool IsPercentageFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Contains("%");
        }
    }
}
