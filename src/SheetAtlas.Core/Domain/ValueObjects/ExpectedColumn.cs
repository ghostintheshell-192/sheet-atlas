using System.Text.Json.Serialization;

namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Defines an expected column in an Excel template.
    /// Captures column requirements: name, position, type, and validation rules.
    /// Immutable value object with factory methods for common patterns.
    /// </summary>
    public sealed record ExpectedColumn
    {
        /// <summary>Expected column header name (case-insensitive matching).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Expected column position (0-based index).
        /// -1 means column can be anywhere (flexible positioning).
        /// </summary>
        public int Position { get; init; } = -1;

        /// <summary>Expected data type for column values.</summary>
        public DataType ExpectedType { get; init; } = DataType.Unknown;

        /// <summary>Is this column required in the file?</summary>
        public bool IsRequired { get; init; } = true;

        /// <summary>
        /// Minimum type confidence required for validation pass.
        /// Default: 0.8 (80% of values must match expected type).
        /// </summary>
        public double MinTypeConfidence { get; init; } = 0.8;

        /// <summary>
        /// Expected currency code (for Currency type columns).
        /// Example: "EUR", "USD", "GBP".
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExpectedCurrency { get; init; }

        /// <summary>
        /// Validation rules to apply to this column.
        /// </summary>
        public IReadOnlyList<ValidationRule> Rules { get; init; } = Array.Empty<ValidationRule>();

        /// <summary>
        /// Alternative column names that are also acceptable.
        /// Useful for handling variations like "Date" vs "Transaction Date".
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? AlternativeNames { get; init; }

        /// <summary>
        /// User-defined semantic name for this column.
        /// Used to give a meaningful name across different files (e.g., "Revenue" for "Rev 2016").
        /// If set, this name is used for display and matching in column linking.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SemanticName { get; init; }

        /// <summary>
        /// Creates an empty expected column.
        /// Use factory methods (Required, Optional, etc.) for convenience.
        /// </summary>
        public ExpectedColumn() { }

        // === Factory Methods ===

        /// <summary>Create a required column with flexible positioning.</summary>
        public static ExpectedColumn Required(string name, DataType type) =>
            new()
            {
                Name = name,
                ExpectedType = type,
                IsRequired = true,
                Position = -1
            };

        /// <summary>Create a required column at a specific position.</summary>
        public static ExpectedColumn RequiredAt(string name, DataType type, int position) =>
            new()
            {
                Name = name,
                ExpectedType = type,
                IsRequired = true,
                Position = position
            };

        /// <summary>Create an optional column with flexible positioning.</summary>
        public static ExpectedColumn Optional(string name, DataType type) =>
            new()
            {
                Name = name,
                ExpectedType = type,
                IsRequired = false,
                Position = -1
            };

        /// <summary>Create a currency column with expected currency code.</summary>
        public static ExpectedColumn Currency(string name, string currencyCode, bool required = true) =>
            new()
            {
                Name = name,
                ExpectedType = DataType.Currency,
                IsRequired = required,
                Position = -1,
                ExpectedCurrency = currencyCode.ToUpperInvariant()
            };

        /// <summary>Create a date column.</summary>
        public static ExpectedColumn Date(string name, bool required = true) =>
            new()
            {
                Name = name,
                ExpectedType = DataType.Date,
                IsRequired = required,
                Position = -1
            };

        /// <summary>Create a text column.</summary>
        public static ExpectedColumn Text(string name, bool required = true) =>
            new()
            {
                Name = name,
                ExpectedType = DataType.Text,
                IsRequired = required,
                Position = -1
            };

        /// <summary>Create a numeric column.</summary>
        public static ExpectedColumn Number(string name, bool required = true) =>
            new()
            {
                Name = name,
                ExpectedType = DataType.Number,
                IsRequired = required,
                Position = -1
            };

        // === Builder Methods ===

        /// <summary>Add validation rules to this column.</summary>
        public ExpectedColumn WithRules(params ValidationRule[] rules) =>
            this with { Rules = rules };

        /// <summary>Add alternative acceptable names for this column.</summary>
        public ExpectedColumn WithAlternatives(params string[] alternativeNames) =>
            this with { AlternativeNames = alternativeNames };

        /// <summary>Set expected position for this column.</summary>
        public ExpectedColumn AtPosition(int position) =>
            this with { Position = position };

        /// <summary>Set minimum type confidence threshold.</summary>
        public ExpectedColumn WithMinConfidence(double confidence) =>
            this with { MinTypeConfidence = Math.Clamp(confidence, 0.0, 1.0) };

        /// <summary>Set expected currency code.</summary>
        public ExpectedColumn WithCurrency(string currencyCode) =>
            this with { ExpectedCurrency = currencyCode.ToUpperInvariant() };

        /// <summary>Set semantic name for this column.</summary>
        public ExpectedColumn WithSemanticName(string semanticName) =>
            this with { SemanticName = semanticName };

        // === Matching Methods ===

        /// <summary>
        /// Check if a column name matches this expected column.
        /// Matches primary name, semantic name, or any alternative names (case-insensitive).
        /// </summary>
        public bool MatchesName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            var normalized = columnName.Trim();

            if (Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(SemanticName) &&
                SemanticName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (AlternativeNames != null)
            {
                foreach (var alt in AlternativeNames)
                {
                    if (alt.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if position requirement is satisfied.
        /// Returns true if Position is -1 (flexible) or matches actual position.
        /// </summary>
        public bool MatchesPosition(int actualPosition)
        {
            return Position < 0 || Position == actualPosition;
        }
    }
}
