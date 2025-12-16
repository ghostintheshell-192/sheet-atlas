using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for exporting enriched sheet data to various formats.
    /// Uses CleanedValue from cell metadata to write typed cells.
    /// </summary>
    /// <remarks>
    /// KEY PRINCIPLE: Uses the normalized data (CleanedValue) from enrichment phase.
    /// This ensures:
    /// - Dates are written as Excel dates (not serial numbers or text)
    /// - Numbers are written as numbers (not formatted text)
    /// - Text is written clean (no extra whitespace)
    /// - Data can be re-imported without corruption
    /// </remarks>
    public interface IExcelWriterService
    {
        /// <summary>
        /// File extensions supported by this writer for Excel format.
        /// </summary>
        IReadOnlyList<string> SupportedExcelExtensions { get; }

        /// <summary>
        /// Writes sheet data to Excel file (.xlsx) with typed cells.
        /// Uses CleanedValue from cell metadata for proper type preservation.
        /// </summary>
        /// <param name="sheetData">Enriched sheet data with normalization results</param>
        /// <param name="outputPath">Output file path (.xlsx)</param>
        /// <param name="options">Optional export settings</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with success/failure status and statistics</returns>
        Task<ExportResult> WriteToExcelAsync(
            SASheetData sheetData,
            string outputPath,
            ExcelExportOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes sheet data to CSV file with proper escaping.
        /// Uses CleanedValue from cell metadata for consistent data.
        /// </summary>
        /// <param name="sheetData">Enriched sheet data with normalization results</param>
        /// <param name="outputPath">Output file path (.csv)</param>
        /// <param name="options">Optional CSV settings</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with success/failure status and statistics</returns>
        Task<ExportResult> WriteToCsvAsync(
            SASheetData sheetData,
            string outputPath,
            CsvExportOptions? options = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for Excel export.
    /// </summary>
    public record ExcelExportOptions
    {
        /// <summary>
        /// Include header row in output. Default: true.
        /// </summary>
        public bool IncludeHeaders { get; init; } = true;

        /// <summary>
        /// Apply auto-fit to column widths. Default: true.
        /// </summary>
        public bool AutoFitColumns { get; init; } = true;

        /// <summary>
        /// Freeze header row for easier scrolling. Default: false.
        /// </summary>
        public bool FreezeHeaderRow { get; init; } = false;

        /// <summary>
        /// Use original value instead of cleaned value. Default: false.
        /// Set to true to export raw data without normalization.
        /// </summary>
        public bool UseOriginalValues { get; init; } = false;
    }

    /// <summary>
    /// Options for CSV export.
    /// </summary>
    public record CsvExportOptions
    {
        /// <summary>
        /// Field delimiter character. Default: comma.
        /// </summary>
        public char Delimiter { get; init; } = ',';

        /// <summary>
        /// Include header row in output. Default: true.
        /// </summary>
        public bool IncludeHeaders { get; init; } = true;

        /// <summary>
        /// Text encoding for output file. Default: UTF-8 with BOM.
        /// </summary>
        public System.Text.Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

        /// <summary>
        /// Include UTF-8 BOM at start of file. Default: true.
        /// Helps Excel open UTF-8 CSV correctly.
        /// </summary>
        public bool IncludeBom { get; init; } = true;

        /// <summary>
        /// Date format for date values. Default: ISO 8601.
        /// </summary>
        public string DateFormat { get; init; } = "yyyy-MM-dd";

        /// <summary>
        /// Use original value instead of cleaned value. Default: false.
        /// </summary>
        public bool UseOriginalValues { get; init; } = false;
    }
}
