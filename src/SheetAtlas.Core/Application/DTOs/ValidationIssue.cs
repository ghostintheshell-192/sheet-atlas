using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Represents a single validation issue found during template validation.
    /// Includes location, severity, and contextual information.
    /// </summary>
    public sealed record ValidationIssue
    {
        /// <summary>Severity of this validation issue.</summary>
        public ValidationSeverity Severity { get; init; }

        /// <summary>Issue type/code for categorization.</summary>
        public ValidationIssueType IssueType { get; init; }

        /// <summary>Human-readable description of the issue.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Column name where issue was found (null for sheet-level issues).</summary>
        public string? ColumnName { get; init; }

        /// <summary>Column index where issue was found (null for sheet-level issues).</summary>
        public int? ColumnIndex { get; init; }

        /// <summary>Row index where issue was found (null for column/sheet-level issues).</summary>
        public int? RowIndex { get; init; }

        /// <summary>Cell reference in Excel format (e.g., "A5") if applicable.</summary>
        public string? CellReference { get; init; }

        /// <summary>The actual value that caused the issue.</summary>
        public string? ActualValue { get; init; }

        /// <summary>The expected value or pattern.</summary>
        public string? ExpectedValue { get; init; }

        /// <summary>Name of the validation rule that was violated.</summary>
        public string? RuleName { get; init; }

        private ValidationIssue() { }

        // === Factory Methods ===

        /// <summary>Column is missing from the file.</summary>
        public static ValidationIssue MissingColumn(string columnName, bool isRequired) =>
            new()
            {
                Severity = isRequired ? ValidationSeverity.Error : ValidationSeverity.Warning,
                IssueType = ValidationIssueType.MissingColumn,
                ColumnName = columnName,
                Message = isRequired
                    ? $"Required column '{columnName}' is missing"
                    : $"Optional column '{columnName}' is missing"
            };

        /// <summary>Column is in unexpected position.</summary>
        public static ValidationIssue WrongPosition(string columnName, int expectedPosition, int actualPosition) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.WrongPosition,
                ColumnName = columnName,
                ColumnIndex = actualPosition,
                Message = $"Column '{columnName}' is at position {actualPosition}, expected at position {expectedPosition}",
                ExpectedValue = expectedPosition.ToString(),
                ActualValue = actualPosition.ToString()
            };

        /// <summary>Column has unexpected data type.</summary>
        public static ValidationIssue TypeMismatch(string columnName, int columnIndex, DataType expected, DataType actual, double confidence) =>
            new()
            {
                Severity = ValidationSeverity.Error,
                IssueType = ValidationIssueType.TypeMismatch,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' has type {actual} (confidence {confidence:P0}), expected {expected}",
                ExpectedValue = expected.ToString(),
                ActualValue = actual.ToString()
            };

        /// <summary>Column has low type confidence.</summary>
        public static ValidationIssue LowConfidence(string columnName, int columnIndex, double actual, double required) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.LowConfidence,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' has type confidence {actual:P0}, below threshold {required:P0}",
                ExpectedValue = $">= {required:P0}",
                ActualValue = $"{actual:P0}"
            };

        /// <summary>Currency code mismatch.</summary>
        public static ValidationIssue CurrencyMismatch(string columnName, int columnIndex, string expected, string? actual) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.CurrencyMismatch,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' has currency {actual ?? "unknown"}, expected {expected}",
                ExpectedValue = expected,
                ActualValue = actual
            };

        /// <summary>Extra column not defined in template.</summary>
        public static ValidationIssue ExtraColumn(string columnName, int columnIndex) =>
            new()
            {
                Severity = ValidationSeverity.Info,
                IssueType = ValidationIssueType.ExtraColumn,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' is not defined in template"
            };

        /// <summary>Sheet name doesn't match expected.</summary>
        public static ValidationIssue SheetNameMismatch(string expected, string actual) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.SheetNameMismatch,
                Message = $"Sheet name is '{actual}', expected '{expected}'",
                ExpectedValue = expected,
                ActualValue = actual
            };

        /// <summary>Data row count outside expected range.</summary>
        public static ValidationIssue RowCountOutOfRange(int actual, int min, int max) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.RowCountOutOfRange,
                Message = max > 0
                    ? $"File has {actual} data rows, expected {min}-{max}"
                    : $"File has {actual} data rows, expected at least {min}",
                ActualValue = actual.ToString(),
                ExpectedValue = max > 0 ? $"{min}-{max}" : $">= {min}"
            };

        /// <summary>Validation rule violation in a specific cell.</summary>
        public static ValidationIssue RuleViolation(
            string columnName,
            int columnIndex,
            int rowIndex,
            string cellRef,
            string ruleName,
            string message,
            string? actualValue,
            ValidationSeverity severity) =>
            new()
            {
                Severity = severity,
                IssueType = ValidationIssueType.RuleViolation,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                RowIndex = rowIndex,
                CellReference = cellRef,
                RuleName = ruleName,
                Message = message,
                ActualValue = actualValue
            };

        /// <summary>Empty column (no data values).</summary>
        public static ValidationIssue EmptyColumn(string columnName, int columnIndex) =>
            new()
            {
                Severity = ValidationSeverity.Error,
                IssueType = ValidationIssueType.EmptyColumn,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' has no data values"
            };

        /// <summary>Duplicate values in a unique column.</summary>
        public static ValidationIssue DuplicateValue(string columnName, int columnIndex, string value, int count) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.DuplicateValue,
                ColumnName = columnName,
                ColumnIndex = columnIndex,
                Message = $"Column '{columnName}' has duplicate value '{value}' ({count} occurrences)",
                ActualValue = value
            };

        /// <summary>Header row count mismatch.</summary>
        public static ValidationIssue HeaderRowMismatch(int expected, int detected) =>
            new()
            {
                Severity = ValidationSeverity.Warning,
                IssueType = ValidationIssueType.HeaderRowMismatch,
                Message = $"Detected {detected} header row(s), template expects {expected}",
                ExpectedValue = expected.ToString(),
                ActualValue = detected.ToString()
            };

        /// <summary>Generic critical error (e.g., validation failed to run).</summary>
        public static ValidationIssue CriticalError(string message) =>
            new()
            {
                Severity = ValidationSeverity.Critical,
                IssueType = ValidationIssueType.RuleViolation,
                Message = message
            };

        // === Utility ===

        /// <summary>Is this a critical or error level issue?</summary>
        public bool IsError => Severity >= ValidationSeverity.Error;

        /// <summary>Is this an informational or warning level issue?</summary>
        public bool IsWarning => Severity == ValidationSeverity.Warning;

        /// <summary>Get location description for display.</summary>
        public string LocationDescription
        {
            get
            {
                if (!string.IsNullOrEmpty(CellReference))
                    return $"Cell {CellReference}";
                if (ColumnName != null && RowIndex.HasValue)
                    return $"Column '{ColumnName}', Row {RowIndex + 1}";
                if (ColumnName != null)
                    return $"Column '{ColumnName}'";
                return "Sheet";
            }
        }
    }

    /// <summary>
    /// Types of validation issues that can be detected.
    /// </summary>
    public enum ValidationIssueType
    {
        /// <summary>Required column is missing from file.</summary>
        MissingColumn,

        /// <summary>Column is in wrong position.</summary>
        WrongPosition,

        /// <summary>Column data type doesn't match expected.</summary>
        TypeMismatch,

        /// <summary>Column type confidence is below threshold.</summary>
        LowConfidence,

        /// <summary>Currency code doesn't match expected.</summary>
        CurrencyMismatch,

        /// <summary>Extra column not in template.</summary>
        ExtraColumn,

        /// <summary>Sheet name doesn't match.</summary>
        SheetNameMismatch,

        /// <summary>Row count outside expected range.</summary>
        RowCountOutOfRange,

        /// <summary>Validation rule was violated.</summary>
        RuleViolation,

        /// <summary>Column has no data values.</summary>
        EmptyColumn,

        /// <summary>Duplicate value in unique column.</summary>
        DuplicateValue,

        /// <summary>Header row count mismatch.</summary>
        HeaderRowMismatch
    }
}
