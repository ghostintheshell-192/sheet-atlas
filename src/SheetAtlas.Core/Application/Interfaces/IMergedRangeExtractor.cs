using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Generic interface for extracting merged cell range information from various file formats.
    /// Each format (OpenXML, ODF, etc.) provides its own context type.
    /// </summary>
    /// <typeparam name="TContext">Format-specific context (e.g., WorksheetPart for OpenXML)</typeparam>
    /// <remarks>
    /// Design: Generic interface allows type-safe extraction for different file formats
    /// while maintaining a consistent abstraction for the Foundation Layer.
    /// </remarks>
    public interface IMergedRangeExtractor<in TContext>
    {
        /// <summary>
        /// Extracts merged cell range information from the given format-specific context.
        /// </summary>
        /// <param name="context">Format-specific worksheet context</param>
        /// <returns>Array of merged ranges found in the worksheet (empty if none)</returns>
        /// <remarks>
        /// Returns structural information only (StartRow, EndRow, StartCol, EndCol).
        /// Does NOT expand values - that's the responsibility of MergedCellResolver.
        /// </remarks>
        MergedRange[] ExtractMergedRanges(TContext context);
    }
}
