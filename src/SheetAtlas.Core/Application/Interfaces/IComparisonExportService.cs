using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for exporting row comparison results to various formats.
    /// Includes metadata (search terms, files, timestamps) for context.
    /// </summary>
    public interface IComparisonExportService
    {
        /// <summary>
        /// Exports comparison to Excel with Info sheet (metadata) and Comparison sheet (data).
        /// </summary>
        /// <param name="comparison">Row comparison to export</param>
        /// <param name="outputPath">Output file path (.xlsx)</param>
        /// <param name="includedColumns">Optional original column names to include. If null, all columns are exported.</param>
        /// <param name="semanticNames">Optional mapping from original column names to semantic names for display.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with success/failure status</returns>
        Task<ExportResult> ExportToExcelAsync(
            RowComparison comparison,
            string outputPath,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports comparison to CSV (data only, no metadata sheet).
        /// </summary>
        /// <param name="comparison">Row comparison to export</param>
        /// <param name="outputPath">Output file path (.csv)</param>
        /// <param name="includedColumns">Optional original column names to include. If null, all columns are exported.</param>
        /// <param name="semanticNames">Optional mapping from original column names to semantic names for display.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with success/failure status</returns>
        Task<ExportResult> ExportToCsvAsync(
            RowComparison comparison,
            string outputPath,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a suggested filename for the comparison export.
        /// Format: {YYYY-MM-DD}_{HHmm}_{keyword1}_{keyword2}.{extension}
        /// </summary>
        /// <param name="comparison">Row comparison</param>
        /// <param name="extension">File extension (xlsx or csv)</param>
        /// <returns>Suggested filename</returns>
        string GenerateFilename(RowComparison comparison, string extension);
    }
}
