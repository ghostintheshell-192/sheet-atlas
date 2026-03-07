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

        public Task EnrichRegionAsync(SASheetData data, DataRegion region, List<ExcelError> errors)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(region);
            ArgumentNullException.ThrowIfNull(errors);

            EnrichRegionWithColumnAnalysis(data, region, errors);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Enriches a specific DataRegion with column analysis.
        /// Mirrors EnrichSheetWithColumnAnalysis but respects region bounds.
        /// </summary>
        private void EnrichRegionWithColumnAnalysis(SASheetData sheetData, DataRegion region, List<ExcelError> errors)
        {
            int startCol = region.StartColumn ?? 0;
            int endCol = region.EndColumn ?? (sheetData.ColumnCount - 1);

            // Collect data rows within region bounds
            var regionRows = sheetData.EnumerateDataRows(region).ToList();
            int maxSampleSize = Math.Min(100, regionRows.Count);

            for (int colIndex = startCol; colIndex <= endCol; colIndex++)
            {
                var sampleCells = new List<SACellValue>();
                var numberFormats = new List<string?>();
                var absoluteRowIndices = new List<int>();
                var normalizationResults = new List<NormalizationResult>();

                for (int i = 0; i < maxSampleSize; i++)
                {
                    int absoluteRow = region.DataStartRow + i;
                    var cellData = sheetData.GetCellData(absoluteRow, colIndex);

                    var normResult = NormalizeCellValue(cellData.Value, cellData.Metadata?.NumberFormat);
                    normalizationResults.Add(normResult);

                    sampleCells.Add(cellData.Value);
                    numberFormats.Add(cellData.Metadata?.NumberFormat);
                    absoluteRowIndices.Add(absoluteRow);
                }

                if (sampleCells.All(c => c.IsEmpty))
                    continue;

                var analysisResult = _columnAnalysisService.AnalyzeColumn(
                    colIndex,
                    sheetData.ColumnNames[colIndex],
                    sampleCells,
                    numberFormats,
                    customRegion: region
                );

                sheetData.SetColumnMetadata(region.Name, colIndex, analysisResult.ToMetadata());

                // Update cell metadata with normalization results and RegionId
                for (int i = 0; i < absoluteRowIndices.Count; i++)
                {
                    var absoluteRow = absoluteRowIndices[i];
                    var normResult = normalizationResults[i];

                    if (normResult.IsSuccess && normResult.CleanedValue.HasValue)
                    {
                        UpdateCellWithNormalizationResult(sheetData, absoluteRow, colIndex, normResult);
                    }

                    // Set RegionId on cell metadata
                    var currentCell = sheetData.GetCellData(absoluteRow, colIndex);
                    var metadata = currentCell.Metadata ?? new CellMetadata();
                    if (metadata.RegionId != region.Name)
                    {
                        var newMetadata = new CellMetadata
                        {
                            NumberFormat = metadata.NumberFormat,
                            Formula = metadata.Formula,
                            Style = metadata.Style,
                            Validation = metadata.Validation,
                            Currency = metadata.Currency,
                            CustomData = metadata.CustomData,
                            OriginalValue = metadata.OriginalValue,
                            CleanedValue = metadata.CleanedValue,
                            DetectedType = metadata.DetectedType,
                            QualityIssue = metadata.QualityIssue,
                            RegionId = region.Name
                        };
                        sheetData.SetCellData(absoluteRow, colIndex, new SACellData(currentCell.Value, newMetadata));
                    }
                }

                // Step 3: Re-normalize anomalous cells using dominant column type
                if (analysisResult.Anomalies.Count > 0 && analysisResult.TypeConfidence >= 0.5)
                {
                    ReNormalizeAnomalousCells(
                        sheetData, colIndex, analysisResult, absoluteRowIndices, sampleCells, numberFormats);
                }

                foreach (var anomaly in analysisResult.Anomalies)
                {
                    var error = CreateExcelErrorFromAnomaly(sheetData.SheetName, colIndex, anomaly, absoluteRowIndices);
                    errors.Add(error);
                }

                _logger.LogInfo(
                    $"[REGION ENRICHMENT] Region '{region.Name}' Column '{sheetData.ColumnNames[colIndex]}' (idx={colIndex}): " +
                    $"Type={analysisResult.DetectedType}, Confidence={analysisResult.TypeConfidence:F2}, " +
                    $"Samples={sampleCells.Count}, Anomalies={analysisResult.Anomalies.Count}",
                    "SheetAnalysisOrchestrator");
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

                    // Normalize cell value and preserve full result (for export)
                    var normResult = NormalizeCellValue(cellData.Value, cellData.Metadata?.NumberFormat);
                    normalizationResults.Add(normResult);

                    // IMPORTANT: Pass ORIGINAL values to ColumnAnalysisService for anomaly detection
                    // If we pass normalized values, dirty data gets "cleaned" and anomalies are masked
                    sampleCells.Add(cellData.Value);
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

                // Log informative message when type is Unknown due to sparse data
                if (analysisResult.DetectedType == DataType.Unknown && sheetData.DataRowCount > maxSampleSize)
                {
                    var nonEmptyCount = sampleCells.Count(c => !c.IsEmpty);
                    if (nonEmptyCount == 0)
                    {
                        _logger.LogInfo(
                            $"Column '{sheetData.ColumnNames[colIndex]}' has Unknown type: no data found in first {maxSampleSize} rows " +
                            $"(file has {sheetData.DataRowCount} data rows - values may exist beyond sample range)",
                            "SheetAnalysisOrchestrator");
                    }
                }

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

                // Step 3: Re-normalize anomalous cells using dominant column type
                // Cells whose type doesn't match the column's dominant type are re-normalized
                // with the column type as context (e.g., integer 36837 in a Date column → DateTime)
                if (analysisResult.Anomalies.Count > 0 && analysisResult.TypeConfidence >= 0.5)
                {
                    ReNormalizeAnomalousCells(
                        sheetData, colIndex, analysisResult, absoluteRowIndices, sampleCells, numberFormats);
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
        /// Re-normalizes cells flagged as anomalies using the column's dominant type as context.
        /// For example: an integer 36837 in a Date column gets converted to a DateTime.
        /// Only re-normalizes when the conversion produces a meaningful result.
        /// </summary>
        private void ReNormalizeAnomalousCells(
            SASheetData sheetData,
            int colIndex,
            ColumnAnalysisResult analysisResult,
            List<int> absoluteRowIndices,
            List<SACellValue> sampleCells,
            List<string?> numberFormats)
        {
            var dominantType = analysisResult.DetectedType;
            int reNormalizedCount = 0;

            foreach (var anomaly in analysisResult.Anomalies)
            {
                // Map sample-relative index to absolute row
                if (anomaly.RowIndex < 0 || anomaly.RowIndex >= absoluteRowIndices.Count)
                    continue;

                int absoluteRow = absoluteRowIndices[anomaly.RowIndex];
                var cellValue = sampleCells[anomaly.RowIndex];
                var format = anomaly.RowIndex < numberFormats.Count ? numberFormats[anomaly.RowIndex] : null;

                // Attempt re-normalization based on dominant type
                var reNormResult = TryReNormalize(cellValue, format, dominantType);
                if (reNormResult == null)
                    continue;

                UpdateCellWithNormalizationResult(sheetData, absoluteRow, colIndex, reNormResult);
                reNormalizedCount++;
            }

            if (reNormalizedCount > 0)
            {
                _logger.LogInfo(
                    $"[RE-NORMALIZATION] Column '{sheetData.ColumnNames[colIndex]}': " +
                    $"re-normalized {reNormalizedCount} anomalous cells to {dominantType}",
                    "SheetAnalysisOrchestrator");
            }
        }

        /// <summary>
        /// Attempts to re-normalize a cell value to match the column's dominant type.
        /// Returns null if conversion is not possible or not meaningful.
        /// </summary>
        private NormalizationResult? TryReNormalize(
            SACellValue cellValue, string? numberFormat, DataType dominantType)
        {
            if (cellValue.IsEmpty)
                return null;

            switch (dominantType)
            {
                case DataType.Date:
                    return TryReNormalizeToDate(cellValue);

                case DataType.Number:
                case DataType.Currency:
                    return TryReNormalizeToNumber(cellValue, numberFormat);

                default:
                    // For other dominant types (Text, Boolean, etc.) the per-cell
                    // normalization from step 1 is sufficient
                    return null;
            }
        }

        /// <summary>
        /// Converts a numeric value to DateTime (Excel serial date).
        /// Handles integers and floats that are date serial numbers without date format.
        /// </summary>
        private NormalizationResult? TryReNormalizeToDate(SACellValue cellValue)
        {
            double? serial = null;

            if (cellValue.IsInteger)
                serial = cellValue.AsInteger();
            else if (cellValue.IsFloatingPoint)
                serial = cellValue.AsFloatingPoint();
            else if (cellValue.IsText)
            {
                // Text that could be a date string (e.g., "10/07/2000")
                var text = cellValue.AsText();
                if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    var cleaned = SACellValue.FromDateTime(parsedDate);
                    return NormalizationResult.SuccessWithWarning(
                        cellValue, cleaned, DataType.Date, DataQualityIssue.TypeMismatch);
                }
                return null;
            }

            if (serial == null)
                return null;

            // Validate serial date range (1 = Jan 1, 1900 ... 2958465 = Dec 31, 9999)
            if (serial < 1 || serial > 2958465)
                return null;

            // Use the normalization service to convert serial → DateTime
            // Pass a synthetic date format so Normalize recognizes it as a date
            var floatValue = SACellValue.FromFloatingPoint(serial.Value);
            var result = _normalizationService.Normalize(serial.Value, "yyyy-MM-dd");
            if (result.IsSuccess && result.CleanedValue.HasValue)
            {
                return NormalizationResult.SuccessWithWarning(
                    cellValue, result.CleanedValue.Value, DataType.Date, DataQualityIssue.TypeMismatch);
            }

            return null;
        }

        /// <summary>
        /// Converts a text value to a number when the column is numeric.
        /// </summary>
        private NormalizationResult? TryReNormalizeToNumber(SACellValue cellValue, string? numberFormat)
        {
            if (!cellValue.IsText)
                return null;

            // Already handled by step 1 NormalizeText, but re-try with explicit number context
            var result = _normalizationService.Normalize(cellValue.AsText(), numberFormat);
            if (result.IsSuccess && result.CleanedValue.HasValue &&
                (result.DetectedType == DataType.Number || result.DetectedType == DataType.Currency))
            {
                return NormalizationResult.SuccessWithWarning(
                    cellValue, result.CleanedValue.Value, result.DetectedType, DataQualityIssue.TypeMismatch);
            }

            return null;
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
                : original.IsFloatingPoint ? original.AsFloatingPoint()
                : original.IsInteger ? original.AsInteger()
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
