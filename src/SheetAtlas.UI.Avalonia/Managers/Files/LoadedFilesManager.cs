using System.Collections.ObjectModel;
using System.Reflection;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Shared.Helpers;
using SheetAtlas.Infrastructure.External;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.UI.Avalonia.ViewModels;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Managers.Files;

/// <summary>
/// Manages the collection of loaded Excel files and their lifecycle.
/// Handles loading, removal, and retry operations for failed loads.
/// </summary>
public class LoadedFilesManager : ILoadedFilesManager, IDisposable
{
    private readonly IExcelReaderService _excelReaderService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logger;
    private readonly IFileLogService _fileLogService;

    private readonly ObservableCollection<IFileLoadResultViewModel> _loadedFiles = new();
    private bool _disposed;

    // Error message constants
    private const string OutOfMemoryMessage =
        "Insufficient memory to load selected files.\n\n" +
        "Try to:\n" +
        "- Close other applications\n" +
        "- Load a lower amount of files\n" +
        "- Restart the application";
    private const string OutOfMemoryTitle = "Insufficient Memory";

    public ReadOnlyObservableCollection<IFileLoadResultViewModel> LoadedFiles { get; }

    public event EventHandler<FileLoadedEventArgs>? FileLoaded;
    public event EventHandler<FileRemovedEventArgs>? FileRemoved;
    public event EventHandler<FileLoadFailedEventArgs>? FileLoadFailed;
    public event EventHandler<FileReloadedEventArgs>? FileReloaded;

    public LoadedFilesManager(
        IExcelReaderService excelReaderService,
        IDialogService dialogService,
        ILogService logger,
        IFileLogService fileLogService)
    {
        _excelReaderService = excelReaderService ?? throw new ArgumentNullException(nameof(excelReaderService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogService = fileLogService ?? throw new ArgumentNullException(nameof(fileLogService));

        LoadedFiles = new ReadOnlyObservableCollection<IFileLoadResultViewModel>(_loadedFiles);
    }

    public async Task LoadFilesAsync(IEnumerable<string> filePaths)
    {
        if (filePaths == null || !filePaths.Any())
        {
            _logger.LogWarning("LoadFilesAsync called with null or empty file paths", "LoadedFilesManager");
            return;
        }

        _logger.LogInfo($"Loading {filePaths.Count()} files", "LoadedFilesManager");

        try
        {
            var loadedExcelFiles = await _excelReaderService.LoadFilesAsync(filePaths);

            // Process each file individually, continuing even if one fails
            var successCount = 0;
            var failureCount = 0;

            foreach (var excelFile in loadedExcelFiles)
            {
                try
                {
                    await ProcessLoadedFileAsync(excelFile);
                    successCount++;
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other files
                    _logger.LogError($"Error processing file {excelFile?.FilePath ?? "unknown"}", ex, "LoadedFilesManager");
                    failureCount++;

                    // Still try to add the file with error status if possible
                    if (excelFile != null)
                    {
                        FileLoadFailed?.Invoke(this, new FileLoadFailedEventArgs(
                            excelFile.FilePath,
                            ex));
                    }
                }
            }

            _logger.LogInfo($"File processing completed: {successCount} succeeded, {failureCount} failed", "LoadedFilesManager");
        }
        catch (OutOfMemoryException ex)
        {
            // System resource exhaustion
            _logger.LogError("Out of memory while loading files", ex, "LoadedFilesManager");

            await _dialogService.ShowErrorAsync(OutOfMemoryMessage, OutOfMemoryTitle);
        }
        catch (Exception ex)
        {
            // Unexpected errors - log and notify
            _logger.LogError($"Unexpected error loading files", ex, "LoadedFilesManager");

            await _dialogService.ShowErrorAsync(
                "Unforeseen error during file loading.\n\n" +
                $"Details: {ex.Message}\n\n" +
                "Operation cancelled.",
                "Loading Error");
        }
    }

    public void RemoveFile(IFileLoadResultViewModel? file, bool isRetry = false)
    {
        if (file == null)
        {
            _logger.LogWarning("RemoveFile called with null file", "LoadedFilesManager");
            return;
        }

        if (!_loadedFiles.Contains(file))
        {
            _logger.LogWarning($"Attempted to remove file not in collection: {file.FileName}", "LoadedFilesManager");
            return;
        }

        _loadedFiles.Remove(file);
        _logger.LogInfo($"Removed file: {file.FileName} (isRetry: {isRetry})", "LoadedFilesManager");

        FileRemoved?.Invoke(this, new FileRemovedEventArgs(file, isRetry));
    }

    public async Task RetryLoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("RetryLoadAsync called with null or empty file path", "LoadedFilesManager");
            return;
        }

        _logger.LogInfo($"Retrying file load for: {filePath}", "LoadedFilesManager");

        try
        {
            // Find existing file entry and its index
            var existingFile = _loadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            int originalIndex = -1;
            if (existingFile != null)
            {
                originalIndex = _loadedFiles.IndexOf(existingFile);
            }

            var reloadedFiles = await _excelReaderService.LoadFilesAsync([filePath]);

            // ExcelReaderService always returns a list (never null), but check if empty
            if (!reloadedFiles.Any())
            {
                _logger.LogError($"Retry failed: ExcelReaderService returned no results for {filePath}", "LoadedFilesManager");
                await _dialogService.ShowErrorAsync(
                    $"Impossible to load file.\n\n" +
                    $"File: {Path.GetFileName(filePath)}\n\n" +
                    "Reading service gave no results.",
                    "Loading Error");
                return;
            }

            foreach (var reloadedFile in reloadedFiles)
            {
                try
                {
                    // Save log before UI update to minimize flicker
                    await SaveFileLogAsync(reloadedFile);

                    if (existingFile != null)
                    {
                        RemoveFile(existingFile, isRetry: true);
                    }

                    await ProcessLoadedFileAtIndexAsync(reloadedFile, originalIndex);

                    if (reloadedFile.Status == LoadStatus.Success)
                    {
                        _logger.LogInfo($"File {reloadedFile.FilePath} reloaded successfully", "LoadedFilesManager");
                    }
                    else if (reloadedFile.Status == LoadStatus.PartialSuccess)
                    {
                        _logger.LogWarning($"File {reloadedFile.FilePath} reloaded with warnings", "LoadedFilesManager");
                    }
                    else
                    {
                        _logger.LogWarning($"File {reloadedFile.FilePath} reload failed", "LoadedFilesManager");
                    }

                    var reloadedViewModel = _loadedFiles.FirstOrDefault(f =>
                        f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                    if (reloadedViewModel != null)
                    {
                        FileReloaded?.Invoke(this, new FileReloadedEventArgs(reloadedViewModel, filePath));
                        _logger.LogInfo($"FileReloaded event triggered for: {reloadedViewModel.FileName}", "LoadedFilesManager");
                    }
                }
                catch (Exception ex)
                {

                    _logger.LogError($"Error processing reloaded file {reloadedFile?.FilePath ?? filePath}", ex, "LoadedFilesManager");

                    if (reloadedFile != null)
                    {
                        FileLoadFailed?.Invoke(this, new FileLoadFailedEventArgs(
                            reloadedFile.FilePath,
                            ex));
                    }
                }
            }
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError($"Out of memory during retry: {filePath}", ex, "LoadedFilesManager");

            await _dialogService.ShowErrorAsync(OutOfMemoryMessage, OutOfMemoryTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error during retry: {filePath}", ex, "LoadedFilesManager");

            await _dialogService.ShowErrorAsync(
                "Unforeseen error during file loading.\n\n" +
                $"Details: {ex.Message}\n\n" +
                "Operation cancelled.",
                "Loading Error");
        }
    }

    /// <summary>
    /// Processes a loaded Excel file and determines whether to add it to the collection.
    /// Respects the LoadStatus from Core to decide how to handle the file.
    /// </summary>
    private async Task ProcessLoadedFileAsync(ExcelFile excelFile)
    {
        // Check for duplicates
        if (LoadedFiles.Any(f => f.FilePath.Equals(excelFile.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogService.ShowMessageAsync(
                $"File {excelFile.FileName} is already loaded.",
                "Duplicate File");
            return;
        }

        // Respect Core's LoadStatus to determine handling strategy
        bool hasErrors = excelFile.Status != LoadStatus.Success;

        switch (excelFile.Status)
        {
            case LoadStatus.Success:
                // File loaded successfully - add to collection
                await AddFileToCollectionCore(excelFile, insertIndex: null, hasErrors: false);
                _logger.LogInfo($"File loaded successfully: {excelFile.FileName}", "LoadedFilesManager");
                break;

            case LoadStatus.PartialSuccess:
                // File loaded with warnings/errors but has usable data - add to collection
                await AddFileToCollectionCore(excelFile, insertIndex: null, hasErrors: true);
                _logger.LogWarning($"File loaded with errors: {excelFile.FileName} - {excelFile.Errors.Count} errors", "LoadedFilesManager");
                break;

            case LoadStatus.Failed:
                // File completely failed to load - add to collection so user can see error details
                await AddFileToCollectionCore(excelFile, insertIndex: null, hasErrors: true);

                _logger.LogError($"File failed to load: {excelFile.FileName} - {excelFile.Errors.Count} errors", "LoadedFilesManager");

                // Notify listeners of the failure
                TriggerFileLoadFailedEvent(excelFile);
                break;

            default:
                _logger.LogWarning($"Unknown LoadStatus: {excelFile.Status} for file {excelFile.FileName}", "LoadedFilesManager");
                break;
        }
    }

    /// <summary>
    /// Processes a loaded Excel file and inserts it at specific index (for retry scenarios)
    /// NOTE: Log saving is done by the caller (RetryFileLoadAsync) BEFORE calling this method
    /// </summary>
    private async Task ProcessLoadedFileAtIndexAsync(ExcelFile excelFile, int targetIndex)
    {
        bool hasErrors = excelFile.Status != LoadStatus.Success;

        switch (excelFile.Status)
        {
            case LoadStatus.Success:
                await AddFileToCollectionCore(excelFile, insertIndex: targetIndex, hasErrors: false, skipLogSave: true);
                _logger.LogInfo($"File reloaded successfully: {excelFile.FileName}", "LoadedFilesManager");
                break;

            case LoadStatus.PartialSuccess:
                await AddFileToCollectionCore(excelFile, insertIndex: targetIndex, hasErrors: true, skipLogSave: true);
                _logger.LogWarning($"File reloaded with errors: {excelFile.FileName} - {excelFile.Errors.Count} errors", "LoadedFilesManager");
                break;

            case LoadStatus.Failed:
                await AddFileToCollectionCore(excelFile, insertIndex: targetIndex, hasErrors: true, skipLogSave: true);
                _logger.LogError($"File reload failed: {excelFile.FileName} - {excelFile.Errors.Count} errors", "LoadedFilesManager");
                TriggerFileLoadFailedEvent(excelFile);
                break;

            default:
                _logger.LogWarning($"Unknown LoadStatus: {excelFile.Status} for file {excelFile.FileName}", "LoadedFilesManager");
                break;
        }
    }

    /// <summary>
    /// Triggers FileLoadFailed event with critical error message
    /// </summary>
    private void TriggerFileLoadFailedEvent(ExcelFile excelFile)
    {
        var criticalErrors = excelFile.Errors.Where(e => e.Level == Logging.Models.LogSeverity.Critical);
        var errorMessage = criticalErrors.Any()
            ? criticalErrors.First().Message
            : "Unknown error";

        FileLoadFailed?.Invoke(this, new FileLoadFailedEventArgs(
            excelFile.FilePath,
            new InvalidOperationException(errorMessage)));
    }

    /// <summary>
    /// Adds a file to the collection, optionally at a specific index.
    /// Used by both initial load and retry scenarios.
    /// </summary>
    /// <param name="excelFile">The Excel file to add to the collection</param>
    /// <param name="insertIndex">Optional index where to insert the file; if null or out of range, appends to end</param>
    /// <param name="hasErrors">Whether the file has any errors or warnings</param>
    /// <param name="skipLogSave">If true, skips saving the log (used when log was already saved before calling this method)</param>
    private async Task AddFileToCollectionCore(ExcelFile excelFile, int? insertIndex, bool hasErrors, bool skipLogSave = false)
    {
        var fileViewModel = new FileLoadResultViewModel(excelFile);

        if (insertIndex.HasValue && insertIndex.Value >= 0 && insertIndex.Value < _loadedFiles.Count)
        {
            _loadedFiles.Insert(insertIndex.Value, fileViewModel);
        }
        else
        {
            _loadedFiles.Add(fileViewModel);
        }

        if (!skipLogSave)
        {
            await SaveFileLogAsync(excelFile);
        }

        FileLoaded?.Invoke(this, new FileLoadedEventArgs(fileViewModel, hasErrors));
    }

    /// <summary>
    /// Saves a structured JSON log for the loaded file
    /// Runs asynchronously and does not block the UI
    /// </summary>
    private async Task SaveFileLogAsync(ExcelFile excelFile)
    {
        try
        {
            var logEntry = CreateFileLogEntry(excelFile);
            await _fileLogService.SaveFileLogAsync(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save file log for {excelFile.FileName}", ex, "LoadedFilesManager");
            // Don't throw - logging should never crash the app
        }
    }

    /// <summary>
    /// Creates a FileLogEntry from an ExcelFile
    /// </summary>
    private FileLogEntry CreateFileLogEntry(ExcelFile excelFile)
    {
        // Get file info
        FileInfo? fileInfo = null;
        try
        {
            fileInfo = new FileInfo(excelFile.FilePath);
        }
        catch
        {
            // File might not exist anymore
        }

        // Compute file hash
        string fileHash = "md5:unknown";
        try
        {
            if (fileInfo != null && fileInfo.Exists)
            {
                fileHash = FilePathHelper.ComputeFileHash(excelFile.FilePath);
            }
        }
        catch
        {
            // Hash computation failed
        }

        // Get app version
        var appVersion = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "0.0.0";

        // Create log entry
        var logEntry = new FileLogEntry
        {
            SchemaVersion = "1.0",
            File = new FileInfoDto
            {
                Name = excelFile.FileName,
                OriginalPath = excelFile.FilePath,
                SizeBytes = fileInfo?.Length ?? 0,
                Hash = fileHash,
                LastModified = fileInfo?.LastWriteTime ?? DateTime.MinValue
            },
            LoadAttempt = new LoadAttemptInfo
            {
                Timestamp = DateTime.Now,
                Status = excelFile.Status.ToString(),
                DurationMs = 0, // TODO: measure actual duration in future
                AppVersion = appVersion
            },
            Errors = excelFile.Errors.ToList(), // Convert IReadOnlyList to List
            Summary = CreateErrorSummary(excelFile.Errors),
            Extensions = null
        };

        return logEntry;
    }

    /// <summary>
    /// Creates error summary with aggregations
    /// </summary>
    private ErrorSummary CreateErrorSummary(IReadOnlyList<ExcelError> errors)
    {
        var summary = new ErrorSummary
        {
            TotalErrors = errors.Count,
            BySeverity = errors
                .GroupBy(e => e.Level.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            ByContext = errors
                .Where(e => !string.IsNullOrEmpty(e.Context))
                .GroupBy(e => e.Context!)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return summary;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose managed resources here if any
            Dispose(true);
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources here if any
            foreach (var file in _loadedFiles)
            {
                file.Dispose();
            }
            _loadedFiles.Clear();
        }
    }
}
