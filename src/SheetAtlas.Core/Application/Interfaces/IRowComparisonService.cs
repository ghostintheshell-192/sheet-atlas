using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for comparing rows from search results. Extracts row data and column headers.
    /// </summary>
    public interface IRowComparisonService
    {
        /// <summary>
        /// Create a row comparison from selected search results
        /// </summary>
        RowComparison CreateRowComparison(RowComparisonRequest request);

        /// <summary>
        /// Extract complete row data from a search result
        /// </summary>
        ExcelRow ExtractRowFromSearchResult(SearchResult searchResult);

        /// <summary>
        /// Get column headers for a specific sheet
        /// </summary>
        IReadOnlyList<string> GetColumnHeaders(ExcelFile file, string sheetName);
    }
}
