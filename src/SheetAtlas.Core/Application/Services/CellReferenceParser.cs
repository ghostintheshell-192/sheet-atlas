using System.Text.RegularExpressions;
using SheetAtlas.Core.Application.Interfaces;

namespace SheetAtlas.Core.Application.Services
{
    public partial class CellReferenceParser : ICellReferenceParser
    {
        private static readonly Regex _columnRegex = MyRegex();
        private static readonly Regex _rowRegex = new("[0-9]+", RegexOptions.Compiled);

        public string GetColumnName(string cellReference)
        {
            var match = _columnRegex.Match(cellReference);
            return match.Success ? match.Value : string.Empty;
        }

        public int GetColumnIndex(string cellReference)
        {
            string columnName = GetColumnName(cellReference);
            int columnIndex = 0;

            for (int i = 0; i < columnName.Length; i++)
            {
                columnIndex = columnIndex * 26 + (columnName[i] - 'A' + 1);
            }

            return columnIndex - 1;
        }

        public int GetRowIndex(string cellReference)
        {
            var match = _rowRegex.Match(cellReference);
            if (int.TryParse(match.Value, out int rowIndex))
            {
                return rowIndex - 1; // Convert to 0-based
            }
            return 0;
        }

        public string CreateCellReference(int columnIndex, int rowIndex)
        {
            string columnName = GetColumnNameFromIndex(columnIndex);
            return $"{columnName}{rowIndex + 1}";
        }

        public string GetColumnNameFromIndex(int columnIndex)
        {
            string columnName = "";
            columnIndex++; // Convert from 0-based to 1-based

            while (columnIndex > 0)
            {
                int modulo = (columnIndex - 1) % 26;
                columnName = (char)('A' + modulo) + columnName;
                columnIndex = (columnIndex - modulo) / 26;
            }

            return columnName;
        }

        [GeneratedRegex("[A-Za-z]+", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}
