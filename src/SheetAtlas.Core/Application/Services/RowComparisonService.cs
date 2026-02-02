using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.Exceptions;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Implementation of IRowComparisonService. Creates row comparisons from search results.
    /// </summary>
    public class RowComparisonService : IRowComparisonService
    {
        private readonly ILogService _logger;

        public RowComparisonService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RowComparison CreateRowComparison(RowComparisonRequest request)
        {
            if (request?.SelectedMatches == null || request.SelectedMatches.Count < 2)
                throw new ArgumentException("At least two search results are required for comparison", nameof(request));

            _logger.LogInfo($"Creating row comparison from {request.SelectedMatches.Count} search results", "RowComparisonService");

            var excelRows = new List<ExcelRow>();

            foreach (var searchResult in request.SelectedMatches)
            {
                try
                {
                    var excelRow = ExtractRowFromSearchResult(searchResult);
                    excelRows.Add(excelRow);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to extract row from search result: {searchResult.FileName}, Sheet: {searchResult.SheetName}, Row: {searchResult.Row}", ex, "RowComparisonService");
                    throw;
                }
            }

            return new RowComparison(excelRows.AsReadOnly(), request.SearchTerms, request.Name);
        }

        public ExcelRow ExtractRowFromSearchResult(SearchResult searchResult)
        {
            ArgumentNullException.ThrowIfNull(searchResult);
            ArgumentNullException.ThrowIfNull(searchResult.SourceFile, nameof(searchResult.SourceFile));

            // Skip non-cell results (filename, sheet name matches)
            if (searchResult.Row < 0 || searchResult.Column < 0)
                throw new ArgumentException("Search result does not represent a valid cell", nameof(searchResult));

            var sheet = searchResult.SourceFile.GetSheet(searchResult.SheetName);
            if (sheet == null)
                throw ComparisonException.MissingSheet(searchResult.SheetName, searchResult.FileName);

            // SearchResult.Row uses ABSOLUTE indexing (0-based, same as SASheetData)
            // Row 0 = first row of sheet (usually header), Row 1 = second row, etc.
            // Display to user = Row + 1 (1-based, like Excel)
            if (searchResult.Row < sheet.HeaderRowCount)
                throw new ArgumentException($"Row {searchResult.Row + 1} is a header row and cannot be compared", nameof(searchResult));

            if (searchResult.Row >= sheet.RowCount)
                throw new ArgumentOutOfRangeException(nameof(searchResult), $"Row index {searchResult.Row} is out of range for sheet '{searchResult.SheetName}'");

            // Row is already absolute - use directly for SASheetData access
            int absoluteRow = searchResult.Row;

            // Extract complete row data
            var rowCells = sheet.GetRow(absoluteRow);
            // Preserve type and format metadata using ExportCellValue
            var cells = rowCells.Select(cell => (object?)new ExportCellValue(cell)).ToArray();

            // Get column headers
            var columnHeaders = GetColumnHeaders(searchResult.SourceFile, searchResult.SheetName);

            return new ExcelRow(
                searchResult.SourceFile,
                searchResult.SheetName,
                searchResult.Row,
                cells,
                columnHeaders);
        }

        public IReadOnlyList<string> GetColumnHeaders(ExcelFile file, string sheetName)
        {
            ArgumentNullException.ThrowIfNull(file);

            var sheet = file.GetSheet(sheetName);
            if (sheet == null)
                throw ComparisonException.MissingSheet(sheetName, file.FilePath);

            // SASheetData already has ColumnNames array
            return sheet.ColumnNames;
        }
    }
}
