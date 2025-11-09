using System.Text.RegularExpressions;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Extracts currency information from Excel number format strings.
    /// Implements ICurrencyDetector interface.
    /// </summary>
    public partial class CurrencyDetector : ICurrencyDetector
    {
        private static readonly System.Buffers.SearchValues<char> _formatChars = System.Buffers.SearchValues.Create("#0");

        // Pattern for Excel format: [$€-407] #,##0.00
        private static readonly Regex _currencyFormatPattern = MyRegex();

        // Pattern for ISO code in brackets: [EUR]
        private static readonly Regex _isoCodePattern = new(
            @"\[(?<code>[A-Z]{3})\]",
            RegexOptions.Compiled);

        // Locale to currency code mapping
        private static readonly Dictionary<string, string> _localeToCurrency = new()
        {
            // Euro locales
            { "407", "EUR" },   // German
            { "40C", "EUR" },   // French
            { "410", "EUR" },   // Italian
            { "C0A", "EUR" },   // Spanish
            { "413", "EUR" },   // Dutch

            // Dollar locales
            { "409", "USD" },   // US English
            { "1009", "CAD" },  // Canadian French
            { "1409", "CAD" },  // Canadian English (alternative)
            { "C09", "CAD" },   // Canadian English (hex)
            { "809", "GBP" },   // UK English
            { "0C09", "AUD" },  // Australian English

            // Other currencies
            { "411", "JPY" },   // Japanese
            { "412", "KRW" },   // Korean
            { "404", "CNY" },   // Chinese
        };

        // Symbol to currency code mapping (fallback without locale)
        private static readonly Dictionary<string, string> _symbolToCurrency = new()
        {
            { "€", "EUR" },
            { "$", "USD" },  // Ambiguous without locale
            { "£", "GBP" },
            { "¥", "JPY" },  // Ambiguous (also used for CNY), defaults to JPY
            { "₹", "INR" },
            { "₩", "KRW" },
        };

        public CurrencyInfo? DetectCurrency(string numberFormat)
        {
            if (string.IsNullOrWhiteSpace(numberFormat))
                return null;

            // Try to extract from standard Excel pattern [$symbol-locale]
            var match = _currencyFormatPattern.Match(numberFormat);
            if (match.Success)
            {
                var symbol = match.Groups["symbol"].Value;
                var locale = match.Groups["locale"].Value;

                // Determine currency code
                string? currencyCode = null;
                ConfidenceLevel confidence;

                if (!string.IsNullOrEmpty(locale))
                {
                    // With locale → unambiguous
                    currencyCode = DetermineCurrencyFromLocale(locale, symbol);
                    confidence = ConfidenceLevel.Unambiguous;
                }
                else
                {
                    // Without locale → low confidence
                    currencyCode = DetermineCurrencyFromSymbol(symbol);
                    confidence = ConfidenceLevel.Low;
                }

                if (currencyCode == null)
                    return null;

                // Extract additional information from format
                var decimalPlaces = CountDecimalPlaces(numberFormat);
                var (decimalSeparator, thousandSeparator) = DetectSeparators(numberFormat, locale);
                var position = DetectPosition(numberFormat, symbol);

                return CreateCurrencyInfo(
                    currencyCode,
                    symbol,
                    position,
                    decimalPlaces,
                    decimalSeparator,
                    thousandSeparator,
                    locale,
                    confidence);
            }

            // Try ISO code pattern [EUR]
            var isoMatch = _isoCodePattern.Match(numberFormat);
            if (isoMatch.Success)
            {
                var code = isoMatch.Groups["code"].Value;
                var symbol = GetSymbolForCode(code);
                var decimalPlaces = CountDecimalPlaces(numberFormat);
                var (decimalSeparator, thousandSeparator) = DetectSeparators(numberFormat, null);

                return CreateCurrencyInfo(
                    code,
                    symbol,
                    CurrencyPosition.Prefix,
                    decimalPlaces,
                    decimalSeparator,
                    thousandSeparator,
                    null,
                    ConfidenceLevel.Unambiguous);
            }

            // Try to find symbol without brackets ($ #,##0.00 or #,##0.00 $)
            foreach (var kvp in _symbolToCurrency)
            {
                if (numberFormat.Contains(kvp.Key))
                {
                    var decimalPlaces = CountDecimalPlaces(numberFormat);
                    var (decimalSeparator, thousandSeparator) = DetectSeparators(numberFormat, null);
                    var position = DetectPosition(numberFormat, kvp.Key);

                    return CreateCurrencyInfo(
                        kvp.Value,
                        kvp.Key,
                        position,
                        decimalPlaces,
                        decimalSeparator,
                        thousandSeparator,
                        null,
                        ConfidenceLevel.Low);
                }
            }

            return null;
        }

        public IReadOnlyList<CurrencyInfo> DetectMixedCurrencies(IEnumerable<string> cellFormats)
        {
            Dictionary<string, CurrencyInfo> currencies = new();

            foreach (var format in cellFormats)
            {
                var currency = DetectCurrency(format);
                if (currency != null)
                {
                    currencies.TryAdd(currency.Code, currency);
                }
            }

            return currencies.Values.ToList();
        }

        private string? DetermineCurrencyFromLocale(string locale, string symbol)
        {
            // Try direct locale lookup
            if (_localeToCurrency.TryGetValue(locale.ToUpperInvariant(), out var code))
                return code;

            // Fallback to symbol
            return DetermineCurrencyFromSymbol(symbol);
        }

        private static string? DetermineCurrencyFromSymbol(string symbol)
        {
            return _symbolToCurrency.TryGetValue(symbol, out var code) ? code : null;
        }

        private static string GetSymbolForCode(string code)
        {
            return code switch
            {
                "EUR" => "€",
                "USD" => "$",
                "CAD" => "$",
                "AUD" => "$",
                "GBP" => "£",
                "JPY" => "¥",
                "CNY" => "¥",
                "KRW" => "₩",
                "INR" => "₹",
                _ => code
            };
        }

        private static int CountDecimalPlaces(string format)
        {
            // Search for pattern .00 or ,00
            var decimalMatch = Regex.Match(format, @"[.,]0+");
            if (decimalMatch.Success)
            {
                var decimals = decimalMatch.Value.Substring(1); // Remove . or ,
                return decimals.Length;
            }

            return 0;
        }

        private (char decimalSeparator, char thousandSeparator) DetectSeparators(string format, string? locale = null)
        {
            // IMPORTANT: Excel ALWAYS writes format strings using US convention
            // (comma for thousands, period for decimal), REGARDLESS of locale.
            // The locale code determines how the number is DISPLAYED to the user.
            //
            // Example: [$€-407] #,##0.00
            // - Format string: #,##0.00 (US convention: comma=thousands, period=decimal)
            // - Locale 407 (German): displayed as 1.234,56 (period=thousands, comma=decimal)
            //
            // Therefore we must:
            // 1. Read separators from format string (always US convention)
            // 2. Invert them for European locales that use opposite convention

            var thousandMatch = Regex.Match(format, @"#([.,])##");
            var decimalMatch = Regex.Match(format, @"0([.,])0");

            // Default US convention from format string
            char formatThousandSep = ',';
            char formatDecimalSep = '.';

            if (thousandMatch.Success && decimalMatch.Success)
            {
                formatThousandSep = thousandMatch.Groups[1].Value[0];
                formatDecimalSep = decimalMatch.Groups[1].Value[0];
            }
            else if (decimalMatch.Success)
            {
                formatDecimalSep = decimalMatch.Groups[1].Value[0];
                formatThousandSep = formatDecimalSep == '.' ? ',' : '.';
            }
            else if (thousandMatch.Success)
            {
                formatThousandSep = thousandMatch.Groups[1].Value[0];
                formatDecimalSep = formatThousandSep == ',' ? '.' : ',';
            }

            // The format string ALREADY REFLECTS the locale's separators!
            // German: #,##0.00 → format WRITES thousand=, decimal=. BUT German USES thousand=. decimal=,
            // So Excel writes using US convention, but MEANS the locale's separators
            // CONCLUSION: read thousand and decimal from format, then invert for non-US locales

            bool shouldInvert = ShouldInvertSeparators(locale);

            if (shouldInvert)
            {
                // Excel always writes US convention (#,##0.00)
                // But for European locales, thousand and decimal are inverted
                return (formatThousandSep, formatDecimalSep);
            }

            // US/UK locales: separators in format match reality
            return (formatDecimalSep, formatThousandSep);
        }

        private static bool ShouldInvertSeparators(string? locale)
        {
            if (string.IsNullOrEmpty(locale))
                return false;

            // European locales that use comma as decimal separator
            var europeanLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "407",   // German
                "40C",   // French
                "410",   // Italian
                "C0A",   // Spanish
                "413",   // Dutch
                "816",   // Portuguese
                "813",   // Belgian
                "40E",   // Hungarian
                "415",   // Polish
                "405",   // Czech
                "406",   // Danish
                "41D",   // Swedish
                "414",   // Norwegian
                "422",   // Ukrainian
                "419",   // Russian
                // Add others if needed
            };

            return europeanLocales.Contains(locale.ToUpperInvariant());
        }

        private static CurrencyPosition DetectPosition(string format, string symbol)
        {
            // Find the position of symbol relative to numbers
            var symbolIndex = format.IndexOf(symbol);
            var numberIndex = format.AsSpan().IndexOfAny(_formatChars);

            if (symbolIndex < 0 || numberIndex < 0)
                return CurrencyPosition.Prefix;

            return symbolIndex < numberIndex ? CurrencyPosition.Prefix : CurrencyPosition.Suffix;
        }

        private static CurrencyInfo CreateCurrencyInfo(
            string code,
            string symbol,
            CurrencyPosition position,
            int decimalPlaces,
            char decimalSeparator,
            char thousandSeparator,
            string? locale,
            ConfidenceLevel confidence)
        {
            // Use static factory methods if available for common currencies
            // but only if parameters match exactly
            if (locale != null && confidence == ConfidenceLevel.Unambiguous)
            {
                CurrencyInfo? baseInfo = code switch
                {
                    "EUR" when position == CurrencyPosition.Prefix && decimalPlaces == 2 && decimalSeparator == ','
                        => CurrencyInfo.EUR,
                    "USD" when position == CurrencyPosition.Prefix && decimalPlaces == 2 && decimalSeparator == '.'
                        => CurrencyInfo.USD,
                    "GBP" when position == CurrencyPosition.Prefix && decimalPlaces == 2 && decimalSeparator == '.'
                        => CurrencyInfo.GBP,
                    "JPY" when position == CurrencyPosition.Prefix && decimalPlaces == 0 && decimalSeparator == '.'
                        => CurrencyInfo.JPY,
                    _ => null
                };

                if (baseInfo != null)
                {
                    // Add locale using with expression
                    return baseInfo with { Locale = locale, ThousandSeparator = thousandSeparator };
                }
            }

            // Create new instance with all parameters
            return new CurrencyInfo(
                code,
                symbol,
                position,
                decimalPlaces,
                decimalSeparator,
                confidence)
            {
                ThousandSeparator = thousandSeparator,
                Locale = locale
            };
        }

        [GeneratedRegex(@"\[\$(?<symbol>[^\-\]]+)(?:-(?<locale>[^\]]+))?\]", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}
