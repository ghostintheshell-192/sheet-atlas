using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Managers.Search;
using SheetAtlas.UI.Avalonia.Managers.Selection;
using SheetAtlas.UI.Avalonia.Models.Search;
using SheetAtlas.Logging.Services;
using Avalonia.Threading;
using SheetAtlas.UI.Avalonia.Commands;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class SearchViewModel : ViewModelBase, IDisposable
{
    private readonly ISearchResultsManager _searchResultsManager;
    private readonly ISelectionManager _selectionManager;
    private readonly ILogService _logger;

    private string _searchQuery = string.Empty;
    private bool _caseSensitive;
    private bool _exactMatch;
    private bool _useRegexSearch;
    private bool _isDropDownOpen;
    private ObservableCollection<string> _searchSuggestions = new();
    private bool _disposed;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                // If the search query is empty or only whitespace, clear results immediately
                if (string.IsNullOrWhiteSpace(value))
                {
                    _ = SafeClearResultsAsync();
                }

                // Notify search command that CanExecute state may have changed
                SearchCommand.RaiseCanExecuteChanged();

                // Search is now triggered only by button click or Enter key
                // No automatic search on every character typed
            }
        }
    }

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            SetField(ref _caseSensitive, value);
            // Search options changed, but search will be triggered only by button click
        }
    }

    public bool ExactMatch
    {
        get => _exactMatch;
        set
        {
            SetField(ref _exactMatch, value);
            // Search options changed, but search will be triggered only by button click
        }
    }

    public bool UseRegexSearch
    {
        get => _useRegexSearch;
        set
        {
            SetField(ref _useRegexSearch, value);
            // Search options changed, but search will be triggered only by button click
        }
    }

    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        set => SetField(ref _isDropDownOpen, value);
    }

    public ObservableCollection<string> SearchSuggestions
    {
        get => _searchSuggestions;
        private set => SetField(ref _searchSuggestions, value);
    }

    // Properties exposed for UI binding (delegating to managers)
    public IReadOnlyList<SearchResult> SearchResults => _searchResultsManager.Results;
    public IReadOnlyList<IGroupedSearchResult> GroupedResults => _searchResultsManager.GroupedResults;
    public IReadOnlyList<string> Suggestions => _searchResultsManager.Suggestions;
    public IReadOnlyList<ICellOccurrence> SelectedCells => _selectionManager.SelectedCells;
    public IReadOnlyList<ISheetOccurrence> SelectedSheets => _selectionManager.SelectedSheets;

    // Search commands
    public RelayCommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ShowAllFilesCommand { get; }

    // Selection and visibility commands
    public ICommand ToggleCellSelectionCommand { get; }
    public ICommand ToggleSheetSelectionCommand { get; }
    public ICommand ToggleFileVisibilityCommand { get; }
    public ICommand ShowOnlyFileCommand { get; }
    public ICommand ClearSelectionsCommand { get; }

    public SearchViewModel(
        ISearchResultsManager searchResultsManager,
        ISelectionManager selectionManager,
        ILogService logger)
    {
        _searchResultsManager = searchResultsManager ?? throw new ArgumentNullException(nameof(searchResultsManager));
        _selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _searchResultsManager.ResultsChanged += OnResultsChanged;
        _searchResultsManager.SuggestionsChanged += OnSuggestionsChanged;
        _searchResultsManager.GroupedResultsUpdated += OnGroupedResultsUpdated;
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _selectionManager.VisibilityChanged += OnVisibilityChanged;

        // Initialize commands
        SearchCommand = new RelayCommand(async () => await PerformSearchAsync(SearchQuery), () => !string.IsNullOrWhiteSpace(SearchQuery));
        ClearSearchCommand = new RelayCommand(() => Task.Run(ClearSearch));
        ShowAllFilesCommand = new RelayCommand(() => Task.Run(() => _selectionManager.ShowAllFiles()));

        ToggleCellSelectionCommand = new RelayCommand<ICellOccurrence>(
            (ICellOccurrence cell) => _selectionManager.ToggleCellSelection(cell));
        ToggleSheetSelectionCommand = new RelayCommand<ISheetOccurrence>(
            (ISheetOccurrence sheet) => _selectionManager.ToggleSheetSelection(sheet));
        ToggleFileVisibilityCommand = new RelayCommand<IFileLoadResultViewModel>(
            (IFileLoadResultViewModel file) => _selectionManager.ToggleFileVisibility(file));
        ShowOnlyFileCommand = new RelayCommand<IFileLoadResultViewModel>(
            (IFileLoadResultViewModel file) => _selectionManager.ShowOnlyFile(file));
        ClearSelectionsCommand = new RelayCommand(() => Task.Run(() => _selectionManager.ClearSelections()));
    }

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        base.OnPropertyChanged(nameof(GroupedResults));
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        base.OnPropertyChanged(nameof(SelectedCells));
        base.OnPropertyChanged(nameof(SelectedSheets));
    }

    private void OnGroupedResultsUpdated(object? sender, GroupedResultsEventArgs e)
    {
        _selectionManager.UpdateGroupedResults(e.GroupedResults);
    }

    private void OnSuggestionsChanged(object? sender, EventArgs e)
    {
        base.OnPropertyChanged(nameof(Suggestions));
        UpdateSearchSuggestions();
    }

    private void OnResultsChanged(object? sender, EventArgs e)
    {
        base.OnPropertyChanged(nameof(SearchResults));
        base.OnPropertyChanged(nameof(GroupedResults));
    }

    public void Initialize(ReadOnlyObservableCollection<IFileLoadResultViewModel> loadedFiles)
    {
        _searchResultsManager.SetSearchableFiles(loadedFiles);
    }

    public void RemoveResultsForFile(ExcelFile file)
    {
        _searchResultsManager.RemoveResultsForFile(file);
    }

    private async Task PerformSearchAsync(string query)
    {
        // SearchResultsManager handles all error cases internally
        // No need for try-catch here
        if (string.IsNullOrWhiteSpace(query))
        {
            await _searchResultsManager.PerformSearchAsync("");
            return;
        }

        var searchOptions = new SearchOptions
        {
            CaseSensitive = CaseSensitive,
            ExactMatch = ExactMatch,
            UseRegex = UseRegexSearch
        };

        await _searchResultsManager.PerformSearchAsync(query, searchOptions);
    }

    private void ClearSearch()
    {
        // Execute on UI thread to avoid threading issues
        Dispatcher.UIThread.Post(() =>
        {
            SearchQuery = string.Empty;
            // The SearchQuery setter will automatically trigger clearing of results
            // But let's also explicitly clear to be sure
            _ = SafeClearResultsAsync();
        });
    }

    /// <summary>
    /// Safely clears search results with error handling for fire-and-forget calls.
    /// </summary>
    private async Task SafeClearResultsAsync()
    {
        try
        {
            await _searchResultsManager.PerformSearchAsync("", new SearchOptions());
        }
        catch (Exception ex)
        {
            // Log but don't crash - clearing results is not critical
            _logger.LogError("Error clearing search results", ex, "SearchViewModel");
        }
    }
    private void UpdateSearchSuggestions()
    {
        SearchSuggestions.Clear();
        foreach (var suggestion in Suggestions.Take(10)) // Limit to top 10 suggestions
        {
            SearchSuggestions.Add(suggestion);
        }
        IsDropDownOpen = SearchSuggestions.Count > 0;
    }

    public void OnTextChanged(string text)
    {
        try
        {
            // Clear suggestions if text is empty
            if (string.IsNullOrWhiteSpace(text))
            {
                SearchSuggestions.Clear();
                IsDropDownOpen = false;
                return;
            }

            // Generate suggestions based on current search text
            var potentialSuggestions = new List<string>();

            // Add suggestions from previous searches
            if (Suggestions != null)
            {
                potentialSuggestions.AddRange(Suggestions.Where(s =>
                    s != null && s.Contains(text, StringComparison.OrdinalIgnoreCase)));
            }

            // Add suggestions from file names
            if (_searchResultsManager.GroupedResults != null)
            {
                var fileNames = _searchResultsManager.GroupedResults
                    .Where(g => g?.FileOccurrences != null)
                    .SelectMany(g => g.FileOccurrences)
                    .Where(f => f?.File?.FileName != null)
                    .Select(f => f.File.FileName)
                    .Distinct()
                    .Where(name => name.Contains(text, StringComparison.OrdinalIgnoreCase))
                    .Take(5);
                potentialSuggestions.AddRange(fileNames);
            }

            SearchSuggestions.Clear();
            foreach (var suggestion in potentialSuggestions.Where(s => !string.IsNullOrEmpty(s)).Distinct().Take(10))
            {
                SearchSuggestions.Add(suggestion);
            }

            IsDropDownOpen = SearchSuggestions.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in OnTextChanged with text: '{text}'", ex, "SearchViewModel");
            SearchSuggestions.Clear();
            IsDropDownOpen = false;
        }
    }

    public void OnQuerySubmitted(string query)
    {
        IsDropDownOpen = false;
        SearchQuery = query;
        _ = PerformSearchAsync(query);
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
            // Currently, there are no managed resources to dispose
            _searchResultsManager.ResultsChanged -= OnResultsChanged;
            _searchResultsManager.SuggestionsChanged -= OnSuggestionsChanged;
            _searchResultsManager.GroupedResultsUpdated -= OnGroupedResultsUpdated;
            _selectionManager.SelectionChanged -= OnSelectionChanged;
            _selectionManager.VisibilityChanged -= OnVisibilityChanged;
        }

        _disposed = true;
    }
}
