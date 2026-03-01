using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
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

    /// <summary>
    /// Sets a provider function that returns the included column names for filtering search.
    /// When set, only columns returned by this function will be searched.
    /// </summary>
    void SetIncludedColumnsProvider(Func<IEnumerable<string>>? provider);

    /// <summary>
    /// Sets a DataRegion filter for search. When set, only cells within this region are searched.
    /// </summary>
    void SetSelectedRegion(string? filePath, string? sheetName, DataRegion? region);

    /// <summary>
    /// Sets a cross-file DataRegion filter. When set, only cells within matching regions across
    /// multiple files are searched. Mutually exclusive with single-file SetSelectedRegion.
    /// </summary>
    void SetCrossFileRegionFilter(string regionName, IReadOnlyList<RegionFilterEntry> regions);

    /// <summary>
    /// Clears the DataRegion filter.
    /// </summary>
    void ClearSelectedRegion();

    void RemoveResultsForFile(ExcelFile file);

    event EventHandler<EventArgs> ResultsChanged;
    event EventHandler<EventArgs> SuggestionsChanged;

    event EventHandler<GroupedResultsEventArgs> GroupedResultsUpdated;
}

/// <summary>
/// Represents a single file+sheet+region entry for cross-file region filtering.
/// </summary>
public record RegionFilterEntry(string FilePath, string SheetName, DataRegion Region);

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
