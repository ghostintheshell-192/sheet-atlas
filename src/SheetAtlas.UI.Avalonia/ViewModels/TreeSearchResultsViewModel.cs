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

    public IReadOnlyList<SearchResultItem> SelectedItems => _cachedSelectedItems;
    public int SelectedCount => _cachedSelectedCount;
    public bool CanCompareRows => _cachedSelectedCount >= 2;

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

        var existing = SearchHistory.FirstOrDefault(s => s.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

        var selectionStateMap = new Dictionary<SearchResult, bool>();
        var fileExpansionMap = new Dictionary<string, bool>();
        var sheetExpansionMap = new Dictionary<string, bool>();
        bool wasSearchExpanded = true;

        if (existing != null)
        {
            wasSearchExpanded = existing.IsExpanded;
            foreach (var fileGroup in existing.FileGroups)
            {
                fileExpansionMap[fileGroup.FileName] = fileGroup.IsExpanded;

                foreach (var sheetGroup in fileGroup.SheetGroups)
                {
                    var sheetKey = $"{fileGroup.FileName}_{sheetGroup.SheetName}";
                    sheetExpansionMap[sheetKey] = sheetGroup.IsExpanded;

                    foreach (var item in sheetGroup.Results)
                    {
                        selectionStateMap[item.Result] = item.IsSelected;
                    }
                }
            }
            CleanupSearchItem(existing);
            SearchHistory.Remove(existing);
        }

        var searchItem = new SearchHistoryItem(query, results);

        searchItem.IsExpanded = wasSearchExpanded;

        searchItem.SelectionChanged += OnSearchItemSelectionChanged;
        foreach (var fileGroup in searchItem.FileGroups)
        {
            foreach (var sheetGroup in fileGroup.SheetGroups)
            {
                sheetGroup.SetupSelectionEvents(NotifySelectionChanged);
            }
        }

        bool hasRestoredSelections = false;
        if (selectionStateMap.Count > 0 || fileExpansionMap.Count > 0 || sheetExpansionMap.Count > 0)
        {
            foreach (var fileGroup in searchItem.FileGroups)
            {
                if (fileExpansionMap.TryGetValue(fileGroup.FileName, out var fileExpanded))
                {
                    fileGroup.IsExpanded = fileExpanded;
                }

                foreach (var sheetGroup in fileGroup.SheetGroups)
                {
                    var sheetKey = $"{fileGroup.FileName}_{sheetGroup.SheetName}";
                    if (sheetExpansionMap.TryGetValue(sheetKey, out var sheetExpanded))
                    {
                        sheetGroup.IsExpanded = sheetExpanded;
                    }

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

        SearchHistory.Insert(0, searchItem);

        while (SearchHistory.Count > 5)
        {
            var oldItem = SearchHistory[SearchHistory.Count - 1];
            CleanupSearchItem(oldItem);
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }

        if (hasRestoredSelections)
        {
            NotifySelectionChanged();
        }

        _logger.LogInfo($"Added search '{query}' with {results.Count} results to history", "TreeSearchResultsViewModel");
    }

    public void ClearHistory()
    {
        foreach (var item in SearchHistory)
        {
            CleanupSearchItem(item);
        }
        SearchHistory.Clear();
        _logger.LogInfo("Cleared search history", "TreeSearchResultsViewModel");
    }

    public void RemoveSearchResultsForFile(ExcelFile file)
    {
        if (file == null)
            return;

        var searchItemsToRemove = new List<SearchHistoryItem>();

        foreach (var searchItem in SearchHistory.ToList())
        {
            var fileGroupsToRemove = searchItem.FileGroups
                .Where(fg => fg.File == file)
                .ToList();

            if (fileGroupsToRemove.Count == 0)
                continue;

            foreach (var fileGroup in fileGroupsToRemove)
            {
                fileGroup.Dispose();  // Cleanup event handlers before removal
                searchItem.FileGroups.Remove(fileGroup);
            }

            if (searchItem.FileGroups.Count == 0)
            {
                searchItemsToRemove.Add(searchItem);
            }
        }

        foreach (var item in searchItemsToRemove)
        {
            CleanupSearchItem(item);
            SearchHistory.Remove(item);
        }

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

            // Collect search terms from searches that have selected rows
            var searchTerms = SearchHistory
                .Where(sh => sh.FileGroups
                    .SelectMany(fg => fg.SheetGroups)
                    .SelectMany(sg => sg.Results)
                    .Any(r => r.IsSelected && r.CanBeCompared))
                .Select(sh => sh.Query)
                .Distinct()
                .ToList();

            var request = new RowComparisonRequest(
                selectedResults.AsReadOnly(),
                searchTerms.AsReadOnly(),
                $"Row Comparison {DateTime.Now:HH:mm:ss}");

            var comparison = _rowComparisonService.CreateRowComparison(request);

            RowComparisonCreated?.Invoke(this, comparison);

            // Clear selections after creating comparison so next comparison starts fresh
            ClearSelection();

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
        RefreshSelectionCache();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanCompareRows));
        OnPropertyChanged(nameof(SelectedItems));
        ((RelayCommand)CompareSelectedRowsCommand).RaiseCanExecuteChanged();
    }

    // Named handler for SearchHistoryItem.SelectionChanged event
    // This allows proper unsubscription (unlike anonymous lambdas)
    private void OnSearchItemSelectionChanged(object? sender, EventArgs e)
    {
        NotifySelectionChanged();
    }

    // Helper to properly cleanup a SearchHistoryItem before removal
    private void CleanupSearchItem(SearchHistoryItem item)
    {
        item.SelectionChanged -= OnSearchItemSelectionChanged;
        item.Dispose();
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
            foreach (var searchItem in SearchHistory)
            {
                CleanupSearchItem(searchItem);
            }

            SearchHistory.Clear();
        }

        _disposed = true;
    }
}
