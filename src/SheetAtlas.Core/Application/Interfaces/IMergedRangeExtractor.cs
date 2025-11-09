using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

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
        /// Reports validation errors (invalid range references) to the errors collection.
        /// </summary>
        /// <param name="context">Format-specific worksheet context</param>
        /// <param name="sheetName">Sheet name for error reporting</param>
        /// <param name="errors">Error collection for reporting invalid merge ranges</param>
        /// <returns>Array of merged ranges found in the worksheet (empty if none)</returns>
        /// <remarks>
        /// Returns structural information only (StartRow, EndRow, StartCol, EndCol).
        /// Does NOT expand values - that's the responsibility of MergedCellResolver.
        /// Invalid range references are reported as warnings in the errors collection.
        /// </remarks>
        MergedRange[] ExtractMergedRanges(TContext context, string sheetName, List<ExcelError> errors);
    }
}
