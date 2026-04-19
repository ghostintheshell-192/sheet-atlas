using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Managers.FileDetails;

/// <summary>
/// Coordinates file detail operations: removal, retry, cleanup. Orchestrates FilesManager, SearchViewModels, and ComparisonCoordinator.
/// </summary>
public interface IFileDetailsCoordinator
{
    /// <summary>
    /// Handles the removal of a file from the loaded files list.
    /// </summary>
    /// <param name="file">The file to remove</param>
    void HandleRemoveFromList(IFileLoadResultViewModel? file);

    /// <summary>
    /// Destructive cleanup path for a file: clears derived artifacts (search results,
    /// comparisons), disposes the underlying <c>ExcelFile</c>, removes it from the loaded
    /// files collection, and forces aggressive LOH garbage collection to reclaim memory.
    /// This is the only path that releases the ~100–500 MB held by a loaded workbook.
    /// Contrast with <c>ILoadedFilesManager.RemoveFile</c>, which is non-destructive.
    /// See ADR-015.
    /// </summary>
    /// <param name="file">The file to clean up</param>
    /// <param name="treeSearchResults">Tree search results ViewModel to clean up (nullable)</param>
    /// <param name="searchViewModel">Search ViewModel to clean up (nullable)</param>
    /// <param name="onClearSelection">Callback to clear file selection in MainWindowViewModel if needed</param>
    void HandleCleanAllData(
        IFileLoadResultViewModel? file,
        TreeSearchResultsViewModel? treeSearchResults,
        SearchViewModel? searchViewModel,
        Action<IFileLoadResultViewModel> onClearSelection);

    /// <summary>
    /// Handles the removal of a file notification.
    /// </summary>
    /// <param name="file">The file whose notification should be removed</param>
    void HandleRemoveNotification(IFileLoadResultViewModel? file);

    /// <summary>
    /// Handles the retry operation for a failed file load.
    /// </summary>
    /// <param name="file">The file to retry loading</param>
    /// <param name="onRetrySuccess">Callback to re-select the file after successful retry</param>
    /// <returns>A task representing the async retry operation</returns>
    Task HandleTryAgainAsync(
        IFileLoadResultViewModel? file,
        Action<IFileLoadResultViewModel> onRetrySuccess);
}
