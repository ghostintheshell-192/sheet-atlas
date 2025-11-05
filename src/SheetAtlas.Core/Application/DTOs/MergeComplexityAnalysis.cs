using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Analysis of merged cell complexity in sheet.
    /// Used to recommend strategy and warn user.
    /// </summary>
    public record MergeComplexityAnalysis
    {
        /// <summary>Overall complexity level.</summary>
        public MergeComplexity Level { get; init; }

        /// <summary>Percentage of cells that are merged (0.0 - 100.0).</summary>
        public double MergedCellPercentage { get; init; }

        /// <summary>Recommended strategy based on analysis.</summary>
        public MergeStrategy RecommendedStrategy { get; init; }

        /// <summary>Human-readable explanation of recommendation.</summary>
        public string Explanation { get; init; } = string.Empty;

        /// <summary>Factory for simple cases (headers only).</summary>
        public static MergeComplexityAnalysis SimpleCase(double percentage) =>
            new()
            {
                Level = MergeComplexity.Simple,
                MergedCellPercentage = percentage,
                RecommendedStrategy = MergeStrategy.ExpandValue,
                Explanation = "Simple horizontal merges (headers only). Safe to expand values."
            };

        /// <summary>Factory for complex cases (vertical/nested merges).</summary>
        public static MergeComplexityAnalysis ComplexCase(double percentage) =>
            new()
            {
                Level = MergeComplexity.Complex,
                MergedCellPercentage = percentage,
                RecommendedStrategy = MergeStrategy.TreatAsHeader,
                Explanation = "Complex merge patterns detected. Recommend context-aware strategy."
            };

        /// <summary>Factory for chaotic cases (>20% merged).</summary>
        public static MergeComplexityAnalysis ChaoticCase(double percentage) =>
            new()
            {
                Level = MergeComplexity.Chaos,
                MergedCellPercentage = percentage,
                RecommendedStrategy = MergeStrategy.KeepTopLeft,
                Explanation = $"High merge density ({percentage:F1}%). Consider exporting as values first."
            };
    }
}
