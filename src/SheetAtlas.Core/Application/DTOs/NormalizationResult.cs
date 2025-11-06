using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Result of normalizing single cell value.
    /// Preserves original + cleaned value + quality issues.
    /// Follows Result pattern (no exceptions for business errors).
    /// </summary>
    public record NormalizationResult
    {
        /// <summary>
        /// Original value as stored in Excel (for auditing).
        /// </summary>
        public SACellValue OriginalValue { get; init; }

        /// <summary>
        /// Normalized value after cleaning (if successful).
        /// </summary>
        public SACellValue? CleanedValue { get; init; }

        /// <summary>
        /// Data type detected for value.
        /// </summary>
        public DataType DetectedType { get; init; }

        /// <summary>
        /// Quality issue found (if any).
        /// </summary>
        public DataQualityIssue QualityIssue { get; init; } = DataQualityIssue.None;

        /// <summary>
        /// Normalization successful (cleaned value is usable).
        /// True even with warnings - check QualityIssue for data quality concerns.
        /// False only when normalization completely failed (CleanedValue is null).
        /// </summary>
        public bool IsSuccess => CleanedValue != null;

        /// <summary>
        /// Optional error message if normalization failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Factory method for successful normalization.
        /// </summary>
        public static NormalizationResult Success(SACellValue original, SACellValue cleaned, DataType type) =>
            new()
            {
                OriginalValue = original,
                CleanedValue = cleaned,
                DetectedType = type,
                QualityIssue = DataQualityIssue.None
            };

        /// <summary>
        /// Factory method for successful normalization with warnings.
        /// </summary>
        public static NormalizationResult SuccessWithWarning(
            SACellValue original,
            SACellValue cleaned,
            DataType type,
            DataQualityIssue warning) =>
            new()
            {
                OriginalValue = original,
                CleanedValue = cleaned,
                DetectedType = type,
                QualityIssue = warning
            };

        /// <summary>
        /// Factory method for failed normalization.
        /// </summary>
        public static NormalizationResult Failure(
            SACellValue original,
            DataQualityIssue issue,
            string message) =>
            new()
            {
                OriginalValue = original,
                CleanedValue = null,
                DetectedType = DataType.Unknown,
                QualityIssue = issue,
                ErrorMessage = message
            };

        /// <summary>
        /// Factory method for empty/blank cells.
        /// </summary>
        public static NormalizationResult Empty =>
            new()
            {
                OriginalValue = SACellValue.Empty,
                CleanedValue = SACellValue.Empty,
                DetectedType = DataType.Unknown,
                QualityIssue = DataQualityIssue.None
            };
    }
}
