namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Complete cell information: typed value + optional metadata.
    /// Most cells (90-95%) have only value for memory efficiency.
    /// Metadata added on-demand for validation, cleaning, formulas, styles.
    /// Memory: 24 bytes per cell (16 SACellValue + 8 reference pointer).
    /// </summary>
    public readonly struct SACellData : IEquatable<SACellData>
    {
        private readonly SACellValue _value;
        private readonly CellMetadata? _metadata;

        public SACellData(SACellValue value, CellMetadata? metadata = null)
        {
            _value = value;
            _metadata = metadata;
        }

        /// <summary>
        /// Cell value (always present). Fast access, no allocation.
        /// </summary>
        public SACellValue Value => _value;

        /// <summary>
        /// Optional metadata (validation, cleaning, formulas, styles).
        /// Null for most cells (memory efficient).
        /// </summary>
        public CellMetadata? Metadata => _metadata;

        /// <summary>
        /// Check if cell has metadata without accessing it.
        /// </summary>
        public bool HasMetadata => _metadata != null;

        /// <summary>
        /// Get effective value: cleaned value if available, otherwise original.
        /// </summary>
        public SACellValue EffectiveValue => _metadata?.CleanedValue ?? _value;

        public bool Equals(SACellData other)
        {
            // Compare by value only (metadata is auxiliary information)
            return _value.Equals(other._value);
        }

        public override bool Equals(object? obj) => obj is SACellData other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(SACellData left, SACellData right) => left.Equals(right);
        public static bool operator !=(SACellData left, SACellData right) => !left.Equals(right);

        public override string ToString() => EffectiveValue.ToString();
    }

    /// <summary>
    /// Optional cell metadata for validation, data cleaning, formulas, and styles.
    /// Created on-demand (~5-10% of cells), not allocated for clean simple cells.
    /// </summary>
    public class CellMetadata
    {
        // === Data Cleaning Support ===

        /// <summary>
        /// Original value before cleaning (if cleaning was applied).
        /// Preserves user input for auditing.
        /// </summary>
        public SACellValue? OriginalValue { get; set; }

        /// <summary>
        /// Cleaned/normalized value after applying cleaning rules.
        /// Used as effective value if present.
        /// </summary>
        public SACellValue? CleanedValue { get; set; }

        /// <summary>
        /// Type of data quality issue found (if any).
        /// Used for reporting and user warnings.
        /// </summary>
        public DataQualityIssue? QualityIssue { get; set; }

        // === Validation Support ===

        /// <summary>
        /// Validation rules for this cell (min/max, list, custom formula).
        /// Applied during template-based validation.
        /// </summary>
        public DataValidation? Validation { get; set; }

        // === Formula Support (Future) ===

        /// <summary>
        /// Excel formula if cell contains formula (e.g., "=SUM(A1:A10)").
        /// Value stores computed result.
        /// </summary>
        public string? Formula { get; set; }

        // === Style Support (Future) ===

        /// <summary>
        /// Cell style (colors, fonts, borders).
        /// Shared between cells with same style (flyweight pattern).
        /// </summary>
        public CellStyle? Style { get; set; }

        // === Foundation Layer Support ===

        /// <summary>
        /// Currency context for numeric cells (from parent column analysis).
        /// Used during normalization for proper currency parsing.
        /// </summary>
        public CurrencyInfo? Currency { get; set; }

        /// <summary>
        /// Detected data type after normalization (cached for performance).
        /// Faster than re-analyzing on each comparison.
        /// </summary>
        public DataType? DetectedType { get; set; }

        /// <summary>
        /// Excel number format string (e.g., "mm/dd/yyyy", "[$€-407] #,##0.00").
        /// Used by CurrencyDetector and type inference for accurate analysis.
        /// </summary>
        public string? NumberFormat { get; set; }

        // === Extensibility ===

        /// <summary>
        /// Custom metadata for user-defined extensions.
        /// No breaking changes when adding new features.
        /// </summary>
        public Dictionary<string, object>? CustomData { get; set; }
    }

    /// <summary>
    /// Data quality issues detected during file load or validation.
    /// Used for reporting and user warnings about data problems.
    /// </summary>
    public enum DataQualityIssue
    {
        /// <summary>No issue detected.</summary>
        None = 0,

        /// <summary>Extra whitespace (leading/trailing spaces).</summary>
        ExtraWhitespace,

        /// <summary>Inconsistent format (e.g., "1.234,56" vs "1,234.56").</summary>
        InconsistentFormat,

        /// <summary>Invalid characters in numeric field (e.g., "42€").</summary>
        InvalidCharacters,

        /// <summary>Type mismatch (text in numeric column).</summary>
        TypeMismatch,

        /// <summary>Value out of valid range.</summary>
        OutOfRange,

        /// <summary>Duplicate value where uniqueness required.</summary>
        DuplicateValue,

        /// <summary>Missing required value.</summary>
        MissingRequired
    }

    /// <summary>
    /// Cell style information (colors, fonts, borders).
    /// Immutable record shared between cells with same style (flyweight pattern).
    /// Example: 1000 cells with red background → 1 CellStyle instance.
    /// </summary>
    public record CellStyle
    {
        public string? BackgroundColor { get; init; }
        public string? ForegroundColor { get; init; }
        public string? FontName { get; init; }
        public int? FontSize { get; init; }
        public bool IsBold { get; init; }
        public bool IsItalic { get; init; }
    }

    /// <summary>
    /// Data validation rules for cell values.
    /// Applied during template-based validation or user input.
    /// </summary>
    public record DataValidation
    {
        public ValidationType Type { get; init; }
        public object? MinValue { get; init; }
        public object? MaxValue { get; init; }
        public string? Formula { get; init; }
        public List<string>? AllowedValues { get; init; }  // For list validation
    }

    /// <summary>
    /// Type of validation to apply to cell values.
    /// </summary>
    public enum ValidationType
    {
        None = 0,
        WholeNumber,
        Decimal,
        List,
        Date,
        Custom
    }
}
