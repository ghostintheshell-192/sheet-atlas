using System.Collections.ObjectModel;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.UI.Avalonia.Models.Search;
using SheetAtlas.UI.Avalonia.ViewModels;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Managers.Search;

public class SearchResultsManager : ISearchResultsManager
{
    private readonly ISearchService _searchService;
    private readonly ILogService _logger;
    private readonly ISearchResultFactory _factory;

    private IReadOnlyCollection<IFileLoadResultViewModel> _searchableFiles;
    private Func<IEnumerable<string>>? _includedColumnsProvider;
    private readonly List<SearchResult> _results = new();
    private readonly ObservableCollection<IGroupedSearchResult> _groupedResults = new();
    private readonly ObservableCollection<string> _suggestions = new();

    public IReadOnlyList<SearchResult> Results => _results.AsReadOnly();

    public IReadOnlyList<IGroupedSearchResult> GroupedResults =>
        new ReadOnlyObservableCollection<IGroupedSearchResult>(_groupedResults);

    public IReadOnlyList<string> Suggestions =>
        new ReadOnlyObservableCollection<string>(_suggestions);

    public event EventHandler<EventArgs>? ResultsChanged;
    public event EventHandler<EventArgs>? SuggestionsChanged;
    public event EventHandler<GroupedResultsEventArgs>? GroupedResultsUpdated;

    public SearchResultsManager(
        ISearchService searchService,
        ILogService logger,
        ISearchResultFactory factory)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _searchableFiles = new List<IFileLoadResultViewModel>();
    }

    public void SetSearchableFiles(IReadOnlyCollection<IFileLoadResultViewModel> files)
    {
        _searchableFiles = files ?? throw new ArgumentNullException(nameof(files));
    }

    public void SetIncludedColumnsProvider(Func<IEnumerable<string>>? provider)
    {
        _includedColumnsProvider = provider;
    }

    public void RemoveResultsForFile(ExcelFile file)
    {
        if (file == null) return;

        // Remove all search results that reference this file
        var removedCount = _results.RemoveAll(r => r.FileName == file.FileName);

        if (removedCount > 0)
        {
            // Rebuild grouped results without the removed file's results
            GroupSearchResults(_results);

            // Notify UI of changes
            ResultsChanged?.Invoke(this, EventArgs.Empty);
            GroupedResultsUpdated?.Invoke(this, new GroupedResultsEventArgs(_groupedResults));

            _logger.LogInfo($"Removed {removedCount} search results for file: {file.FileName}", "SearchResultsManager");
        }
    }

    public async Task PerformSearchAsync(string query, SearchOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _results.Clear();
            _groupedResults.Clear();
            ResultsChanged?.Invoke(this, EventArgs.Empty);
            GroupedResultsUpdated?.Invoke(this, new GroupedResultsEventArgs(_groupedResults));
            return;
        }

        try
        {
            // Get included columns from provider (if set)
            var includedColumns = _includedColumnsProvider?.Invoke()?.ToList();

            // Debug logging for column filtering
            if (includedColumns != null)
            {
                _logger.LogInfo($"Search with column filter: {includedColumns.Count} columns included", "SearchResultsManager");
                if (includedColumns.Count <= 10)
                {
                    _logger.LogInfo($"Included columns: [{string.Join(", ", includedColumns)}]", "SearchResultsManager");
                }
            }
            else
            {
                _logger.LogInfo("Search without column filter (all columns)", "SearchResultsManager");
            }

            // Perform search asynchronously to not block UI
            var allResults = await Task.Run(() =>
            {
                var results = new List<SearchResult>();
                foreach (var fileViewModel in _searchableFiles)
                {
                    if (fileViewModel.Status == LoadStatus.Failed || fileViewModel.File == null)
                        continue;

                    var searchOptions = options ?? new SearchOptions();
                    var searchResults = _searchService.Search(fileViewModel.File, query, searchOptions, includedColumns);
                    results.AddRange(searchResults);
                }
                return results;
            });

            _results.Clear();
            _results.AddRange(allResults);

            GroupSearchResults(allResults);

            ResultsChanged?.Invoke(this, EventArgs.Empty);
            GroupedResultsUpdated?.Invoke(this, new GroupedResultsEventArgs(_groupedResults));

            _logger.LogInfo($"Search completed. Found {_results.Count} results for query: {query}", "SearchResultsManager");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error performing search for query: {query}", ex, "SearchResultsManager");
        }
    }

    public void GenerateSuggestions(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            _suggestions.Clear();
            SuggestionsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var uniqueTerms = ExtractUniqueTerms(query);

            _suggestions.Clear();
            foreach (var term in uniqueTerms.Take(10))
            {
                _suggestions.Add(term);
            }

            SuggestionsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating suggestions for query: {query}", ex, "SearchResultsManager");
        }
    }

    private IEnumerable<string> ExtractUniqueTerms(string query)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileViewModel in _searchableFiles)
        {
            if (fileViewModel.Status == LoadStatus.Failed || fileViewModel.File == null)
                continue;

            // Add filename if it contains the query
            if (fileViewModel.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                terms.Add(fileViewModel.FileName);
            }

            // Check each sheet
            foreach (var (sheetName, _) in fileViewModel.File.Sheets)
            {
                // Add sheet name if it contains the query
                if (sheetName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    terms.Add(sheetName);
                }

                // Search cells in sheet data
                var sheet = fileViewModel.File.Sheets[sheetName];
                for (int rowIndex = 0; rowIndex < sheet.RowCount; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    for (int colIndex = 0; colIndex < row.Length; colIndex++)
                    {
                        var cellValue = row[colIndex].EffectiveValue.ToString();
                        if (!string.IsNullOrEmpty(cellValue) &&
                            cellValue.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            // Add fragment around the match
                            int index = cellValue.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                            int start = Math.Max(0, index - 10);
                            int length = Math.Min(cellValue.Length - start, query.Length + 20);
                            var fragment = cellValue.Substring(start, length);

                            if (start > 0) fragment = "..." + fragment;
                            if (start + length < cellValue.Length) fragment += "...";

                            terms.Add(fragment);
                        }
                    }
                }
            }
        }

        return terms;
    }

    private void GroupSearchResults(IEnumerable<SearchResult> results)
    {
        _groupedResults.Clear();

        // Group by found value
        var groupedByValue = results
            .GroupBy(r => r.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // For each found value, create a group
        foreach (var valueGroup in groupedByValue)
        {
            var groupedResult = _factory.CreateGroupedSearchResult(valueGroup.Key);

            // Group by file
            var fileGroups = valueGroup
                .GroupBy(r => r.FileName);

            foreach (var fileGroup in fileGroups)
            {
                var fileViewModel = _searchableFiles.FirstOrDefault(f => f.FileName == fileGroup.Key);
                if (fileViewModel == null) continue;

                var fileOccurrence = _factory.CreateFileOccurrence(fileViewModel);

                // Group by sheet within each file
                var sheetGroups = fileGroup.GroupBy(r => r.SheetName);

                foreach (var sheetGroup in sheetGroups)
                {
                    // Get or create sheet occurrence
                    var sheetOccurrence = fileOccurrence.GetOrAddSheetOccurrence(sheetGroup.Key);

                    foreach (var result in sheetGroup)
                    {
                        // Add cell occurrence to sheet
                        var context = new Dictionary<string, string>
                        {
                            ["CellAddress"] = result.CellAddress,
                            ["Value"] = result.Value
                        };

                        sheetOccurrence.AddCellOccurrence(
                            result.Row,
                            result.Column,
                            result.Value,
                            context);
                    }
                }

                groupedResult.AddFileOccurrence(fileOccurrence);
            }

            // Update statistics
            groupedResult.UpdateStats(_searchableFiles.Count);
            _groupedResults.Add(groupedResult);
        }

        // Sort results: differences first, then common ones
        var sortedResults = _groupedResults
            .OrderByDescending(g => g.HasVisibleDifferences)
            .ThenBy(g => g.Value)
            .ToList();

        _groupedResults.Clear();
        foreach (var result in sortedResults)
        {
            _groupedResults.Add(result);
        }
    }
}
