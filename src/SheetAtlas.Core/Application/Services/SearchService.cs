using System.Text.RegularExpressions;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Service for searching within Excel files across sheets and cells.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Searches for a query string across all sheets in an Excel file.
        /// </summary>
        /// <param name="file">The Excel file to search in</param>
        /// <param name="query">The search query string</param>
        /// <param name="options">Optional search configuration (case sensitivity, regex, exact match)</param>
        /// <param name="includedColumns">Optional set of column names to include in search. If null, all columns are searched.</param>
        /// <returns>List of search results including file names, sheet names, and cell matches</returns>
        List<SearchResult> Search(ExcelFile file, string query, SearchOptions? options = null, IEnumerable<string>? includedColumns = null);

        /// <summary>
        /// Searches for a query string within a specific sheet of an Excel file.
        /// </summary>
        /// <param name="file">The Excel file to search in</param>
        /// <param name="sheetName">The name of the sheet to search</param>
        /// <param name="query">The search query string</param>
        /// <param name="options">Optional search configuration (case sensitivity, regex, exact match)</param>
        /// <param name="includedColumns">Optional set of column names to include in search. If null, all columns are searched.</param>
        /// <returns>List of search results found in the specified sheet</returns>
        List<SearchResult> SearchInSheet(ExcelFile file, string sheetName, string query, SearchOptions? options = null, IEnumerable<string>? includedColumns = null);
    }

    /// <summary>
    /// Implements search functionality for Excel files with support for various search modes.
    /// </summary>
    public class SearchService : ISearchService
    {
        private readonly ILogService _logger;

        public SearchService(ILogService logger)
        {
            _logger = logger;
        }

        public List<SearchResult> Search(ExcelFile file, string query, SearchOptions? options = null, IEnumerable<string>? includedColumns = null)
        {
            List<SearchResult> results = new();

            if (string.IsNullOrWhiteSpace(query))
                return results;

            if (IsMatch(file.FileName, query, options))
            {
                results.Add(new SearchResult(file, "", -1, -1, file.FileName)
                {
                    Context = { ["Type"] = "FileName" }
                });
            }

            foreach (var sheetName in file.GetSheetNames())
            {
                if (IsMatch(sheetName, query, options))
                {
                    results.Add(new SearchResult(file, sheetName, -1, -1, sheetName)
                    {
                        Context = { ["Type"] = "SheetName" }
                    });
                }

                results.AddRange(SearchInSheet(file, sheetName, query, options, includedColumns));
            }

            return results;
        }

        public List<SearchResult> SearchInSheet(ExcelFile file, string sheetName, string query, SearchOptions? options = null, IEnumerable<string>? includedColumns = null)
        {
            List<SearchResult> results = new();
            var sheet = file.GetSheet(sheetName);

            if (sheet == null) return results;

            // Create a hashset for fast column lookup (case-insensitive)
            HashSet<string>? includedColumnsSet = null;
            if (includedColumns != null)
            {
                var columnsList = includedColumns.ToList();
                includedColumnsSet = new HashSet<string>(columnsList, StringComparer.OrdinalIgnoreCase);
                _logger.LogInfo($"SearchInSheet [{file.FileName}]: filtering by {includedColumnsSet.Count} columns (from {columnsList.Count} input)", "SearchService");
            }

            // Start from HeaderRowCount to skip header rows (search only in data)
            // This supports multi-row headers (HeaderRowCount can be 1, 2, or more)
            for (int rowIndex = sheet.HeaderRowCount; rowIndex < sheet.RowCount; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                for (int colIndex = 0; colIndex < sheet.ColumnCount; colIndex++)
                {
                    var columnName = sheet.ColumnNames[colIndex];

                    // Skip columns not in the included set
                    if (includedColumnsSet != null && !includedColumnsSet.Contains(columnName))
                        continue;

                    var cellValue = row[colIndex].Value.ToString();
                    if (!string.IsNullOrEmpty(cellValue) && IsMatch(cellValue, query, options))
                    {
                        var result = new SearchResult(file, sheetName, rowIndex, colIndex, cellValue);

                        result.Context["ColumnHeader"] = columnName;

                        if (colIndex > 0)
                        {
                            result.Context["RowHeader"] = row[0].Value.ToString();
                        }

                        result.Context["CellCoordinates"] = $"R{rowIndex + 1}C{colIndex + 1}";

                        results.Add(result);
                    }
                }
            }

            return results;
        }

        private bool IsMatch(string text, string query, SearchOptions? options)
        {
            if (string.IsNullOrEmpty(text)) return false;

            options ??= new SearchOptions();

            try
            {
                if (options.UseRegex)
                {
                    var regexOptions = options.CaseSensitive ?
                        RegexOptions.None : RegexOptions.IgnoreCase;

                    return Regex.IsMatch(text, query, regexOptions);
                }

                if (options.ExactMatch)
                {
                    return options.CaseSensitive
                        ? text.Equals(query)
                        : text.Equals(query, StringComparison.OrdinalIgnoreCase);
                }

                return options.CaseSensitive
                    ? text.Contains(query)
                    : text.Contains(query, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException ex)
            {
                // Invalid regex pattern provided by user - expected error, use fallback
                _logger.LogWarning($"Invalid regex pattern '{query}': {ex.Message}", "SearchService");
                return text.Contains(query, StringComparison.OrdinalIgnoreCase);
            }
            catch (RegexMatchTimeoutException ex)
            {
                // Regex took too long - expected for complex patterns, use fallback
                _logger.LogWarning($"Regex timeout for pattern '{query}': {ex.Message}", "SearchService");
                return text.Contains(query, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
