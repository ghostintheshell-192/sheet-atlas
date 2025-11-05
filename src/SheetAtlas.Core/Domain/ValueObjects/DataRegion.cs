namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Defines a data region within an Excel sheet.
    /// Supports both auto-detection and manual user selection (future UI).
    /// </summary>
    public record DataRegion
    {
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
        /// Whether this region was auto-detected or manually specified.
        /// </summary>
        public bool IsAutoDetected { get; init; }

        /// <summary>
        /// Factory: Auto-detect everything (default behavior).
        /// </summary>
        public static DataRegion AutoDetect => new()
        {
            DataStartRow = 0,
            IsAutoDetected = true
        };

        /// <summary>
        /// Factory: Manual selection from UI (future feature).
        /// </summary>
        /// <param name="headerStart">First header row (0-based)</param>
        /// <param name="dataStart">First data row (0-based)</param>
        /// <param name="dataEnd">Last data row (inclusive), null = till end</param>
        public static DataRegion Manual(int headerStart, int dataStart, int? dataEnd = null) =>
            new()
            {
                HeaderStartRow = headerStart,
                DataStartRow = dataStart,
                DataEndRow = dataEnd,
                IsAutoDetected = false
            };

        /// <summary>
        /// Factory: Manual data range only (header auto-detected within range).
        /// </summary>
        /// <param name="dataStart">First row to consider (0-based)</param>
        /// <param name="dataEnd">Last row to consider (inclusive), null = till end</param>
        public static DataRegion FromDataRange(int dataStart, int? dataEnd = null) =>
            new()
            {
                DataStartRow = dataStart,
                DataEndRow = dataEnd,
                IsAutoDetected = false
            };

        /// <summary>
        /// Validates the data region configuration.
        /// </summary>
        public bool IsValid()
        {
            if (DataStartRow < 0)
                return false;

            if (HeaderStartRow.HasValue && HeaderStartRow.Value < 0)
                return false;

            if (HeaderEndRow.HasValue && HeaderStartRow.HasValue && HeaderEndRow.Value < HeaderStartRow.Value)
                return false;

            if (DataEndRow.HasValue && DataEndRow.Value < DataStartRow)
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
    }
}
