using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Models.Search;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Managers.Search;

/// <summary>
/// Manager for search operations and results
/// </summary>
public interface ISearchResultsManager
{
    IReadOnlyList<SearchResult> Results { get; }
    IReadOnlyList<IGroupedSearchResult> GroupedResults { get; }
    IReadOnlyList<string> Suggestions { get; }

    Task PerformSearchAsync(string query, SearchOptions? options = null);
    void GenerateSuggestions(string query);

    void SetSearchableFiles(IReadOnlyCollection<IFileLoadResultViewModel> files);

    void RemoveResultsForFile(ExcelFile file);

    event EventHandler<EventArgs> ResultsChanged;
    event EventHandler<EventArgs> SuggestionsChanged;

    event EventHandler<GroupedResultsEventArgs> GroupedResultsUpdated;
}

/// <summary>
/// Event arguments for grouped results updates
/// </summary>
public class GroupedResultsEventArgs : EventArgs
{
    public IEnumerable<IGroupedSearchResult> GroupedResults { get; }

    public GroupedResultsEventArgs(IEnumerable<IGroupedSearchResult> results)
    {
        GroupedResults = results;
    }
}
