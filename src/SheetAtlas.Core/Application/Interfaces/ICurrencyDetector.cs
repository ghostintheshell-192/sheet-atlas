using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Extracts currency information from Excel number format strings.
    /// Used during file load to enhance ColumnMetadata with currency awareness.
    /// </summary>
    /// <remarks>
    /// STATELESS service for dependency injection.
    /// Parses format patterns like "[$€-407] #,##0.00" to extract currency code, symbol, position.
    /// </remarks>
    public interface ICurrencyDetector
    {
        /// <summary>
        /// Analyzes Excel number format string to detect currency information.
        /// </summary>
        /// <param name="numberFormat">Excel number format string (e.g., "[$€-407] #,##0.00")</param>
        /// <returns>Currency info if detected, null if not currency format</returns>
        /// <example>
        /// Input: "[$€-407] #,##0.00"
        /// Output: CurrencyInfo { Code="EUR", Symbol="€", Position=Prefix, DecimalPlaces=2 }
        /// </example>
        CurrencyInfo? DetectCurrency(string numberFormat);

        /// <summary>
        /// Detects if column likely contains mixed currencies (confidence warning).
        /// Examines sample cell formats in column.
        /// </summary>
        /// <param name="cellFormats">Sample of number formats from column cells</param>
        /// <returns>List of distinct currencies found in sample</returns>
        IReadOnlyList<CurrencyInfo> DetectMixedCurrencies(IEnumerable<string> cellFormats);
    }
}
