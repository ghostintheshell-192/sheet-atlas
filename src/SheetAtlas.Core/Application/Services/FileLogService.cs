using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Json;
using SheetAtlas.Core.Shared.Helpers;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Manages structured logging of Excel file load attempts to JSON files
    /// Each Excel file gets its own folder with chronological JSON logs
    /// </summary>
    public class FileLogService : IFileLogService
    {
        private readonly ILogger<FileLogService> _logger;
        private readonly string _logRootDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        // Constants
        private const int DefaultRetentionDays = 30;

        public FileLogService(ILogger<FileLogService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Setup log directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logRootDirectory = Path.Combine(appDataPath, "SheetAtlas", "Logs", "Files");

            // JSON serialization options
            // Use options from source-generated context and add custom converters
            _jsonOptions = new JsonSerializerOptions(AppJsonContext.Default.Options)
            {
                Converters =
                {
                    new ExcelErrorJsonConverter()
                },
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                MaxDepth = 64
            };

            // Ensure root directory exists
            try
            {
                Directory.CreateDirectory(_logRootDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create file log root directory: {Directory}", _logRootDirectory);
            }
        }

        public async Task SaveFileLogAsync(FileLogEntry logEntry)
        {
            ArgumentNullException.ThrowIfNull(logEntry);

            if (logEntry.File == null || string.IsNullOrWhiteSpace(logEntry.File.OriginalPath))
                throw new ArgumentException("Log entry must have valid file info", nameof(logEntry));

            try
            {
                // Generate folder and file names
                var folderName = FilePathHelper.GenerateLogFolderName(logEntry.File.OriginalPath);
                var fileName = FilePathHelper.GenerateLogFileName(logEntry.LoadAttempt.Timestamp);
                var folderPath = Path.Combine(_logRootDirectory, folderName);
                var filePath = Path.Combine(folderPath, fileName);

                // Ensure folder exists
                Directory.CreateDirectory(folderPath);

                // Serialize to JSON
                var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

                // Atomic write: temp file + rename
                var tempFilePath = $"{filePath}.tmp";
                await File.WriteAllTextAsync(tempFilePath, json);
                File.Move(tempFilePath, filePath, overwrite: true);

                _logger.LogDebug("File log saved: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file log for {FilePath}", logEntry.File.OriginalPath);
                // Don't throw - logging should not crash the app
            }
        }

        public async Task<List<FileLogEntry>> GetFileLogHistoryAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var logs = new List<FileLogEntry>();

            try
            {
                var folderName = FilePathHelper.GenerateLogFolderName(filePath);
                var folderPath = Path.Combine(_logRootDirectory, folderName);

                if (!Directory.Exists(folderPath))
                {
                    _logger.LogDebug("No log folder found for {FilePath}", filePath);
                    return logs;
                }

                // Get all JSON files sorted by name (which is timestamp-based)
                var jsonFiles = Directory.GetFiles(folderPath, "*.json")
                    .OrderByDescending(f => f) // Newest first
                    .ToList();

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(jsonFile);
                        var logEntry = JsonSerializer.Deserialize<FileLogEntry>(json, _jsonOptions);

                        if (logEntry != null)
                        {
                            logs.Add(logEntry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read log file: {FilePath}", jsonFile);
                        // Continue with other files
                    }
                }

                _logger.LogDebug("Loaded {Count} log entries for {FilePath}", logs.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load file log history for {FilePath}", filePath);
            }

            return logs;
        }

        public async Task<FileLogEntry?> GetLatestFileLogAsync(string filePath)
        {
            var history = await GetFileLogHistoryAsync(filePath);
            return history.FirstOrDefault(); // Already sorted newest first
        }

        public async Task DeleteFileLogsAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            try
            {
                var folderName = FilePathHelper.GenerateLogFolderName(filePath);
                var folderPath = Path.Combine(_logRootDirectory, folderName);

                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                    _logger.LogInformation("Deleted log folder for {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file logs for {FilePath}", filePath);
            }

            await Task.CompletedTask;
        }

        public async Task CleanupOldLogsAsync(int retentionDays)
        {
            if (retentionDays <= 0)
            {
                _logger.LogDebug("Cleanup skipped: retention days is {Days}", retentionDays);
                return;
            }

            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var deletedCount = 0;

                if (!Directory.Exists(_logRootDirectory))
                    return;

                // Iterate through all file folders
                var fileFolders = Directory.GetDirectories(_logRootDirectory);

                foreach (var folder in fileFolders)
                {
                    try
                    {
                        var jsonFiles = Directory.GetFiles(folder, "*.json");

                        foreach (var jsonFile in jsonFiles)
                        {
                            var fileInfo = new FileInfo(jsonFile);

                            if (fileInfo.LastWriteTime < cutoffDate)
                            {
                                File.Delete(jsonFile);
                                deletedCount++;
                                _logger.LogDebug("Deleted old log file: {FilePath}", jsonFile);
                            }
                        }

                        // Delete folder if empty
                        if (!Directory.EnumerateFileSystemEntries(folder).Any())
                        {
                            Directory.Delete(folder);
                            _logger.LogDebug("Deleted empty folder: {FolderPath}", folder);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup folder: {Folder}", folder);
                        // Continue with other folders
                    }
                }

                _logger.LogInformation("Cleanup completed: deleted {Count} old log files (retention: {Days} days)",
                    deletedCount, retentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old logs");
            }

            await Task.CompletedTask;
        }

        public string GetLogRootDirectory()
        {
            return _logRootDirectory;
        }
    }
}
