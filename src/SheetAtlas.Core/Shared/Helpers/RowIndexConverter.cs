namespace SheetAtlas.Core.Shared.Helpers
{
    /// <summary>
    /// Converts between Excel 1-based and absolute 0-based row indexing. Excel Row 1 = Absolute Row 0. See ADR-002 for details.
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
