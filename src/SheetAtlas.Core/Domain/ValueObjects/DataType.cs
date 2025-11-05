namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Detected data type for cell or column.
    /// Used by normalization and column analysis services.
    /// </summary>
    public enum DataType : byte
    {
        Unknown = 0,
        Number = 1,
        Date = 2,
        Currency = 3,
        Percentage = 4,
        Text = 5,
        Boolean = 6,
        Error = 7
    }

    /// <summary>
    /// Excel cell data type (from cell metadata).
    /// Maps to OpenXML CellValues enum.
    /// </summary>
    public enum CellDataType : byte
    {
        General = 0,
        Number = 1,
        String = 2,
        Boolean = 3,
        Error = 4,
        Blank = 5
    }
}
