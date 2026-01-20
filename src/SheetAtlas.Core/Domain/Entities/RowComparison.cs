using System.Data;

namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Represents a complete row from an Excel sheet for comparison purposes
    /// </summary>
    public class ExcelRow
    {
        public ExcelFile SourceFile { get; }
        public string SheetName { get; }
        public int RowIndex { get; }
        public IReadOnlyList<object?> Cells { get; }
        public IReadOnlyList<string> ColumnHeaders { get; }

        public string FileName => SourceFile?.FileName ?? string.Empty;
        public string DisplayName => $"{FileName} - {SheetName} - Row {RowIndex + 1}";

        public ExcelRow(ExcelFile sourceFile, string sheetName, int rowIndex,
                       IReadOnlyList<object?> cells, IReadOnlyList<string> columnHeaders)
        {
            SourceFile = sourceFile ?? throw new ArgumentNullException(nameof(sourceFile));
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            RowIndex = rowIndex;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            ColumnHeaders = columnHeaders ?? throw new ArgumentNullException(nameof(columnHeaders));
        }

        /// <summary>
        /// Get cell value by column index
        /// </summary>
        public object? GetCell(int columnIndex)
        {
            return columnIndex >= 0 && columnIndex < Cells.Count ? Cells[columnIndex] : null;
        }

        /// <summary>
        /// Get cell value as string
        /// </summary>
        public string GetCellAsString(int columnIndex)
        {
            return GetCell(columnIndex)?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Get cell value by header name with intelligent mapping
        /// </summary>
        public string GetCellAsStringByHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                return string.Empty;

            var headerIndex = ColumnHeaders.ToList().IndexOf(headerName);
            if (headerIndex >= 0)
            {
                return GetCellAsString(headerIndex);
            }

            var normalizedTargetHeader = headerName.Trim().ToLowerInvariant();

            for (int i = 0; i < ColumnHeaders.Count; i++)
            {
                var normalizedHeader = ColumnHeaders[i].Trim().ToLowerInvariant();
                if (normalizedHeader == normalizedTargetHeader)
                {
                    return GetCellAsString(i);
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Represents a comparison between multiple Excel rows
    /// </summary>
    public class RowComparison
    {
        public Guid Id { get; }
        public IReadOnlyList<ExcelRow> Rows { get; }
        public DateTime CreatedAt { get; }
        public string Name { get; set; }
        public IReadOnlyList<RowComparisonWarning> Warnings { get; private set; }
        public IReadOnlyList<string> SearchTerms { get; }

        public RowComparison(IReadOnlyList<ExcelRow> rows, IReadOnlyList<string>? searchTerms = null, string? name = null)
        {
            if (rows == null || rows.Count < 2)
                throw new ArgumentException("At least two rows are required for comparison", nameof(rows));

            Id = Guid.NewGuid();
            Rows = rows;
            CreatedAt = DateTime.UtcNow;
            Name = name ?? $"Comparison {CreatedAt:HH:mm:ss}";
            SearchTerms = searchTerms ?? Array.Empty<string>().AsReadOnly();
            Warnings = AnalyzeStructuralIssues();
        }

        /// <summary>
        /// Get all unique column headers from all rows
        /// </summary>
        public IReadOnlyList<string> GetAllColumnHeaders()
        {
            return Rows
                .SelectMany(r => r.ColumnHeaders)
                .Distinct()
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Get maximum number of columns across all rows
        /// </summary>
        public int MaxColumns => Rows.Max(r => r.Cells.Count);

        /// <summary>
        /// Get a normalized column mapping that aligns headers across all rows
        /// </summary>
        public IReadOnlyDictionary<string, int> GetNormalizedColumnMapping()
        {
            var allUniqueHeaders = GetAllColumnHeaders();
            var mapping = new Dictionary<string, int>();

            for (int i = 0; i < allUniqueHeaders.Count; i++)
            {
                mapping[allUniqueHeaders[i]] = i;
            }

            return mapping.AsReadOnly();
        }

        /// <summary>
        /// Analyze structural issues and generate warnings
        /// </summary>
        private IReadOnlyList<RowComparisonWarning> AnalyzeStructuralIssues()
        {
            var warnings = new List<RowComparisonWarning>();
            var allHeaders = GetAllColumnHeaders();

            var rowsByFile = Rows.GroupBy(r => r.FileName).ToList();

            foreach (var headerName in allHeaders)
            {
                var filesWithMissingHeader = new List<string>();
                var filesWithDifferentPosition = new List<string>();

                foreach (var fileGroup in rowsByFile)
                {
                    var sampleRow = fileGroup.First();
                    var headerIndex = sampleRow.ColumnHeaders.ToList().IndexOf(headerName);

                    if (headerIndex == -1)
                    {
                        filesWithMissingHeader.Add(sampleRow.FileName);
                    }
                    else
                    {
                        var expectedPosition = allHeaders.ToList().IndexOf(headerName);
                        if (headerIndex != expectedPosition)
                        {
                            filesWithDifferentPosition.Add(sampleRow.FileName);
                        }
                    }
                }

                if (filesWithMissingHeader.Count != 0)
                {
                    warnings.Add(RowComparisonWarning.CreateMissingHeaderWarning(headerName, filesWithMissingHeader));
                }

                if (filesWithDifferentPosition.Count != 0)
                {
                    warnings.Add(RowComparisonWarning.CreateStructureMismatchWarning(headerName, filesWithDifferentPosition));
                }
            }

            return warnings.AsReadOnly();
        }
    }

    /// <summary>
    /// Represents a request to create a row comparison from search results
    /// </summary>
    public class RowComparisonRequest
    {
        public IReadOnlyList<SearchResult> SelectedMatches { get; }
        public IReadOnlyList<string> SearchTerms { get; }
        public string? Name { get; set; }

        public RowComparisonRequest(
            IReadOnlyList<SearchResult> selectedMatches,
            IReadOnlyList<string>? searchTerms = null,
            string? name = null)
        {
            SelectedMatches = selectedMatches ?? throw new ArgumentNullException(nameof(selectedMatches));
            SearchTerms = searchTerms ?? Array.Empty<string>().AsReadOnly();
            Name = name;
        }
    }
}
