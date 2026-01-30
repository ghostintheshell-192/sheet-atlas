namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Resolves semantic names for column headers.
    /// Provides unified interface for different resolution sources
    /// (ColumnLink, Template, Dictionary).
    /// </summary>
    public interface IHeaderResolver
    {
        /// <summary>
        /// Get semantic name for a header, or null if no mapping exists.
        /// </summary>
        /// <param name="originalHeader">The original header name from the file</param>
        /// <returns>Semantic name if mapping exists, otherwise null</returns>
        string? ResolveSemanticName(string originalHeader);
    }
}
