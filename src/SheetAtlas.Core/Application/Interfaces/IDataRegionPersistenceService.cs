using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Persists DataRegion definitions to/from JSON files.
    /// Each Excel file gets its own regions.json in the DataRegions folder.
    /// </summary>
    public interface IDataRegionPersistenceService
    {
        /// <summary>Save all regions for an Excel file.</summary>
        Task SaveAsync(string excelFilePath, DataRegionFile data);

        /// <summary>Load regions for an Excel file. Returns null if no file exists.</summary>
        Task<DataRegionFile?> LoadAsync(string excelFilePath);

        /// <summary>Delete stored regions for an Excel file.</summary>
        Task DeleteAsync(string excelFilePath);
    }
}
