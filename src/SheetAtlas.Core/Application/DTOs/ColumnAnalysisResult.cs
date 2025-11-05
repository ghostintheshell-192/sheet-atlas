using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Result of analyzing column characteristics.
    /// Extends ColumnMetadata record with detected information.
    /// </summary>
    public record ColumnAnalysisResult
    {
        public int ColumnIndex { get; init; }
        public string ColumnName { get; init; } = string.Empty;

        /// <summary>Primary detected data type for column.</summary>
        public DataType DetectedType { get; init; }

        /// <summary>Confidence score: 0.0 - 1.0. >0.8 = strong type.</summary>
        public double TypeConfidence { get; init; }

        /// <summary>Currency info if column is currency type.</summary>
        public CurrencyInfo? Currency { get; init; }

        /// <summary>Number of data quality issues found in sample.</summary>
        public int WarningCount { get; init; }

        /// <summary>Distribution of data types in sample.</summary>
        public Dictionary<DataType, int> TypeDistribution { get; init; } = new();

        /// <summary>Sample values for preview (cleaned values).</summary>
        public IReadOnlyList<SACellValue> SampleValues { get; init; } = Array.Empty<SACellValue>();

        /// <summary>Convert to SASheetData.ColumnMetadata for storage.</summary>
        public ColumnMetadata ToMetadata() => new()
        {
            Width = null,
            IsHidden = false,
            DetectedType = DetectedType,
            TypeConfidence = TypeConfidence,
            Currency = Currency,
            QualityWarningCount = WarningCount
        };
    }
}
