namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Result of an export operation.
    /// Follows Result pattern - check IsSuccess before using output path.
    /// </summary>
    public record ExportResult
    {
        /// <summary>
        /// Whether the export completed successfully.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Path to the exported file (only valid if IsSuccess is true).
        /// </summary>
        public string? OutputPath { get; init; }

        /// <summary>
        /// Error message if export failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Number of rows exported (excluding header if applicable).
        /// </summary>
        public int RowsExported { get; init; }

        /// <summary>
        /// Number of columns exported.
        /// </summary>
        public int ColumnsExported { get; init; }

        /// <summary>
        /// Number of cells with normalization applied.
        /// </summary>
        public int NormalizedCellCount { get; init; }

        /// <summary>
        /// File size in bytes (only valid if IsSuccess is true).
        /// </summary>
        public long FileSizeBytes { get; init; }

        /// <summary>
        /// Export duration.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        /// Factory method for successful export.
        /// </summary>
        public static ExportResult Success(
            string outputPath,
            int rowsExported,
            int columnsExported,
            int normalizedCellCount,
            long fileSizeBytes,
            TimeSpan duration) => new()
            {
                IsSuccess = true,
                OutputPath = outputPath,
                RowsExported = rowsExported,
                ColumnsExported = columnsExported,
                NormalizedCellCount = normalizedCellCount,
                FileSizeBytes = fileSizeBytes,
                Duration = duration
            };

        /// <summary>
        /// Factory method for failed export.
        /// </summary>
        public static ExportResult Failure(string errorMessage, TimeSpan duration) => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }
}
