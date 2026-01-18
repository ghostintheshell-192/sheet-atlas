
namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Configuration options for search operations.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>Whether the search should be case-sensitive.</summary>
        public bool CaseSensitive { get; set; }

        /// <summary>Whether to match the entire cell value exactly (no partial matches).</summary>
        public bool ExactMatch { get; set; }

        /// <summary>Whether to treat the search query as a regular expression.</summary>
        public bool UseRegex { get; set; }
    }

    /// <summary>
    /// Represents a single search match found within an Excel file.
    /// Can represent a file name match, sheet name match, or cell content match.
    /// </summary>
    public class SearchResult
    {
        public ExcelFile SourceFile { get; set; }
        public string SheetName { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> Context { get; set; }

        public string FileName => SourceFile?.FileName ?? string.Empty;
        public string CellAddress => $"{GetColumnName(Column)}{Row + 1}";

        private SearchResult()
        {
            Context = new Dictionary<string, string>();
            SourceFile = null!;
            SheetName = null!;
            Value = null!;
        }

        public SearchResult(ExcelFile sourceFile, string sheetName, int row, int column, string value)
            : this()
        {
            SourceFile = sourceFile;
            SheetName = sheetName;
            Row = row;
            Column = column;
            Value = value;
        }

        private static string GetColumnName(int columnIndex)
        {
            string columnName = "";
            while (columnIndex >= 0)
            {
                columnName = (char)('A' + (columnIndex % 26)) + columnName;
                columnIndex = (columnIndex / 26) - 1;
            }
            return columnName;
        }
    }
}
