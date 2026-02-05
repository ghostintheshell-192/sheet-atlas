namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Defines a named rectangular data region within an Excel sheet.
    /// Used as interpretive lens for search, comparison, and normalization.
    /// Stored in SASheetData as Dictionary&lt;string, DataRegion&gt; keyed by Name.
    /// See ADR-009 for design decisions.
    /// </summary>
    public record DataRegion
    {
        /// <summary>
        /// User-friendly identifier for this region. Required — used as Dictionary key.
        /// Default region uses the sheet name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// First row of headers (0-based). Null = auto-detect.
        /// </summary>
        public int? HeaderStartRow { get; init; }

        /// <summary>
        /// Last row of headers (inclusive). Null = single-row header.
        /// </summary>
        public int? HeaderEndRow { get; init; }

        /// <summary>
        /// First row of actual data (0-based). Required.
        /// </summary>
        public int DataStartRow { get; init; }

        /// <summary>
        /// Last row of data (inclusive). Null = till end of sheet.
        /// </summary>
        public int? DataEndRow { get; init; }

        /// <summary>
        /// First column of the region (0-based). Null = first column.
        /// </summary>
        public int? StartColumn { get; init; }

        /// <summary>
        /// Last column of the region (inclusive, 0-based). Null = last column.
        /// </summary>
        public int? EndColumn { get; init; }

        /// <summary>
        /// Whether this region was auto-detected or manually specified.
        /// </summary>
        public bool IsAutoDetected { get; init; }

        /// <summary>
        /// Factory: Auto-detect everything (default behavior).
        /// </summary>
        public static DataRegion AutoDetect(string name) => new()
        {
            Name = name,
            DataStartRow = 0,
            IsAutoDetected = true
        };

        /// <summary>
        /// Factory: Manual selection from UI.
        /// </summary>
        /// <param name="name">Region name</param>
        /// <param name="headerStart">First header row (0-based)</param>
        /// <param name="dataStart">First data row (0-based)</param>
        /// <param name="dataEnd">Last data row (inclusive), null = till end</param>
        public static DataRegion Manual(string name, int headerStart, int dataStart, int? dataEnd = null) =>
            new()
            {
                Name = name,
                HeaderStartRow = headerStart,
                DataStartRow = dataStart,
                DataEndRow = dataEnd,
                IsAutoDetected = false
            };

        /// <summary>
        /// Factory: Manual data range only (header auto-detected within range).
        /// </summary>
        /// <param name="name">Region name</param>
        /// <param name="dataStart">First row to consider (0-based)</param>
        /// <param name="dataEnd">Last row to consider (inclusive), null = till end</param>
        public static DataRegion FromDataRange(string name, int dataStart, int? dataEnd = null) =>
            new()
            {
                Name = name,
                DataStartRow = dataStart,
                DataEndRow = dataEnd,
                IsAutoDetected = false
            };

        /// <summary>
        /// Factory: Region covering an entire sheet. Used as default when no user selection.
        /// Name defaults to the sheet name.
        /// </summary>
        /// <param name="name">Region name (typically the sheet name)</param>
        /// <param name="rowCount">Total row count of the sheet</param>
        /// <param name="colCount">Total column count of the sheet</param>
        public static DataRegion WholeSheet(string name, int rowCount, int colCount) =>
            new()
            {
                Name = name,
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = rowCount > 1 ? rowCount - 1 : null,
                StartColumn = 0,
                EndColumn = colCount > 0 ? colCount - 1 : null,
                IsAutoDetected = true
            };

        /// <summary>
        /// Validates the data region configuration.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return false;

            if (DataStartRow < 0)
                return false;

            if (HeaderStartRow.HasValue && HeaderStartRow.Value < 0)
                return false;

            if (HeaderEndRow.HasValue && HeaderStartRow.HasValue && HeaderEndRow.Value < HeaderStartRow.Value)
                return false;

            if (DataEndRow.HasValue && DataEndRow.Value < DataStartRow)
                return false;

            if (StartColumn.HasValue && StartColumn.Value < 0)
                return false;

            if (EndColumn.HasValue && StartColumn.HasValue && EndColumn.Value < StartColumn.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Gets the effective header row count (1 if not specified).
        /// </summary>
        public int HeaderRowCount =>
            HeaderStartRow.HasValue && HeaderEndRow.HasValue
                ? HeaderEndRow.Value - HeaderStartRow.Value + 1
                : 1;

        /// <summary>
        /// Checks if this region overlaps with another region (both row and column ranges).
        /// Two regions overlap if their row ranges AND column ranges intersect.
        /// </summary>
        public bool OverlapsWith(DataRegion other)
        {
            if (!RowRangeOverlaps(other))
                return false;

            if (!ColumnRangeOverlaps(other))
                return false;

            return true;
        }

        private bool RowRangeOverlaps(DataRegion other)
        {
            int thisStart = HeaderStartRow ?? DataStartRow;
            int otherStart = other.HeaderStartRow ?? other.DataStartRow;

            // Use int.MaxValue for unbounded end
            int thisEnd = DataEndRow ?? int.MaxValue;
            int otherEnd = other.DataEndRow ?? int.MaxValue;

            return thisStart <= otherEnd && otherStart <= thisEnd;
        }

        private bool ColumnRangeOverlaps(DataRegion other)
        {
            // If either region has no column bounds, they potentially overlap on all columns
            int thisStart = StartColumn ?? 0;
            int otherStart = other.StartColumn ?? 0;

            int thisEnd = EndColumn ?? int.MaxValue;
            int otherEnd = other.EndColumn ?? int.MaxValue;

            return thisStart <= otherEnd && otherStart <= thisEnd;
        }
    }
}
