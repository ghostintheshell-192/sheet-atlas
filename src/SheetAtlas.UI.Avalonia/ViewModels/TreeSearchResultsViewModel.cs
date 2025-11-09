using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Commands;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class TreeSearchResultsViewModel : ViewModelBase, IDisposable
{
    private bool _disposed = false;
    private readonly ILogService _logger;
    private readonly IRowComparisonService _rowComparisonService;
    private ObservableCollection<SearchHistoryItem> _searchHistory = new();
    private List<SearchResultItem> _cachedSelectedItems = new();
    private int _cachedSelectedCount = 0;


    public ObservableCollection<SearchHistoryItem> SearchHistory
    {
        get => _searchHistory;
        set => SetField(ref _searchHistory, value);
    }

    public ICommand ClearHistoryCommand { get; }
    public ICommand CompareSelectedRowsCommand { get; }
    public ICommand ClearSelectionCommand { get; }

    // Properties for UI binding
    public IReadOnlyList<SearchResultItem> SelectedItems => _cachedSelectedItems;
    public int SelectedCount => _cachedSelectedCount;
    public bool CanCompareRows => _cachedSelectedCount >= 2;

    // Event for notifying about row comparison creation
    public event EventHandler<RowComparison>? RowComparisonCreated;

    public TreeSearchResultsViewModel(ILogService logger, IRowComparisonService rowComparisonService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rowComparisonService = rowComparisonService ?? throw new ArgumentNullException(nameof(rowComparisonService));

        ClearHistoryCommand = new RelayCommand(() => { ClearHistory(); return Task.CompletedTask; });
        CompareSelectedRowsCommand = new RelayCommand(() => { CompareSelectedRows(); return Task.CompletedTask; }, () => CanCompareRows);
        ClearSelectionCommand = new RelayCommand(() => { ClearSelection(); return Task.CompletedTask; });

        RefreshSelectionCache();
    }

    private void RefreshSelectionCache()
    {
        _cachedSelectedItems = SearchHistory
            .SelectMany(sh => sh.FileGroups)
            .SelectMany(fg => fg.SheetGroups)
            .SelectMany(sg => sg.Results)
            .Where(item => item.IsSelected && item.CanBeCompared)
            .ToList();

        _cachedSelectedCount = _cachedSelectedItems.Count;
    }

    public void AddSearchResults(string query, IReadOnlyList<SearchResult> results)
    {
        if (string.IsNullOrWhiteSpace(query) || !results.Any())
            return;

        // Check if we already have this search in history
        var existing = SearchHistory.FirstOrDefault(s => s.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

        // Build selection state and expansion state maps from existing item BEFORE removing it
        var selectionStateMap = new Dictionary<SearchResult, bool>();
        var fileExpansionMap = new Dictionary<string, bool>();
        var sheetExpansionMap = new Dictionary<string, bool>();
        bool wasSearchExpanded = true; // Default to expanded for new searches

        if (existing != null)
        {
            wasSearchExpanded = existing.IsExpanded;
            foreach (var fileGroup in existing.FileGroups)
            {
                // Save file expansion state
                fileExpansionMap[fileGroup.FileName] = fileGroup.IsExpanded;

                foreach (var sheetGroup in fileGroup.SheetGroups)
                {
                    // Save sheet expansion state (use fileName_sheetName as key for uniqueness)
                    var sheetKey = $"{fileGroup.FileName}_{sheetGroup.SheetName}";
                    sheetExpansionMap[sheetKey] = sheetGroup.IsExpanded;

                    foreach (var item in sheetGroup.Results)
                    {
                        selectionStateMap[item.Result] = item.IsSelected;
                    }
                }
            }
            SearchHistory.Remove(existing);
        }

        // Create new search history item
        var searchItem = new SearchHistoryItem(query, results);

        // Restore expansion state from previous version
        searchItem.IsExpanded = wasSearchExpanded;

        // Setup selection change events BEFORE restoring selection state
        searchItem.SelectionChanged += (s, e) => NotifySelectionChanged();
        foreach (var fileGroup in searchItem.FileGroups)
        {
            foreach (var sheetGroup in fileGroup.SheetGroups)
            {
                sheetGroup.SetupSelectionEvents(NotifySelectionChanged);
            }
        }

        // Restore expansion and selection state from previous version
        bool hasRestoredSelections = false;
        if (selectionStateMap.Count > 0 || fileExpansionMap.Count > 0 || sheetExpansionMap.Count > 0)
        {
            foreach (var fileGroup in searchItem.FileGroups)
            {
                // Restore file expansion state
                if (fileExpansionMap.TryGetValue(fileGroup.FileName, out var fileExpanded))
                {
                    fileGroup.IsExpanded = fileExpanded;
                }

                foreach (var sheetGroup in fileGroup.SheetGroups)
                {
                    // Restore sheet expansion state
                    var sheetKey = $"{fileGroup.FileName}_{sheetGroup.SheetName}";
                    if (sheetExpansionMap.TryGetValue(sheetKey, out var sheetExpanded))
                    {
                        sheetGroup.IsExpanded = sheetExpanded;
                    }

                    // Restore selection state
                    foreach (var item in sheetGroup.Results)
                    {
                        if (selectionStateMap.TryGetValue(item.Result, out var wasSelected) && wasSelected)
                        {
                            item.IsSelected = true;
                            hasRestoredSelections = true;
                        }
                    }
                }
            }
        }

        // Add to top of list
        SearchHistory.Insert(0, searchItem);

        // Keep only last 5 searches
        while (SearchHistory.Count > 5)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }

        // If we restored selections, notify the UI to update counters and button states
        if (hasRestoredSelections)
        {
            NotifySelectionChanged();
        }

        _logger.LogInfo($"Added search '{query}' with {results.Count} results to history", "TreeSearchResultsViewModel");
    }

    public void ClearHistory()
    {
        SearchHistory.Clear();
        _logger.LogInfo("Cleared search history", "TreeSearchResultsViewModel");
    }

    public void RemoveSearchResultsForFile(ExcelFile file)
    {
        if (file == null)
            return;

        // Strategy: Modify existing SearchHistoryItems in place by removing FileResultGroups
        // This preserves UI bindings and selection state for items that remain

        var searchItemsToRemove = new List<SearchHistoryItem>();

        foreach (var searchItem in SearchHistory.ToList())
        {
            // Find file groups that reference the removed file
            var fileGroupsToRemove = searchItem.FileGroups
                .Where(fg => fg.File == file)
                .ToList();

            if (!fileGroupsToRemove.Any())
                continue;

            // Remove those file groups from the search item (modifies in place)
            foreach (var fileGroup in fileGroupsToRemove)
            {
                searchItem.FileGroups.Remove(fileGroup);
            }

            // If search item has no more file groups, mark it for complete removal
            if (searchItem.FileGroups.Count == 0)
            {
                searchItemsToRemove.Add(searchItem);
            }
        }

        // Remove empty search items from history
        foreach (var item in searchItemsToRemove)
        {
            SearchHistory.Remove(item);
        }

        // Notify UI that selection state may have changed (counters, button enable state)
        NotifySelectionChanged();

        _logger.LogInfo($"Removed search results for file: {file.FilePath}", "TreeSearchResultsViewModel");
    }

    public void ClearSelection()
    {
        foreach (var item in SelectedItems.ToList())
        {
            item.IsSelected = false;
        }
        NotifySelectionChanged();
        _logger.LogInfo("Cleared row selection", "TreeSearchResultsViewModel");
    }

    private void CompareSelectedRows()
    {
        try
        {
            var selectedResults = SelectedItems.Select(item => item.Result).ToList();

            if (selectedResults.Count < 2)
            {
                _logger.LogWarning("Attempted to compare rows with less than 2 selected items", "TreeSearchResultsViewModel");
                return;
            }

            var request = new RowComparisonRequest(selectedResults.AsReadOnly(),
                $"Row Comparison {DateTime.Now:HH:mm:ss}");

            var comparison = _rowComparisonService.CreateRowComparison(request);

            RowComparisonCreated?.Invoke(this, comparison);

            _logger.LogInfo($"Created row comparison with {comparison.Rows.Count} rows", "TreeSearchResultsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create row comparison", ex, "TreeSearchResultsViewModel");
            // In a real app, you'd show an error message to the user
        }
    }

    private void NotifySelectionChanged()
    {
        RefreshSelectionCache();   // Update cached selection state
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanCompareRows));
        OnPropertyChanged(nameof(SelectedItems));
        ((RelayCommand)CompareSelectedRowsCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            // Dispose managed resources
            foreach (var searchItem in SearchHistory)
            {
                searchItem.Dispose();
            }

            SearchHistory.Clear();
        }

        _disposed = true;
    }
}
