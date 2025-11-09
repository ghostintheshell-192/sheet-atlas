namespace SheetAtlas.Core.Shared.Helpers
{
    /// <summary>
    /// Row indexing converter for SheetAtlas.
    ///
    /// TWO INDEXING SYSTEMS:
    /// - Excel: 1-based (Row 1 = first row visible in Excel, typically header)
    /// - Absolute: 0-based (Row 0 = first row in sheet, SASheetData uses this)
    ///
    /// MAPPING EXAMPLE (single-row header):
    /// - Excel Row 1 ↔ Absolute Row 0 (header)
    /// - Excel Row 2 ↔ Absolute Row 1 (first data row)
    /// - Excel Row 3 ↔ Absolute Row 2 (second data row)
    ///
    /// MAPPING EXAMPLE (two-row header):
    /// - Excel Row 1 ↔ Absolute Row 0 (header row 1)
    /// - Excel Row 2 ↔ Absolute Row 1 (header row 2)
    /// - Excel Row 3 ↔ Absolute Row 2 (first data row)
    ///
    /// USAGE:
    /// - ExcelToAbsolute: When parsing user input or Excel notation ("A5" → row 4)
    /// - AbsoluteToExcel: When generating Excel notation for errors (row 4 → "A5")
    /// </summary>
    public static class RowIndexConverter
    {
        /// <summary>
        /// Converts Excel 1-based row to absolute 0-based row.
        /// Example: Excel Row 1 → Absolute Row 0
        /// </summary>
        /// <param name="excelRow">Excel row number (1-based, as shown in Excel UI)</param>
        /// <returns>Absolute 0-based row index (for SASheetData access)</returns>
        public static int ExcelToAbsolute(int excelRow)
        {
            if (excelRow < 1)
                throw new ArgumentException("Excel row must be >= 1", nameof(excelRow));

            return excelRow - 1;
        }

        /// <summary>
        /// Converts absolute 0-based row to Excel 1-based row.
        /// Example: Absolute Row 0 → Excel Row 1
        /// </summary>
        /// <param name="absoluteRow">Absolute 0-based row index (SASheetData row)</param>
        /// <returns>Excel row number (1-based, for display to user)</returns>
        public static int AbsoluteToExcel(int absoluteRow)
        {
            if (absoluteRow < 0)
                throw new ArgumentException("Absolute row must be >= 0", nameof(absoluteRow));

            return absoluteRow + 1;
        }
    }
}
