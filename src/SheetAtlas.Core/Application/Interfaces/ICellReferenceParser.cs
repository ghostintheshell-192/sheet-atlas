namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Parses Excel cell references (e.g., A1) to/from column/row indices.
    /// </summary>
    public interface ICellReferenceParser
    {
        string GetColumnName(string cellReference);
        int GetColumnIndex(string cellReference);
        int GetRowIndex(string cellReference);
        string CreateCellReference(int columnIndex, int rowIndex);
        string GetColumnNameFromIndex(int columnIndex);
    }
}
