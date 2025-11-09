namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Strategy for resolving merged cells.
    /// Configurable via appsettings.json FoundationLayer.MergedCells.DefaultStrategy.
    /// </summary>
    public enum MergeStrategy : byte
    {
        /// <summary>
        /// Replicate value from top-left cell to all cells in merged range.
        /// Best for search accuracy and data comparison.
        /// </summary>
        ExpandValue = 0,

        /// <summary>
        /// Keep value only in top-left cell, others remain empty.
        /// Preserves original Excel structure.
        /// </summary>
        KeepTopLeft = 1,

        /// <summary>
        /// Concatenate values if multi-row merge (for complex headers).
        /// Flattens merged content to string.
        /// </summary>
        FlattenToString = 2,

        /// <summary>
        /// Auto-detect based on context (header vs data rows).
        /// Intelligent strategy selection.
        /// </summary>
        TreatAsHeader = 3
    }

    /// <summary>
    /// Complexity level of merged cells in sheet.
    /// Used to warn user about potential data quality issues.
    /// </summary>
    public enum MergeComplexity : byte
    {
        /// <summary>
        /// Few horizontal merges (headers only). Safe to process.
        /// </summary>
        Simple = 0,

        /// <summary>
        /// Vertical merges or nested merges. Requires careful handling.
        /// </summary>
        Complex = 1,

        /// <summary>
        /// >20% cells merged or complex patterns. High risk of data loss.
        /// </summary>
        Chaos = 2
    }
}
