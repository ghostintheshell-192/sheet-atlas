using System.Text.RegularExpressions;
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
        // Pattern per formato Excel: [$€-407] #,##0.00
        private static readonly Regex _currencyFormatPattern = new(
            @"\[\$(?<symbol>[^\-\]]+)(?:-(?<locale>[^\]]+))?\]",
            RegexOptions.Compiled);

        // Pattern per ISO code in brackets: [EUR]
        private static readonly Regex _isoCodePattern = new(
            @"\[(?<code>[A-Z]{3})\]",
            RegexOptions.Compiled);

        // Mappatura locale → codice valuta
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

        // Mappatura simbolo → codice valuta (fallback senza locale)
        private static readonly Dictionary<string, string> _symbolToCurrency = new()
        {
            { "€", "EUR" },
            { "$", "USD" },  // Ambiguo senza locale
            { "£", "GBP" },
            { "¥", "JPY" },  // Ambiguo (usato anche per CNY), default JPY
            { "₹", "INR" },
            { "₩", "KRW" },
        };

        public CurrencyInfo? DetectCurrency(string numberFormat)
        {
            if (string.IsNullOrWhiteSpace(numberFormat))
                return null;

            // Prova a estrarre da pattern Excel standard [$symbol-locale]
            var match = _currencyFormatPattern.Match(numberFormat);
            if (match.Success)
            {
                var symbol = match.Groups["symbol"].Value;
                var locale = match.Groups["locale"].Value;

                // Determina codice valuta
                string? currencyCode = null;
                ConfidenceLevel confidence;

                if (!string.IsNullOrEmpty(locale))
                {
                    // Con locale → unambiguous
                    currencyCode = DetermineCurrencyFromLocale(locale, symbol);
                    confidence = ConfidenceLevel.Unambiguous;
                }
                else
                {
                    // Senza locale → low confidence
                    currencyCode = DetermineCurrencyFromSymbol(symbol);
                    confidence = ConfidenceLevel.Low;
                }

                if (currencyCode == null)
                    return null;

                // Estrai informazioni aggiuntive dal formato
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

            // Prova pattern ISO code [EUR]
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

            // Prova a trovare simbolo senza brackets ($ #,##0.00 o #,##0.00 $)
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
            var currencies = new Dictionary<string, CurrencyInfo>();

            foreach (var format in cellFormats)
            {
                var currency = DetectCurrency(format);
                if (currency != null && !currencies.ContainsKey(currency.Code))
                {
                    currencies[currency.Code] = currency;
                }
            }

            return currencies.Values.ToList();
        }

        private string? DetermineCurrencyFromLocale(string locale, string symbol)
        {
            // Prova lookup diretto del locale
            if (_localeToCurrency.TryGetValue(locale.ToUpperInvariant(), out var code))
                return code;

            // Fallback su simbolo
            return DetermineCurrencyFromSymbol(symbol);
        }

        private string? DetermineCurrencyFromSymbol(string symbol)
        {
            return _symbolToCurrency.TryGetValue(symbol, out var code) ? code : null;
        }

        private string GetSymbolForCode(string code)
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

        private int CountDecimalPlaces(string format)
        {
            // Cerca pattern .00 o ,00
            var decimalMatch = Regex.Match(format, @"[.,]0+");
            if (decimalMatch.Success)
            {
                var decimals = decimalMatch.Value.Substring(1); // Rimuovi . o ,
                return decimals.Length;
            }

            return 0;
        }

        private (char decimalSeparator, char thousandSeparator) DetectSeparators(string format, string? locale = null)
        {
            // IMPORTANTE: Excel scrive SEMPRE i format string con convenzione US
            // (comma per migliaia, period per decimale), INDIPENDENTEMENTE dal locale.
            // Il locale code determina come il numero viene VISUALIZZATO all'utente.
            //
            // Esempio: [$€-407] #,##0.00
            // - Format string: #,##0.00 (convenzione US: comma=migliaia, period=decimale)
            // - Locale 407 (tedesco): visualizzato come 1.234,56 (period=migliaia, comma=decimale)
            //
            // Quindi dobbiamo:
            // 1. Leggere separatori dal format string (sempre US convention)
            // 2. Invertirli per locale europei che usano convenzione opposta

            var thousandMatch = Regex.Match(format, @"#([.,])##");
            var decimalMatch = Regex.Match(format, @"0([.,])0");

            // Default US convention dal format string
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

            // Il format string RIFLETTE GIÀ i separatori del locale!
            // Tedesco: #,##0.00 → il formato SCRIVE thousand=, decimal=. MA il tedesco USA thousand=. decimal=,
            // Quindi Excel scrive con US convention, ma SIGNIFICA i separatori del locale
            // CONCLUSIONE: leggi thousand e decimal dal formato, poi inverti per locale non-US

            bool shouldInvert = ShouldInvertSeparators(locale);

            if (shouldInvert)
            {
                // Excel scrive sempre US convention (#,##0.00)
                // Ma per locale europei, thousand e decimal sono invertiti
                return (formatThousandSep, formatDecimalSep);
            }

            // Locale US/UK: i separatori nel formato corrispondono alla realtà
            return (formatDecimalSep, formatThousandSep);
        }

        private bool ShouldInvertSeparators(string? locale)
        {
            if (string.IsNullOrEmpty(locale))
                return false;

            // Locale europei che usano virgola come decimale
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
                // Aggiungi altri se necessario
            };

            return europeanLocales.Contains(locale.ToUpperInvariant());
        }

        private CurrencyPosition DetectPosition(string format, string symbol)
        {
            // Trova la posizione del simbolo rispetto ai numeri
            var symbolIndex = format.IndexOf(symbol);
            var numberIndex = format.IndexOfAny(new[] { '#', '0' });

            if (symbolIndex < 0 || numberIndex < 0)
                return CurrencyPosition.Prefix;

            return symbolIndex < numberIndex ? CurrencyPosition.Prefix : CurrencyPosition.Suffix;
        }

        private CurrencyInfo CreateCurrencyInfo(
            string code,
            string symbol,
            CurrencyPosition position,
            int decimalPlaces,
            char decimalSeparator,
            char thousandSeparator,
            string? locale,
            ConfidenceLevel confidence)
        {
            // Usa factory methods statici se disponibili per valute comuni
            // ma solo se parametri corrispondono esattamente
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
                    // Aggiungi locale usando with expression
                    return baseInfo with { Locale = locale, ThousandSeparator = thousandSeparator };
                }
            }

            // Crea nuova istanza con tutti i parametri
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
    }
}
