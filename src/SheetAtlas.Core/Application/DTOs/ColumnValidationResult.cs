using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Validation result for a single column.
    /// Combines expected column definition with actual column analysis.
    /// </summary>
    public sealed record ColumnValidationResult
    {
        /// <summary>Column name from template.</summary>
        public string ExpectedName { get; init; } = string.Empty;

        /// <summary>Actual column name found in file (null if not found).</summary>
        public string? ActualName { get; init; }

        /// <summary>Expected column position from template (-1 = flexible).</summary>
        public int ExpectedPosition { get; init; } = -1;

        /// <summary>Actual column position in file (-1 if not found).</summary>
        public int ActualPosition { get; init; } = -1;

        /// <summary>Expected data type from template.</summary>
        public DataType ExpectedType { get; init; }

        /// <summary>Detected data type from column analysis.</summary>
        public DataType ActualType { get; init; }

        /// <summary>Type detection confidence (0.0 - 1.0).</summary>
        public double TypeConfidence { get; init; }

        /// <summary>Expected currency code (for currency columns).</summary>
        public string? ExpectedCurrency { get; init; }

        /// <summary>Detected currency code.</summary>
        public string? ActualCurrency { get; init; }

        /// <summary>Is this column required in template?</summary>
        public bool IsRequired { get; init; }

        /// <summary>Was the column found in the file?</summary>
        public bool Found { get; init; }

        /// <summary>Did the column pass all validation checks?</summary>
        public bool Passed { get; init; }

        /// <summary>List of validation issues for this column.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

        /// <summary>Number of data quality warnings from column analysis.</summary>
        public int QualityWarningCount { get; init; }

        private ColumnValidationResult() { }

        // === Factory Methods ===

        /// <summary>Create result for a found and valid column.</summary>
        public static ColumnValidationResult Valid(
            ExpectedColumn expected,
            int actualPosition,
            ColumnAnalysisResult analysis) =>
            new()
            {
                ExpectedName = expected.Name,
                ActualName = analysis.ColumnName,
                ExpectedPosition = expected.Position,
                ActualPosition = actualPosition,
                ExpectedType = expected.ExpectedType,
                ActualType = analysis.DetectedType,
                TypeConfidence = analysis.TypeConfidence,
                ExpectedCurrency = expected.ExpectedCurrency,
                ActualCurrency = analysis.Currency?.Code,
                IsRequired = expected.IsRequired,
                Found = true,
                Passed = true,
                Issues = Array.Empty<ValidationIssue>(),
                QualityWarningCount = analysis.WarningCount
            };

        /// <summary>Create result for a found column with issues.</summary>
        public static ColumnValidationResult WithIssues(
            ExpectedColumn expected,
            int actualPosition,
            ColumnAnalysisResult analysis,
            IReadOnlyList<ValidationIssue> issues) =>
            new()
            {
                ExpectedName = expected.Name,
                ActualName = analysis.ColumnName,
                ExpectedPosition = expected.Position,
                ActualPosition = actualPosition,
                ExpectedType = expected.ExpectedType,
                ActualType = analysis.DetectedType,
                TypeConfidence = analysis.TypeConfidence,
                ExpectedCurrency = expected.ExpectedCurrency,
                ActualCurrency = analysis.Currency?.Code,
                IsRequired = expected.IsRequired,
                Found = true,
                Passed = !issues.Any(i => i.IsError),
                Issues = issues,
                QualityWarningCount = analysis.WarningCount
            };

        /// <summary>Create result for a missing column.</summary>
        public static ColumnValidationResult Missing(ExpectedColumn expected) =>
            new()
            {
                ExpectedName = expected.Name,
                ActualName = null,
                ExpectedPosition = expected.Position,
                ActualPosition = -1,
                ExpectedType = expected.ExpectedType,
                ActualType = DataType.Unknown,
                TypeConfidence = 0,
                ExpectedCurrency = expected.ExpectedCurrency,
                ActualCurrency = null,
                IsRequired = expected.IsRequired,
                Found = false,
                Passed = !expected.IsRequired,
                Issues = new[] { ValidationIssue.MissingColumn(expected.Name, expected.IsRequired) },
                QualityWarningCount = 0
            };

        /// <summary>Create result for an extra column (not in template).</summary>
        public static ColumnValidationResult Extra(
            string columnName,
            int position,
            ColumnAnalysisResult analysis) =>
            new()
            {
                ExpectedName = columnName,
                ActualName = columnName,
                ExpectedPosition = -1,
                ActualPosition = position,
                ExpectedType = DataType.Unknown,
                ActualType = analysis.DetectedType,
                TypeConfidence = analysis.TypeConfidence,
                ExpectedCurrency = null,
                ActualCurrency = analysis.Currency?.Code,
                IsRequired = false,
                Found = true,
                Passed = true,
                Issues = new[] { ValidationIssue.ExtraColumn(columnName, position) },
                QualityWarningCount = analysis.WarningCount
            };

        // === Computed Properties ===

        /// <summary>Does the position match expected?</summary>
        public bool PositionMatches =>
            ExpectedPosition < 0 || ExpectedPosition == ActualPosition;

        /// <summary>Does the type match expected?</summary>
        public bool TypeMatches =>
            ExpectedType == DataType.Unknown || ExpectedType == ActualType;

        /// <summary>Does the currency match expected?</summary>
        public bool CurrencyMatches =>
            ExpectedCurrency == null ||
            (ActualCurrency != null && ExpectedCurrency.Equals(ActualCurrency, StringComparison.OrdinalIgnoreCase));

        /// <summary>Number of error-level issues.</summary>
        public int ErrorCount => Issues.Count(i => i.IsError);

        /// <summary>Number of warning-level issues.</summary>
        public int WarningCount => Issues.Count(i => i.IsWarning);

        /// <summary>Summary status for display.</summary>
        public string StatusSummary
        {
            get
            {
                if (!Found)
                    return IsRequired ? "Missing (required)" : "Missing (optional)";
                if (Passed && Issues.Count == 0)
                    return "Valid";
                if (Passed)
                    return $"Valid with {Issues.Count} warning(s)";
                return $"Invalid: {ErrorCount} error(s)";
            }
        }
    }
}
