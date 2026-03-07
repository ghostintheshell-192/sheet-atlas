using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for reading Excel cell values with type preservation. Returns SACellValue instead of string.
    /// </summary>
    public interface ICellValueReader
    {
        /// <summary>
        /// Gets the typed value from an Excel cell, preserving original data type.
        /// </summary>
        /// <param name="cell">The cell to read from</param>
        /// <param name="sharedStringTable">The shared string table for the workbook (if available)</param>
        /// <returns>The cell value with type information, or SACellValue.Empty if cell is null</returns>
        SACellValue GetCellValue(Cell cell, SharedStringTable? sharedStringTable);
    }
}
