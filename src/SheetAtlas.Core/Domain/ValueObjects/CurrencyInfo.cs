namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Immutable currency information extracted from Excel number format.
    /// Used for currency-aware comparison and normalization.
    /// </summary>
    public record CurrencyInfo
    {
        /// <summary>
        /// ISO 4217 currency code (USD, EUR, GBP, JPY, etc).
        /// </summary>
        public string Code { get; init; }

        /// <summary>
        /// Currency symbol (€, $, £, ¥, etc).
        /// </summary>
        public string Symbol { get; init; }

        /// <summary>
        /// Position of symbol relative to number.
        /// </summary>
        public CurrencyPosition Position { get; init; }

        /// <summary>
        /// Decimal places in format (e.g., 2 for EUR, 0 for JPY).
        /// </summary>
        public int DecimalPlaces { get; init; }

        /// <summary>
        /// Thousand separator character (`,`, `.`, or space).
        /// </summary>
        public char? ThousandSeparator { get; init; }

        /// <summary>
        /// Decimal separator character (`.` or `,`).
        /// </summary>
        public char DecimalSeparator { get; init; }

        /// <summary>
        /// Source locale from format (e.g., "407" for German).
        /// </summary>
        public string? Locale { get; init; }

        /// <summary>
        /// Detection confidence level.
        /// </summary>
        public ConfidenceLevel Confidence { get; init; }

        /// <summary>
        /// Constructor with required fields.
        /// </summary>
        public CurrencyInfo(
            string code,
            string symbol,
            CurrencyPosition position = CurrencyPosition.Prefix,
            int decimalPlaces = 2,
            char decimalSeparator = '.',
            ConfidenceLevel confidence = ConfidenceLevel.Unambiguous)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            Position = position;
            DecimalPlaces = decimalPlaces;
            DecimalSeparator = decimalSeparator;
            Confidence = confidence;
        }

        /// <summary>
        /// Factory method for common EUR format.
        /// </summary>
        public static CurrencyInfo EUR => new("EUR", "€", CurrencyPosition.Prefix, 2, ',');

        /// <summary>
        /// Factory method for common USD format.
        /// </summary>
        public static CurrencyInfo USD => new("USD", "$", CurrencyPosition.Prefix, 2, '.');

        /// <summary>
        /// Factory method for common GBP format.
        /// </summary>
        public static CurrencyInfo GBP => new("GBP", "£", CurrencyPosition.Prefix, 2, '.');

        /// <summary>
        /// Factory method for common JPY format (no decimals).
        /// </summary>
        public static CurrencyInfo JPY => new("JPY", "¥", CurrencyPosition.Prefix, 0, '.');
    }

    /// <summary>
    /// Position of currency symbol relative to number.
    /// </summary>
    public enum CurrencyPosition
    {
        Prefix,   // €1000
        Suffix,   // 1000€
        Unknown
    }

    /// <summary>
    /// Confidence level for currency detection.
    /// </summary>
    public enum ConfidenceLevel
    {
        Unambiguous,  // Clear format with locale ([$€-407])
        Inferred,     // Inferred from context ($ with EU decimal separator)
        Low           // Ambiguous symbol without context
    }
}
