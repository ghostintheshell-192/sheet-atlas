namespace SheetAtlas.UI.Avalonia.Models.Search;

/// <summary>
/// Represents a group of search results with the same value
/// </summary>
public interface IGroupedSearchResult : IToggleable
{
    string Value { get; }
    bool FoundInAllFiles { get; }
    int TotalOccurrences { get; }
    bool HasVisibleDifferences { get; }
    IReadOnlyList<IFileOccurrence> FileOccurrences { get; }

    void UpdateStats(int totalFileCount);
    void UpdateVisibilityStats();

    void AddFileOccurrence(IFileOccurrence fileOccurrence);
}
