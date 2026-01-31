namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Groups headers by semantic name, merging columns that map to the same name.
    /// </summary>
    public interface IHeaderGroupingService
    {
        /// <summary>
        /// Group headers by their semantic name (or original name if no mapping).
        /// </summary>
        /// <param name="headers">All headers to group</param>
        /// <param name="resolver">Resolver for semantic names (optional, uses original names if null)</param>
        /// <param name="includedColumns">Filter to include only these columns (optional, includes all if null)</param>
        /// <returns>List of header groups with display name and original headers</returns>
        IReadOnlyList<HeaderGroup> GroupHeaders(
            IReadOnlyList<string> headers,
            IHeaderResolver? resolver = null,
            IEnumerable<string>? includedColumns = null);
    }

    /// <summary>
    /// Represents a group of original headers that map to the same semantic display name.
    /// </summary>
    /// <param name="DisplayName">The semantic name to display (or original name if no mapping)</param>
    /// <param name="OriginalHeaders">List of original header names that map to this display name</param>
    public sealed record HeaderGroup(
        string DisplayName,
        IReadOnlyList<string> OriginalHeaders);
}
