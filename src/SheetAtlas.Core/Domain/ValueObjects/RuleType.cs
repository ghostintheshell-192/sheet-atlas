namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Types of validation rules that can be applied to template columns.
    /// Used by ExpectedColumn and ValidationRule for template validation.
    /// </summary>
    public enum RuleType : byte
    {
        /// <summary>Column must not be empty (at least one non-blank cell).</summary>
        NotEmpty = 1,

        /// <summary>All cells must have values (no blanks allowed).</summary>
        Required = 2,

        /// <summary>Values must be unique within the column.</summary>
        Unique = 3,

        /// <summary>Values must match expected data type.</summary>
        TypeMatch = 4,

        /// <summary>Values must match a specific date format pattern.</summary>
        DateFormat = 5,

        /// <summary>Values must be numeric.</summary>
        Numeric = 6,

        /// <summary>Values must match a currency format.</summary>
        Currency = 7,

        /// <summary>Values must be positive numbers.</summary>
        Positive = 8,

        /// <summary>Values must be non-negative numbers (>= 0).</summary>
        NonNegative = 9,

        /// <summary>Values must be within a specified range.</summary>
        InRange = 10,

        /// <summary>Text values must match a regex pattern.</summary>
        Pattern = 11,

        /// <summary>Text values must have minimum length.</summary>
        MinLength = 12,

        /// <summary>Text values must have maximum length.</summary>
        MaxLength = 13,

        /// <summary>Values must be from a predefined list.</summary>
        InList = 14,

        /// <summary>Custom validation with user-defined expression.</summary>
        Custom = 99
    }

    /// <summary>
    /// Severity level for validation rule violations.
    /// </summary>
    public enum ValidationSeverity : byte
    {
        /// <summary>Informational - suggestion for improvement.</summary>
        Info = 0,

        /// <summary>Warning - potential issue but not critical.</summary>
        Warning = 1,

        /// <summary>Error - rule violation that needs attention.</summary>
        Error = 2,

        /// <summary>Critical - severe issue that may cause data problems.</summary>
        Critical = 3
    }
}
