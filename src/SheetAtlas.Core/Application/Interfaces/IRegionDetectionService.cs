using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Detects DataRegion boundaries in target sheets by matching headers from a source region.
    /// Used for cross-file region application (ADR-012 Phase 2).
    /// </summary>
    public interface IRegionDetectionService
    {
        /// <summary>
        /// Detect region boundaries in a target sheet by matching source region headers.
        /// Uses case-insensitive header comparison and stops at first empty row or end of sheet.
        /// </summary>
        /// <param name="sourceRegion">The region to match against</param>
        /// <param name="sourceSheet">The sheet containing the source region</param>
        /// <param name="targetSheet">The sheet to search for matching headers</param>
        RegionDetectionResult DetectRegion(
            DataRegion sourceRegion,
            SASheetData sourceSheet,
            SASheetData targetSheet);
    }

    /// <summary>
    /// Result of a cross-file region detection attempt.
    /// </summary>
    public record RegionDetectionResult(
        bool Found,
        DataRegion? DetectedRegion,
        string? Message,
        bool WasTruncated = false);
}
