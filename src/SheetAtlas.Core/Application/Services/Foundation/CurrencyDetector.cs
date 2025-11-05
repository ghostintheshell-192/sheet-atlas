using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Extracts currency information from Excel number format strings.
    /// Implements ICurrencyDetector interface.
    /// </summary>
    public class CurrencyDetector : ICurrencyDetector
    {
        public CurrencyInfo? DetectCurrency(string numberFormat)
        {
            // TODO: Implement currency detection logic
            // Parse format patterns like "[$€-407] #,##0.00"
            // Extract currency code, symbol, locale, decimal places
            throw new NotImplementedException("CurrencyDetector.DetectCurrency not yet implemented");
        }

        public IReadOnlyList<CurrencyInfo> DetectMixedCurrencies(IEnumerable<string> cellFormats)
        {
            // TODO: Implement mixed currency detection
            // Collect distinct currencies from sample formats
            // Return list for warning if multiple currencies found
            throw new NotImplementedException("CurrencyDetector.DetectMixedCurrencies not yet implemented");
        }
    }
}
