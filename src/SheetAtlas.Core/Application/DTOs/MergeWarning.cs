using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Warning generated during merged cell resolution.
    /// </summary>
    public record MergeWarning
    {
        /// <summary>Cell range reference (e.g., "A1:C1").</summary>
        public string RangeRef { get; init; } = string.Empty;

        /// <summary>Complexity level of this merge.</summary>
        public MergeComplexity Complexity { get; init; }

        /// <summary>Human-readable warning message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Factory for simple horizontal merge warning.</summary>
        public static MergeWarning Simple(string rangeRef, string message) =>
            new() { RangeRef = rangeRef, Complexity = MergeComplexity.Simple, Message = message };

        /// <summary>Factory for complex merge warning.</summary>
        public static MergeWarning Complex(string rangeRef, string message) =>
            new() { RangeRef = rangeRef, Complexity = MergeComplexity.Complex, Message = message };

        /// <summary>Factory for chaotic merge warning.</summary>
        public static MergeWarning Chaos(string rangeRef, string message) =>
            new() { RangeRef = rangeRef, Complexity = MergeComplexity.Chaos, Message = message };

        /// <summary>Factory for high complexity warning (>20% merged).</summary>
        public static MergeWarning HighComplexity(
            MergeComplexity complexity,
            double percentage,
            string message) =>
            new() { RangeRef = "", Complexity = complexity, Message = message };
    }
}
