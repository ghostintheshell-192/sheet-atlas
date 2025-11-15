using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Utilities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Analyzes column characteristics: data type, confidence, generates ColumnMetadata.
    /// Implements IColumnAnalysisService interface.
    /// </summary>
    public class ColumnAnalysisService : IColumnAnalysisService
    {
        private readonly ICurrencyDetector _currencyDetector;
        private const int ContextWindowSize = 3; // ±3 cells for local context

        public ColumnAnalysisService(ICurrencyDetector currencyDetector)
        {
            _currencyDetector = currencyDetector ?? throw new ArgumentNullException(nameof(currencyDetector));
        }

        // Parameterless constructor for tests (uses default CurrencyDetector)
        public ColumnAnalysisService() : this(new CurrencyDetector())
        {
        }

        public ColumnAnalysisResult AnalyzeColumn(
            int columnIndex,
            string columnName,
            IReadOnlyList<SACellValue> sampleCells,
            IReadOnlyList<string?> numberFormats,
            DataRegion? customRegion = null)
        {
            // Handle empty sample
            if (sampleCells == null || sampleCells.Count == 0)
            {
                return new ColumnAnalysisResult
                {
                    ColumnIndex = columnIndex,
                    ColumnName = columnName,
                    DetectedType = DataType.Unknown,
                    TypeConfidence = 0.0,
                    TypeDistribution = new Dictionary<DataType, int>(),
                    SampleValues = Array.Empty<SACellValue>(),
                    Anomalies = Array.Empty<CellAnomaly>()
                };
            }

            // 1. Calculate type distribution
            var typeDistribution = CalculateTypeDistribution(sampleCells, numberFormats);

            // 2. Determine dominant type
            var dominantType = DetermineDominantType(typeDistribution);

            // 3. Detect currency if applicable
            CurrencyInfo? currency = null;
            if (dominantType == DataType.Currency || dominantType == DataType.Number)
            {
                currency = DetectCurrency(numberFormats);
                // If currency detected, update dominant type
                if (currency != null && dominantType == DataType.Number)
                    dominantType = DataType.Currency;
            }

            // 4. Detect anomalies with context-aware analysis
            var anomalies = DetectAnomalies(sampleCells, numberFormats, dominantType);

            // 5. Calculate confidence score with penalty for anomalies
            var confidence = CalculateTypeConfidence(typeDistribution, dominantType, anomalies);

            return new ColumnAnalysisResult
            {
                ColumnIndex = columnIndex,
                ColumnName = columnName,
                DetectedType = dominantType,
                TypeConfidence = confidence,
                Currency = currency,
                TypeDistribution = typeDistribution,
                SampleValues = sampleCells,
                Anomalies = anomalies
            };
        }

        #region Type Detection

        private Dictionary<DataType, int> CalculateTypeDistribution(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats)
        {
            Dictionary<DataType, int> distribution = new();

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var format = i < numberFormats.Count ? numberFormats[i] : null;

                var type = InferCellType(cell, format);

                if (distribution.TryGetValue(type, out int value))
                    distribution[type] = ++value;
                else
                    distribution[type] = 1;
            }

            return distribution;
        }

        private static DataType InferCellType(SACellValue cell, string? numberFormat)
        {
            // Empty cells
            if (cell.IsEmpty)
                return DataType.Unknown;

            // Boolean
            if (cell.IsBoolean)
                return DataType.Boolean;

            // DateTime
            if (cell.IsDateTime)
                return DataType.Date;

            // Numeric types (both Integer and Number/floating-point)
            // NOTE: SACellValue distinguishes Integer (long) from Number (double) for memory efficiency
            //       but for column analysis, both are treated as numeric data types
            if (cell.IsInteger || cell.IsNumber)
            {
                // Check format for specific type
                if (NumberFormatHelper.IsPercentageFormat(numberFormat))
                    return DataType.Percentage;

                if (NumberFormatHelper.IsCurrencyFormat(numberFormat))
                    return DataType.Currency;

                if (NumberFormatHelper.IsDateFormat(numberFormat))
                    return DataType.Date;

                return DataType.Number;
            }

            // Text-based detection
            if (cell.IsText)
            {
                var text = cell.AsText();

                // Excel formula errors
                if (text.StartsWith('#'))
                    return DataType.Error;

                return DataType.Text;
            }

            return DataType.Unknown;
        }

        private static DataType DetermineDominantType(Dictionary<DataType, int> distribution)
        {
            if (distribution.Count == 0)
                return DataType.Unknown;

            // Exclude Unknown and Error from dominance calculation
            // Error is not a legitimate data type, it's a data quality issue
            var validTypes = distribution.Where(kvp => kvp.Key != DataType.Unknown && kvp.Key != DataType.Error).ToList();

            if (validTypes.Count == 0)
                return DataType.Unknown;

            return validTypes.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        #endregion

        #region Currency Detection

        private CurrencyInfo? DetectCurrency(IReadOnlyList<string?> numberFormats)
        {
            var currencies = _currencyDetector.DetectMixedCurrencies(
                numberFormats.Where(f => f != null).Cast<string>());

            // Return first currency found, or null if none
            return currencies.Count > 0 ? currencies[0] : null;
        }

        #endregion

        #region Anomaly Detection

        private IReadOnlyList<CellAnomaly> DetectAnomalies(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats,
            DataType dominantType)
        {
            List<CellAnomaly> anomalies = new();

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var format = i < numberFormats.Count ? numberFormats[i] : null;
                var cellType = InferCellType(cell, format);

                // Excel formula errors are ALWAYS anomalies (not a legitimate data type)
                if (cellType == DataType.Error && cell.IsText)
                {
                    var errorCode = cell.AsText();
                    anomalies.Add(CellAnomaly.FormulaError(i, cell, dominantType, errorCode));
                    continue;
                }

                // Skip if type matches dominant
                if (cellType == dominantType)
                    continue;

                // Skip Unknown (empty cells are not considered anomalies)
                // Empty cells are legitimate in spreadsheets (optional fields, missing data, etc.)
                // For business-critical validation (required fields), implement separate validation layer
                if (cellType == DataType.Unknown)
                {
                    continue;
                }

                // Check if anomaly is supported by local context
                var localContext = GetLocalContext(cells, numberFormats, i);
                var anomaly = ClassifyAnomaly(cell, format, cellType, dominantType, localContext, i);

                if (anomaly != null)
                    anomalies.Add(anomaly);
            }

            // Detect mixed currency formats
            if (dominantType == DataType.Currency)
            {
                var currencyAnomalies = DetectMixedCurrencyFormats(cells, numberFormats);
                anomalies.AddRange(currencyAnomalies);
            }

            return anomalies.AsReadOnly();
        }

        private static LocalContext GetLocalContext(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats,
            int currentIndex)
        {
            LocalContext context = new();
            List<DataType> types = new();

            int startIndex = Math.Max(0, currentIndex - ContextWindowSize);
            int endIndex = Math.Min(cells.Count - 1, currentIndex + ContextWindowSize);

            for (int i = startIndex; i <= endIndex; i++)
            {
                if (i == currentIndex)
                    continue; // Skip current cell

                var format = i < numberFormats.Count ? numberFormats[i] : null;
                var type = InferCellType(cells[i], format);

                if (type != DataType.Unknown)
                    types.Add(type);
            }

            context.LocalTypes = types;
            context.LocalDominantType = types.Count > 0
                ? types.GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key
                : DataType.Unknown;

            return context;
        }

        private static CellAnomaly? ClassifyAnomaly(
            SACellValue cell,
            string? format,
            DataType actualType,
            DataType expectedType,
            LocalContext localContext,
            int rowIndex)
        {
            // Formula errors → Warning
            if (actualType == DataType.Error && cell.IsText)
            {
                var errorCode = cell.AsText();
                return CellAnomaly.FormulaError(rowIndex, cell, expectedType, errorCode);
            }

            // Type mismatch with local context consideration
            if (actualType != expectedType)
            {
                // If local context supports the actual type, it might be a section change (footer/header)
                // Still flag it, but as type mismatch
                return CellAnomaly.TypeMismatch(
                    rowIndex,
                    cell,
                    expectedType,
                    actualType,
                    $"Expected {expectedType}, found {actualType}");
            }

            return null;
        }

        private IEnumerable<CellAnomaly> DetectMixedCurrencyFormats(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats)
        {
            Dictionary<string, int> currencies = new();

            for (int i = 0; i < numberFormats.Count; i++)
            {
                var format = numberFormats[i];
                if (format == null) continue;

                var currency = _currencyDetector.DetectCurrency(format);
                if (currency != null)
                {
                    if (currencies.TryGetValue(currency.Code, out int value))
                        currencies[currency.Code] = ++value;
                    else
                        currencies[currency.Code] = 1;
                }
            }

            // If more than one currency found → flag as inconsistent
            if (currencies.Count > 1)
            {
                List<CellAnomaly> anomalies = new();
                var dominantCurrency = currencies.OrderByDescending(kvp => kvp.Value).First().Key;

                for (int i = 0; i < numberFormats.Count; i++)
                {
                    var format = numberFormats[i];
                    if (format == null) continue;

                    var currency = _currencyDetector.DetectCurrency(format);
                    if (currency != null && currency.Code != dominantCurrency)
                    {
                        anomalies.Add(new CellAnomaly
                        {
                            RowIndex = i,
                            CellValue = i < cells.Count ? cells[i] : SACellValue.Empty,
                            Issue = DataQualityIssue.InconsistentFormat,
                            ExpectedType = DataType.Currency,
                            ActualType = DataType.Currency,
                            Message = $"Mixed currency format: expected {dominantCurrency}, found {currency.Code}"
                        });
                    }
                }

                return anomalies;
            }

            return Enumerable.Empty<CellAnomaly>();
        }

        #endregion

        #region Confidence Calculation

        private static double CalculateTypeConfidence(
            Dictionary<DataType, int> distribution,
            DataType dominantType,
            IReadOnlyList<CellAnomaly> anomalies)
        {
            if (distribution.Count == 0 || dominantType == DataType.Unknown)
                return 0.0;

            // Base confidence from type distribution
            int totalCells = distribution.Sum(kvp => kvp.Value);
            int dominantCount = distribution.TryGetValue(dominantType, out int value) ? value : 0;

            double baseConfidence = (double)dominantCount / totalCells;

            // Apply penalty for anomalies (weighted by severity)
            double penalty = 0.0;
            foreach (var anomaly in anomalies)
            {
                penalty += anomaly.Severity switch
                {
                    LogSeverity.Info => 0.0,      // No penalty
                    LogSeverity.Warning => 0.02,  // -2% per warning
                    LogSeverity.Error => 0.05,    // -5% per error
                    LogSeverity.Critical => 0.10, // -10% per critical
                    _ => 0.0
                };
            }

            return Math.Max(0.0, Math.Min(1.0, baseConfidence - penalty));
        }

        #endregion


        #region Helper Classes

        private class LocalContext
        {
            public List<DataType> LocalTypes { get; set; } = new();
            public DataType LocalDominantType { get; set; }
        }

        #endregion
    }
}
