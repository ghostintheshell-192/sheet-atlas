using System.Runtime;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Managers.Comparison;
using SheetAtlas.UI.Avalonia.Managers.Files;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Managers.FileDetails;

/// <summary>
/// Coordinates file detail operations such as file removal, retry, and cleanup.
/// Orchestrates interactions between FilesManager, SearchViewModels, and ComparisonCoordinator.
/// </summary>
public class FileDetailsCoordinator : IFileDetailsCoordinator
{
    private readonly ILoadedFilesManager _filesManager;
    private readonly IRowComparisonCoordinator _comparisonCoordinator;
    private readonly ILogService _logger;
    private readonly IActivityLogService _activityLog;
    private readonly IDialogService _dialogService;

    public FileDetailsCoordinator(
        ILoadedFilesManager filesManager,
        IRowComparisonCoordinator comparisonCoordinator,
        ILogService logger,
        IActivityLogService activityLog,
        IDialogService dialogService)
    {
        _filesManager = filesManager ?? throw new ArgumentNullException(nameof(filesManager));
        _comparisonCoordinator = comparisonCoordinator ?? throw new ArgumentNullException(nameof(comparisonCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public void HandleRemoveFromList(IFileLoadResultViewModel? file)
    {
        _filesManager.RemoveFile(file);
    }

    public void HandleCleanAllData(
        IFileLoadResultViewModel? file,
        TreeSearchResultsViewModel? treeSearchResults,
        SearchViewModel? searchViewModel,
        Action<IFileLoadResultViewModel> onClearSelection)
    {
        if (file == null)
        {
            _logger.LogWarning("Clean all data requested with null file", "FileDetailsCoordinator");
            return;
        }

        _logger.LogInfo($"Clean all data requested for: {file.FileName}", "FileDetailsCoordinator");

        onClearSelection(file);

        treeSearchResults?.RemoveSearchResultsForFile(file.File!);

        searchViewModel?.RemoveResultsForFile(file.File!);

        _comparisonCoordinator.RemoveComparisonsForFile(file.File!);

        file.Dispose();

        _filesManager.RemoveFile(file);

        _logger.LogInfo($"Cleaned all data for file: {file.FileName}", "FileDetailsCoordinator");

        // AGGRESSIVE CLEANUP: Force garbage collection after file removal
        // REASON: DataTable objects (100-500 MB each) end up in Large Object Heap (LOH)
        // ISSUE: .NET GC is lazy for Gen 2/LOH - can wait minutes before collection
        // IMPACT: Without this, memory stays high even after Dispose() until GC decides to run
        Task.Run(() =>
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        });
    }

    public void HandleRemoveNotification(IFileLoadResultViewModel? file)
    {
        _filesManager.RemoveFile(file);
    }

    public async Task HandleTryAgainAsync(
        IFileLoadResultViewModel? file,
        Action<IFileLoadResultViewModel> onRetrySuccess)
    {
        if (file == null)
        {
            _logger.LogWarning("Try again requested but file is null", "FileDetailsCoordinator");
            return;
        }

        await RetryLoadFileAsync(file, onRetrySuccess);
    }

    private async Task RetryLoadFileAsync(
        IFileLoadResultViewModel file,
        Action<IFileLoadResultViewModel> onRetrySuccess)
    {
        try
        {
            _activityLog.LogInfo($"Retrying file load: {file.FileName}", "FileRetry");
            _logger.LogInfo($"Retrying file load for: {file.FilePath}", "FileDetailsCoordinator");

            var filePath = file.FilePath;

            await _filesManager.RetryLoadAsync(filePath);

            var reloadedFile = _filesManager.LoadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (reloadedFile != null)
            {
                onRetrySuccess(reloadedFile);
            }

            _activityLog.LogInfo($"Retry completed: {file.FileName}", "FileRetry");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrying file load: {file.FilePath}", ex, "FileDetailsCoordinator");
            _activityLog.LogError($"Error reloading {file.FileName}", ex, "FileRetry");

            await _dialogService.ShowErrorAsync(
                $"Unable to reload file '{file.FileName}'.\n\n" +
                $"Details: {ex.Message}",
                "Reload Error"
            );
        }
    }
}
