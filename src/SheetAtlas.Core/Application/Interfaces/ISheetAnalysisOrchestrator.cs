using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Orchestrates the analysis and enrichment pipeline for sheet data.
    /// Coordinates foundation services to analyze columns, resolve merged cells, and populate metadata.
    /// </summary>
    /// <remarks>
    /// This orchestrator is format-agnostic: works with data from .xlsx, .xls, .csv readers.
    /// Analysis quality depends on available metadata (numberFormat, mergedCells) from reader.
    /// </remarks>
    public interface ISheetAnalysisOrchestrator
    {
        /// <summary>
        /// Enriches raw sheet data with column analysis and metadata.
        /// Runs foundation services pipeline and adds detected anomalies to errors list.
        /// </summary>
        /// <param name="rawData">Raw sheet data from file reader (values populated, metadata minimal)</param>
        /// <param name="fileName">Source file name (for logging/error context)</param>
        /// <param name="errors">Error list to append detected anomalies</param>
        /// <returns>Enriched sheet data with column metadata and analysis results</returns>
        /// <remarks>
        /// Pipeline steps:
        /// 1. Analyze columns (type detection, currency detection, anomaly detection)
        /// 2. Populate column metadata
        /// 3. Add anomalies as ExcelErrors for structured logging
        ///
        /// Future steps (potential):
        /// - Resolve merged cells (if needed)
        /// - Detect headers automatically
        /// - Normalize data values
        /// </remarks>
        Task<SASheetData> EnrichAsync(SASheetData rawData, string fileName, List<ExcelError> errors);
    }
}
