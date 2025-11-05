using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Analyzes column characteristics: data type, confidence, generates ColumnMetadata.
    /// Enhances existing ColumnMetadata record with detected type and quality metrics.
    /// </summary>
    /// <remarks>
    /// Samples ~100 cells, calculates type distribution and confidence.
    /// Confidence > 0.8 = strong type, else = mixed.
    /// Detects headers, currency, warnings.
    /// </remarks>
    public interface IColumnAnalysisService
    {
        /// <summary>
        /// Analyzes column to detect data type and populate metadata.
        /// </summary>
        /// <param name="columnIndex">0-based column index</param>
        /// <param name="columnName">Column header name</param>
        /// <param name="sampleCells">Normalized cell values from column (skip blanks)</param>
        /// <param name="numberFormats">Corresponding Excel number formats</param>
        /// <param name="customRegion">Optional user-defined data region (future UI)</param>
        /// <returns>Enhanced ColumnMetadata with type, confidence, currency, warnings</returns>
        ColumnAnalysisResult AnalyzeColumn(
            int columnIndex,
            string columnName,
            IReadOnlyList<SACellValue> sampleCells,
            IReadOnlyList<string?> numberFormats,
            DataRegion? customRegion = null);

        /// <summary>
        /// Detects header rows in sheet data (not-yet-normalized).
        /// </summary>
        /// <param name="firstRows">First 10-20 rows of sheet data</param>
        /// <param name="columnCount">Number of columns</param>
        /// <param name="customRegion">Optional user-defined region (overrides auto-detect)</param>
        /// <returns>Header analysis: row indices that are headers, confidence</returns>
        HeaderDetectionResult DetectHeaders(
            IReadOnlyList<SACellData[]> firstRows,
            int columnCount,
            DataRegion? customRegion = null);
    }
}
