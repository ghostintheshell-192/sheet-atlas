using System.Text.Json;
using Microsoft.Extensions.Logging;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Json;
using SheetAtlas.Core.Shared.Helpers;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Persists DataRegion definitions as JSON files.
    /// Storage: {LocalApplicationData}/SheetAtlas/DataRegions/{folder}/regions.json
    /// Follows FileLogService pattern for folder naming and atomic writes.
    /// </summary>
    public class DataRegionPersistenceService : IDataRegionPersistenceService
    {
        private readonly ILogger<DataRegionPersistenceService> _logger;
        private readonly string _storageRoot;

        private const string RegionsFileName = "regions.json";

        public DataRegionPersistenceService(ILogger<DataRegionPersistenceService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storageRoot = Path.Combine(appDataPath, "SheetAtlas", "DataRegions");

            try
            {
                Directory.CreateDirectory(_storageRoot);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create DataRegions storage root: {Directory}", _storageRoot);
            }
        }

        public async Task SaveAsync(string excelFilePath, DataRegionFile data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (string.IsNullOrWhiteSpace(excelFilePath))
                throw new ArgumentException("Excel file path cannot be null or empty", nameof(excelFilePath));

            try
            {
                var folderPath = GetFolderPath(excelFilePath);
                Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, RegionsFileName);
                var json = JsonSerializer.Serialize(data, AppJsonContext.Default.DataRegionFile);

                // Atomic write: temp file + rename
                var tempFilePath = $"{filePath}.tmp";
                await File.WriteAllTextAsync(tempFilePath, json);
                File.Move(tempFilePath, filePath, overwrite: true);

                _logger.LogDebug("DataRegion file saved: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save DataRegion file for {ExcelPath}", excelFilePath);
            }
        }

        public async Task<DataRegionFile?> LoadAsync(string excelFilePath)
        {
            if (string.IsNullOrWhiteSpace(excelFilePath))
                throw new ArgumentException("Excel file path cannot be null or empty", nameof(excelFilePath));

            try
            {
                var filePath = Path.Combine(GetFolderPath(excelFilePath), RegionsFileName);

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("No DataRegion file found for {ExcelPath}", excelFilePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.DataRegionFile);

                _logger.LogDebug("DataRegion file loaded for {ExcelPath}", excelFilePath);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load DataRegion file for {ExcelPath} - returning null", excelFilePath);
                return null;
            }
        }

        public Task DeleteAsync(string excelFilePath)
        {
            if (string.IsNullOrWhiteSpace(excelFilePath))
                throw new ArgumentException("Excel file path cannot be null or empty", nameof(excelFilePath));

            try
            {
                var folderPath = GetFolderPath(excelFilePath);

                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                    _logger.LogInformation("Deleted DataRegion folder for {ExcelPath}", excelFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete DataRegion folder for {ExcelPath}", excelFilePath);
            }

            return Task.CompletedTask;
        }

        private string GetFolderPath(string excelFilePath)
        {
            var folderName = FilePathHelper.GenerateLogFolderName(excelFilePath);
            return Path.Combine(_storageRoot, folderName);
        }
    }
}
