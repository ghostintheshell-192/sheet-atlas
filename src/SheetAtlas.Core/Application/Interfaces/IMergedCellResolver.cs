using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Resolves merged cells using configurable strategies.
    /// Handles horizontal/vertical merges, warns on complex patterns.
    /// </summary>
    /// <remarks>
    /// Strategies:
    /// - ExpandValue: Replicate value into all cells
    /// - KeepTopLeft: Only top-left cell has value (rest empty)
    /// - FlattenToString: Concatenate values if multi-row (headers)
    /// - TreatAsHeader: Merge spans header rows
    /// </remarks>
    public interface IMergedCellResolver
    {
        /// <summary>
        /// Resolves merged cells in sheet data using selected strategy.
        /// Performs synchronous in-memory operations (no I/O).
        /// </summary>
        /// <param name="sheetData">Sheet with existing MergedCells collection</param>
        /// <param name="strategy">Resolution strategy to apply</param>
        /// <param name="warningCallback">Optional callback for warnings (complexity level)</param>
        /// <returns>Modified sheet with merged cells resolved; original unmodified</returns>
        /// <remarks>
        /// Returns new SASheetData, original unmodified (immutable pattern).
        /// If >20% cells merged, warns about potential data issues.
        /// </remarks>
        SASheetData ResolveMergedCells(
            SASheetData sheetData,
            MergeStrategy strategy = MergeStrategy.ExpandValue,
            Action<MergeWarning>? warningCallback = null);

        /// <summary>
        /// Analyzes merge complexity to recommend strategy.
        /// </summary>
        /// <param name="mergedCells">Merged ranges in sheet</param>
        /// <returns>Recommended strategy and complexity assessment</returns>
        MergeComplexityAnalysis AnalyzeMergeComplexity(
            IReadOnlyDictionary<string, MergedRange> mergedCells);
    }
}
