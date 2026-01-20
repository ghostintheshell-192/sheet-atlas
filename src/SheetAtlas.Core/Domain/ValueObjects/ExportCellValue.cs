namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Wrapper that preserves both value and formatting metadata for export.
    /// Used in the comparison flow to maintain type and format information
    /// from source Excel files through to exported output.
    /// </summary>
    public readonly struct ExportCellValue
    {
        /// <summary>
        /// The typed cell value (number, text, date, etc.)
        /// </summary>
        public SACellValue Value { get; }

        /// <summary>
        /// Excel number format string (e.g., "[$€-407] #,##0.00", "0.00%").
        /// Used to apply the same formatting in exported Excel files.
        /// </summary>
        public string? NumberFormat { get; }

        public ExportCellValue(SACellValue value, string? numberFormat = null)
        {
            Value = value;
            NumberFormat = numberFormat;
        }

        public ExportCellValue(SACellData cellData)
        {
            Value = cellData.EffectiveValue;
            NumberFormat = cellData.Metadata?.NumberFormat;
        }

        public override string ToString() => Value.ToString();
    }
}
