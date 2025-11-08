using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Orchestrates the analysis and enrichment pipeline for sheet data.
    /// Coordinates foundation services: column analysis, currency detection, data normalization.
    /// </summary>
    public class SheetAnalysisOrchestrator : ISheetAnalysisOrchestrator
    {
        private readonly IColumnAnalysisService _columnAnalysisService;
        private readonly IDataNormalizationService _normalizationService;
        private readonly ILogService _logger;

        public SheetAnalysisOrchestrator(
            IColumnAnalysisService columnAnalysisService,
            IDataNormalizationService normalizationService,
            ILogService logger)
        {
            _columnAnalysisService = columnAnalysisService ?? throw new ArgumentNullException(nameof(columnAnalysisService));
            _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<SASheetData> EnrichAsync(SASheetData rawData, string fileName, List<ExcelError> errors)
        {
            if (rawData == null)
                throw new ArgumentNullException(nameof(rawData));
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            // Run enrichment pipeline
            EnrichSheetWithColumnAnalysis(fileName, rawData, errors);

            // Return enriched data (Task.FromResult for now - async for future steps)
            return Task.FromResult(rawData);
        }

        /// <summary>
        /// Enriches sheet data with column analysis using foundation services.
        /// Samples cells from each column, normalizes data, runs analysis, populates metadata, adds anomalies as ExcelErrors.
        /// </summary>
        private void EnrichSheetWithColumnAnalysis(string fileName, SASheetData sheetData, List<ExcelError> errors)
        {
            int maxSampleSize = Math.Min(100, sheetData.RowCount);

            for (int colIndex = 0; colIndex < sheetData.ColumnCount; colIndex++)
            {
                // Sample cells from column (include empty cells for anomaly detection)
                var sampleCells = new List<SACellValue>();
                var numberFormats = new List<string?>();

                for (int rowIndex = 0; rowIndex < maxSampleSize && rowIndex < sheetData.RowCount; rowIndex++)
                {
                    var cellData = sheetData.GetCellData(rowIndex, colIndex);

                    // Normalize cell value (soft normalization: trim whitespace, clean text)
                    // NOTE: Empty cells are included in sample for anomaly detection
                    var normalized = cellData.Value.IsEmpty
                        ? cellData.Value
                        : NormalizeCellValue(cellData.Value, cellData.Metadata?.NumberFormat);

                    sampleCells.Add(normalized);
                    // Extract numberFormat from metadata (saved during file read)
                    numberFormats.Add(cellData.Metadata?.NumberFormat);
                }

                // Skip completely empty columns
                if (sampleCells.All(c => c.IsEmpty))
                    continue;

                // Analyze column
                var analysisResult = _columnAnalysisService.AnalyzeColumn(
                    colIndex,
                    sheetData.ColumnNames[colIndex],
                    sampleCells,
                    numberFormats,
                    customRegion: null
                );

                // Populate column metadata
                sheetData.SetColumnMetadata(colIndex, analysisResult.ToMetadata());

                // Add anomalies to errors list (will be saved in structured JSON log)
                foreach (var anomaly in analysisResult.Anomalies)
                {
                    var error = CreateExcelErrorFromAnomaly(fileName, sheetData.SheetName, colIndex, anomaly);
                    errors.Add(error);
                }

                // Log analysis results for debugging
                _logger.LogInfo(
                    $"[ENRICHMENT] Column '{sheetData.ColumnNames[colIndex]}' (idx={colIndex}): " +
                    $"Type={analysisResult.DetectedType}, Confidence={analysisResult.TypeConfidence:F2}, " +
                    $"Samples={sampleCells.Count}, Anomalies={analysisResult.Anomalies.Count}",
                    "SheetAnalysisOrchestrator");

                // Log each anomaly for debugging
                foreach (var anomaly in analysisResult.Anomalies)
                {
                    _logger.LogWarning(
                        $"[ANOMALY DETECTED] {sheetData.SheetName} Row{anomaly.RowIndex} Col{colIndex}: {anomaly.Message}",
                        "SheetAnalysisOrchestrator");
                }
            }
        }

        /// <summary>
        /// Normalizes a cell value using DataNormalizationService (soft normalization: trim whitespace, clean text).
        /// Returns normalized SACellValue for improved type detection accuracy.
        /// </summary>
        private SACellValue NormalizeCellValue(SACellValue original, string? numberFormat)
        {
            // Convert SACellValue to object for normalization service
            object? rawValue = original.IsText ? original.AsText()
                : original.IsNumber ? original.AsNumber()
                : original.IsBoolean ? original.AsBoolean()
                : original.IsDateTime ? original.AsDateTime()
                : null;

            if (rawValue == null)
                return original;

            // Normalize using DataNormalizationService
            var result = _normalizationService.Normalize(rawValue, numberFormat);

            // Return cleaned value if successful, otherwise return original
            return result.IsSuccess && result.CleanedValue != null ? result.CleanedValue.Value : original;
        }

        /// <summary>
        /// Helper method: Maps CellAnomaly to ExcelError for structured file logging.
        /// Creates cell-level error with location reference (e.g., row=5, col=2) and appropriate severity.
        /// </summary>
        private ExcelError CreateExcelErrorFromAnomaly(string fileName, string sheetName, int columnIndex, CellAnomaly anomaly)
        {
            // Create cell reference (0-based indices)
            var cellRef = new CellReference(anomaly.RowIndex, columnIndex);

            // Message includes sheet name and cell location in Excel notation (e.g., "Sheet1!B2")
            string cellAddress = cellRef.ToExcelNotation();
            string message = $"{sheetName}!{cellAddress}: {anomaly.Message} (Expected: {anomaly.ExpectedType}, Actual: {anomaly.ActualType})";
            string context = $"Cell:{sheetName}";

            // Use appropriate factory method based on anomaly severity
            return anomaly.Severity switch
            {
                LogSeverity.Info => ExcelError.Info(context, message),
                LogSeverity.Warning => ExcelError.Warning(context, message),
                LogSeverity.Error => ExcelError.CellError(sheetName, cellRef, message),
                LogSeverity.Critical => ExcelError.Critical(context, message),
                _ => ExcelError.Warning(context, message) // Default fallback
            };
        }
    }
}
