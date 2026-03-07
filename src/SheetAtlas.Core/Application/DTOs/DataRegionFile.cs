using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Root DTO for regions.json persistence. See ADR-011.
    /// </summary>
    public class DataRegionFile
    {
        public int Version { get; init; } = 1;
        public DateTime LastModified { get; init; }
        public Dictionary<string, SheetRegionsDto> Sheets { get; init; } = new();
    }

    /// <summary>
    /// Per-sheet region collection within DataRegionFile.
    /// </summary>
    public class SheetRegionsDto
    {
        public Dictionary<string, DataRegion> Regions { get; init; } = new();
    }
}
