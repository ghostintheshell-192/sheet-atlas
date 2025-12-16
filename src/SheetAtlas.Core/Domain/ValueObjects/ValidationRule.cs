using System.Text.Json.Serialization;

namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// A validation rule that can be applied to a column in an Excel template.
    /// Immutable value object with factory methods for common rules.
    /// </summary>
    public sealed record ValidationRule
    {
        /// <summary>Type of validation to perform.</summary>
        public RuleType Type { get; init; }

        /// <summary>Severity level when rule is violated.</summary>
        public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

        /// <summary>Custom error message to display on violation.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Rule parameter (interpretation depends on RuleType):
        /// - InRange: "min|max" (e.g., "0|100")
        /// - Pattern: regex pattern
        /// - MinLength/MaxLength: length as string
        /// - InList: comma-separated values
        /// - DateFormat: expected format pattern
        /// - Currency: expected currency code (e.g., "EUR")
        /// </summary>
        public string? Parameter { get; init; }

        /// <summary>
        /// Column index this rule applies to (0-based).
        /// If null, rule applies to template-level validation.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ColumnIndex { get; init; }

        /// <summary>
        /// Creates an empty validation rule.
        /// Use factory methods for convenience.
        /// </summary>
        public ValidationRule() { }

        // === Factory Methods for Common Rules ===

        /// <summary>Column must have at least one non-empty value.</summary>
        public static ValidationRule NotEmpty(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.NotEmpty, Severity = severity };

        /// <summary>All cells must have values (no blanks).</summary>
        public static ValidationRule Required(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.Required, Severity = severity };

        /// <summary>All values must be unique within the column.</summary>
        public static ValidationRule Unique(ValidationSeverity severity = ValidationSeverity.Warning) =>
            new() { Type = RuleType.Unique, Severity = severity };

        /// <summary>Values must match the expected data type.</summary>
        public static ValidationRule TypeMatch(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.TypeMatch, Severity = severity };

        /// <summary>Values must be numeric.</summary>
        public static ValidationRule Numeric(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.Numeric, Severity = severity };

        /// <summary>Values must be positive numbers (> 0).</summary>
        public static ValidationRule Positive(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.Positive, Severity = severity };

        /// <summary>Values must be non-negative numbers (>= 0).</summary>
        public static ValidationRule NonNegative(ValidationSeverity severity = ValidationSeverity.Error) =>
            new() { Type = RuleType.NonNegative, Severity = severity };

        /// <summary>Values must be within specified range (inclusive).</summary>
        public static ValidationRule InRange(decimal min, decimal max, ValidationSeverity severity = ValidationSeverity.Error) =>
            new()
            {
                Type = RuleType.InRange,
                Severity = severity,
                Parameter = $"{min}|{max}"
            };

        /// <summary>Text values must match the specified regex pattern.</summary>
        public static ValidationRule Pattern(string regex, string? errorMessage = null, ValidationSeverity severity = ValidationSeverity.Error) =>
            new()
            {
                Type = RuleType.Pattern,
                Severity = severity,
                Parameter = regex,
                ErrorMessage = errorMessage ?? $"Value must match pattern: {regex}"
            };

        /// <summary>Text values must have at least the specified length.</summary>
        public static ValidationRule MinLength(int length, ValidationSeverity severity = ValidationSeverity.Error) =>
            new()
            {
                Type = RuleType.MinLength,
                Severity = severity,
                Parameter = length.ToString()
            };

        /// <summary>Text values must not exceed the specified length.</summary>
        public static ValidationRule MaxLength(int length, ValidationSeverity severity = ValidationSeverity.Warning) =>
            new()
            {
                Type = RuleType.MaxLength,
                Severity = severity,
                Parameter = length.ToString()
            };

        /// <summary>Values must be from the specified list.</summary>
        public static ValidationRule InList(IEnumerable<string> allowedValues, ValidationSeverity severity = ValidationSeverity.Error) =>
            new()
            {
                Type = RuleType.InList,
                Severity = severity,
                Parameter = string.Join(",", allowedValues)
            };

        /// <summary>Date values must match the specified format pattern.</summary>
        public static ValidationRule DateFormat(string pattern, ValidationSeverity severity = ValidationSeverity.Warning) =>
            new()
            {
                Type = RuleType.DateFormat,
                Severity = severity,
                Parameter = pattern
            };

        /// <summary>Currency values must use the specified currency code.</summary>
        public static ValidationRule Currency(string currencyCode, ValidationSeverity severity = ValidationSeverity.Warning) =>
            new()
            {
                Type = RuleType.Currency,
                Severity = severity,
                Parameter = currencyCode.ToUpperInvariant()
            };

        // === Utility Methods ===

        /// <summary>Get the allowed values list (for InList rules).</summary>
        public IEnumerable<string> GetAllowedValues()
        {
            if (Type != RuleType.InList || string.IsNullOrEmpty(Parameter))
                return Array.Empty<string>();

            return Parameter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>Get the range bounds (for InRange rules).</summary>
        public (decimal Min, decimal Max)? GetRangeBounds()
        {
            if (Type != RuleType.InRange || string.IsNullOrEmpty(Parameter))
                return null;

            var parts = Parameter.Split('|');
            if (parts.Length != 2)
                return null;

            if (decimal.TryParse(parts[0], out var min) && decimal.TryParse(parts[1], out var max))
                return (min, max);

            return null;
        }

        /// <summary>Get the length limit (for MinLength/MaxLength rules).</summary>
        public int? GetLengthLimit()
        {
            if ((Type != RuleType.MinLength && Type != RuleType.MaxLength) || string.IsNullOrEmpty(Parameter))
                return null;

            return int.TryParse(Parameter, out var limit) ? limit : null;
        }
    }
}
