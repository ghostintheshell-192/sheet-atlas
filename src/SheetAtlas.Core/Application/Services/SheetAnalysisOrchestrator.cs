using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Logging.Services;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Orchestrates the analysis and enrichment pipeline for sheet data.
    /// Coordinates foundation services: merged cell resolution, column analysis, currency detection, data normalization.
    /// </summary>
    public class SheetAnalysisOrchestrator : ISheetAnalysisOrchestrator
    {
        private readonly IMergedCellResolver _mergedCellResolver;
        private readonly IColumnAnalysisService _columnAnalysisService;
        private readonly IDataNormalizationService _normalizationService;
        private readonly ILogService _logger;
        private readonly MergeStrategy _defaultMergeStrategy;
        private readonly double _warnThreshold;

        public SheetAnalysisOrchestrator(
            IMergedCellResolver mergedCellResolver,
            IColumnAnalysisService columnAnalysisService,
            IDataNormalizationService normalizationService,
            ILogService logger,
            MergeStrategy defaultMergeStrategy = MergeStrategy.ExpandValue,
            double warnThreshold = 0.20)
        {
            _mergedCellResolver = mergedCellResolver ?? throw new ArgumentNullException(nameof(mergedCellResolver));
            _columnAnalysisService = columnAnalysisService ?? throw new ArgumentNullException(nameof(columnAnalysisService));
            _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultMergeStrategy = defaultMergeStrategy;
            _warnThreshold = warnThreshold;
        }

        public Task<SASheetData> EnrichAsync(SASheetData rawData, List<ExcelError> errors)
        {
            ArgumentNullException.ThrowIfNull(rawData);
            ArgumentNullException.ThrowIfNull(errors);

            // NOTE: HeaderRowCount is set by file reader (default=1)
            // Future: UI will allow manual configuration for multi-row headers

            var resolvedData = ResolveMergedCells(rawData, errors);

            EnrichSheetWithColumnAnalysis(resolvedData, errors);

            return Task.FromResult(resolvedData);
        }

        /// <summary>
        /// Resolves merged cells using MergedCellResolver if any merged cells exist.
        /// Analyzes complexity, applies configured strategy, generates warnings.
        /// MUST run BEFORE column analysis to ensure accurate type detection.
        /// Synchronous operation - all work is in-memory (no I/O).
        /// </summary>
        private SASheetData ResolveMergedCells(SASheetData sheetData, List<ExcelError> errors)
        {
            if (sheetData.MergedCells.Count == 0)
            {
                _logger.LogInfo($"[MERGE RESOLUTION] No merged cells detected in {sheetData.SheetName}", "SheetAnalysisOrchestrator");
                return sheetData;
            }

            var analysis = _mergedCellResolver.AnalyzeMergeComplexity(sheetData.MergedCells);

            _logger.LogInfo(
                $"[MERGE RESOLUTION] {sheetData.SheetName}: {analysis.Explanation} " +
                $"(Level={analysis.Level}, Percentage={analysis.MergedCellPercentage:P1}, " +
                $"Ranges={analysis.TotalMergeRanges}, Vertical={analysis.VerticalMergeCount}, Horizontal={analysis.HorizontalMergeCount})",
                "SheetAnalysisOrchestrator");

            if (analysis.MergedCellPercentage > _warnThreshold)
            {
                errors.Add(ExcelError.Warning(
                    $"Sheet:{sheetData.SheetName}",
                    $"High merge density detected ({analysis.MergedCellPercentage:P0}, threshold: {_warnThreshold:P0}) - {analysis.Explanation}"));
            }

            // Use configured default strategy
            var strategy = _defaultMergeStrategy;

            // Apply merge resolution with warning callback (synchronous in-memory operation)
            var resolvedData = _mergedCellResolver.ResolveMergedCells(
                sheetData,
                strategy,
                warning => HandleMergeWarning(sheetData.SheetName, warning, errors));

            _logger.LogInfo(
                $"[MERGE RESOLUTION] Applied strategy {strategy} to {sheetData.SheetName}",
                "SheetAnalysisOrchestrator");

            return resolvedData;
        }

        /// <summary>
        /// Callback for merge warnings from MergedCellResolver.
        /// Logs all warnings, adds ExcelError for high-complexity warnings.
        /// </summary>
        private void HandleMergeWarning(string sheetName, MergeWarning warning, List<ExcelError> errors)
        {
            // Always log
            _logger.LogWarning(
                $"[MERGE WARNING] {sheetName} {warning.RangeRef}: {warning.Message} (Complexity={warning.Complexity})",
                "SheetAnalysisOrchestrator");

            // Add ExcelError only for Chaos level (hybrid approach)
            if (warning.Complexity == MergeComplexity.Chaos)
            {
                errors.Add(ExcelError.Warning(
                    $"Sheet:{sheetName}",
                    $"Merge range {warning.RangeRef}: {warning.Message}"));
            }
        }

        /// <summary>
        /// Enriches sheet data with column analysis using foundation services.
        /// Samples cells from each column, normalizes data, runs analysis, populates metadata, adds anomalies as ExcelErrors.
        /// NOTE: Only analyzes DATA rows (skips header rows).
        /// Also saves NormalizationResult in cell metadata for export support.
        /// </summary>
        private void EnrichSheetWithColumnAnalysis(SASheetData sheetData, List<ExcelError> errors)
        {
            int maxSampleSize = Math.Min(100, sheetData.DataRowCount);

            for (int colIndex = 0; colIndex < sheetData.ColumnCount; colIndex++)
            {
                // Sample cells from column (include empty cells for anomaly detection)
                var sampleCells = new List<SACellValue>();
                var numberFormats = new List<string?>();
                var absoluteRowIndices = new List<int>(); // Track absolute row indices for anomaly reporting
                var normalizationResults = new List<NormalizationResult>(); // Store results for cell update

                // Iterate ONLY over data rows (skip header rows)
                for (int dataRowIndex = 0; dataRowIndex < maxSampleSize && dataRowIndex < sheetData.DataRowCount; dataRowIndex++)
                {
                    int absoluteRow = sheetData.HeaderRowCount + dataRowIndex;
                    var cellData = sheetData.GetCellData(absoluteRow, colIndex);

                    // Normalize cell value and preserve full result
                    var normResult = NormalizeCellValue(cellData.Value, cellData.Metadata?.NumberFormat);
                    normalizationResults.Add(normResult);

                    // Use cleaned value for analysis (or original if empty/failed)
                    var normalizedValue = normResult.IsSuccess && normResult.CleanedValue.HasValue
                        ? normResult.CleanedValue.Value
                        : cellData.Value;

                    sampleCells.Add(normalizedValue);
                    // Extract numberFormat from metadata (saved during file read)
                    numberFormats.Add(cellData.Metadata?.NumberFormat);
                    // Track absolute row index for this cell (for anomaly reporting)
                    absoluteRowIndices.Add(absoluteRow);
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

                // Update cell metadata with normalization results
                for (int i = 0; i < absoluteRowIndices.Count; i++)
                {
                    var absoluteRow = absoluteRowIndices[i];
                    var normResult = normalizationResults[i];

                    // Only update cells that have meaningful normalization results
                    if (normResult.IsSuccess && normResult.CleanedValue.HasValue)
                    {
                        UpdateCellWithNormalizationResult(sheetData, absoluteRow, colIndex, normResult);
                    }
                }

                // Add anomalies to errors list (will be saved in structured JSON log)
                // Map sample row index to absolute row index
                foreach (var anomaly in analysisResult.Anomalies)
                {
                    var error = CreateExcelErrorFromAnomaly(sheetData.SheetName, colIndex, anomaly, absoluteRowIndices);
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
        /// Updates a cell's metadata with normalization result.
        /// Creates new CellMetadata if needed, preserves existing metadata fields.
        /// </summary>
        private static void UpdateCellWithNormalizationResult(
            SASheetData sheetData,
            int row,
            int column,
            NormalizationResult normResult)
        {
            var currentCell = sheetData.GetCellData(row, column);

            // Build new metadata (preserve existing fields, add normalization data)
            var newMetadata = new CellMetadata
            {
                // Preserve existing fields
                NumberFormat = currentCell.Metadata?.NumberFormat,
                Formula = currentCell.Metadata?.Formula,
                Style = currentCell.Metadata?.Style,
                Validation = currentCell.Metadata?.Validation,
                Currency = currentCell.Metadata?.Currency,
                CustomData = currentCell.Metadata?.CustomData,

                // Add normalization results
                OriginalValue = normResult.OriginalValue,
                CleanedValue = normResult.CleanedValue,
                DetectedType = normResult.DetectedType,
                QualityIssue = normResult.QualityIssue != DataQualityIssue.None
                    ? normResult.QualityIssue
                    : currentCell.Metadata?.QualityIssue
            };

            // Create new cell with updated metadata
            var updatedCell = new SACellData(currentCell.Value, newMetadata);
            sheetData.SetCellData(row, column, updatedCell);
        }

        /// <summary>
        /// Normalizes a cell value using DataNormalizationService.
        /// Returns full NormalizationResult for storage in cell metadata.
        /// </summary>
        private NormalizationResult NormalizeCellValue(SACellValue original, string? numberFormat)
        {
            // Empty cells don't need normalization
            if (original.IsEmpty)
                return NormalizationResult.Empty;

            // Convert SACellValue to object for normalization service
            object? rawValue = original.IsText ? original.AsText()
                : original.IsNumber ? original.AsNumber()
                : original.IsBoolean ? original.AsBoolean()
                : original.IsDateTime ? original.AsDateTime()
                : null;

            if (rawValue == null)
                return NormalizationResult.Empty;

            // Normalize using DataNormalizationService and return full result
            return _normalizationService.Normalize(rawValue, numberFormat);
        }

        /// <summary>
        /// Helper method: Maps CellAnomaly to ExcelError for structured file logging.
        /// Creates cell-level error with location reference (e.g., row=5, col=2) and appropriate severity.
        /// </summary>
        /// <param name="sheetName">Sheet name where anomaly was found</param>
        /// <param name="columnIndex">Column index (0-based)</param>
        /// <param name="anomaly">Cell anomaly with sample-relative row index</param>
        /// <param name="absoluteRowIndices">Mapping from sample index to absolute sheet row index</param>
        private static ExcelError CreateExcelErrorFromAnomaly(string sheetName, int columnIndex, CellAnomaly anomaly, List<int> absoluteRowIndices)
        {
            // Map sample row index to absolute sheet row index
            // anomaly.RowIndex is relative to the sample (0 = first cell in sample)
            // absoluteRowIndices[anomaly.RowIndex] gives the actual row in SASheetData (absolute 0-based)
            int absoluteRow = absoluteRowIndices[anomaly.RowIndex];
            var cellRef = new CellReference(absoluteRow, columnIndex);

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
