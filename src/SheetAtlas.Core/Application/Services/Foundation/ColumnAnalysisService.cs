using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.DTOs;
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

        public HeaderDetectionResult DetectHeaders(
            IReadOnlyList<SACellData[]> firstRows,
            int columnCount,
            DataRegion? customRegion = null)
        {
            // Handle empty rows
            if (firstRows == null || firstRows.Count == 0 || columnCount == 0)
            {
                return new HeaderDetectionResult
                {
                    HeaderRowIndices = Array.Empty<int>(),
                    FirstDataRowIndex = 0,
                    Confidence = 0.0,
                    Reason = "No rows provided"
                };
            }

            // If custom region provided, use it
            if (customRegion != null && customRegion.HeaderStartRow.HasValue)
            {
                var headerIndices = customRegion.HeaderEndRow.HasValue
                    ? Enumerable.Range(customRegion.HeaderStartRow.Value,
                        customRegion.HeaderEndRow.Value - customRegion.HeaderStartRow.Value + 1).ToArray()
                    : new[] { customRegion.HeaderStartRow.Value };

                return new HeaderDetectionResult
                {
                    HeaderRowIndices = headerIndices,
                    FirstDataRowIndex = customRegion.DataStartRow,
                    Confidence = 1.0,
                    Reason = "User-defined region"
                };
            }

            // Auto-detect headers
            return AutoDetectHeaders(firstRows, columnCount);
        }

        #region Type Detection

        private Dictionary<DataType, int> CalculateTypeDistribution(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats)
        {
            var distribution = new Dictionary<DataType, int>();

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var format = i < numberFormats.Count ? numberFormats[i] : null;

                var type = InferCellType(cell, format);

                if (distribution.ContainsKey(type))
                    distribution[type]++;
                else
                    distribution[type] = 1;
            }

            return distribution;
        }

        private DataType InferCellType(SACellValue cell, string? numberFormat)
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
                if (IsPercentageFormat(numberFormat))
                    return DataType.Percentage;

                if (IsCurrencyFormat(numberFormat))
                    return DataType.Currency;

                if (IsDateFormat(numberFormat))
                    return DataType.Date;

                return DataType.Number;
            }

            // Text-based detection
            if (cell.IsText)
            {
                var text = cell.AsText();

                // Excel formula errors
                if (text.StartsWith("#"))
                    return DataType.Error;

                return DataType.Text;
            }

            return DataType.Unknown;
        }

        private DataType DetermineDominantType(Dictionary<DataType, int> distribution)
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
            return currencies.FirstOrDefault();
        }

        #endregion

        #region Anomaly Detection

        private IReadOnlyList<CellAnomaly> DetectAnomalies(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats,
            DataType dominantType)
        {
            var anomalies = new List<CellAnomaly>();

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

                // Skip Unknown (empty cells are handled separately)
                if (cellType == DataType.Unknown)
                {
                    // Empty cell in non-empty column → warning
                    if (dominantType != DataType.Unknown && cell.IsEmpty)
                    {
                        anomalies.Add(new CellAnomaly
                        {
                            RowIndex = i,
                            CellValue = cell,
                            Issue = DataQualityIssue.MissingRequired,
                            ExpectedType = dominantType,
                            ActualType = DataType.Unknown,
                            Message = $"Empty cell in {dominantType} column"
                        });
                    }
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

        private LocalContext GetLocalContext(
            IReadOnlyList<SACellValue> cells,
            IReadOnlyList<string?> numberFormats,
            int currentIndex)
        {
            var context = new LocalContext();
            var types = new List<DataType>();

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

        private CellAnomaly? ClassifyAnomaly(
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
            var currencies = new Dictionary<string, int>();

            for (int i = 0; i < numberFormats.Count; i++)
            {
                var format = numberFormats[i];
                if (format == null) continue;

                var currency = _currencyDetector.DetectCurrency(format);
                if (currency != null)
                {
                    if (currencies.ContainsKey(currency.Code))
                        currencies[currency.Code]++;
                    else
                        currencies[currency.Code] = 1;
                }
            }

            // If more than one currency found → flag as inconsistent
            if (currencies.Count > 1)
            {
                var anomalies = new List<CellAnomaly>();
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

        private double CalculateTypeConfidence(
            Dictionary<DataType, int> distribution,
            DataType dominantType,
            IReadOnlyList<CellAnomaly> anomalies)
        {
            if (distribution.Count == 0 || dominantType == DataType.Unknown)
                return 0.0;

            // Base confidence from type distribution
            int totalCells = distribution.Sum(kvp => kvp.Value);
            int dominantCount = distribution.ContainsKey(dominantType) ? distribution[dominantType] : 0;

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

        #region Header Detection

        private HeaderDetectionResult AutoDetectHeaders(
            IReadOnlyList<SACellData[]> firstRows,
            int columnCount)
        {
            // Strategy: Look for the row where data types become consistent

            // Skip initial blank rows
            int firstNonBlankRow = 0;
            for (int i = 0; i < firstRows.Count; i++)
            {
                if (!IsBlankRow(firstRows[i]))
                {
                    firstNonBlankRow = i;
                    break;
                }
            }

            // If all rows numeric → likely no header
            bool allNumeric = true;
            for (int i = firstNonBlankRow; i < Math.Min(firstNonBlankRow + 3, firstRows.Count); i++)
            {
                if (!IsNumericRow(firstRows[i]))
                {
                    allNumeric = false;
                    break;
                }
            }

            if (allNumeric)
            {
                return new HeaderDetectionResult
                {
                    HeaderRowIndices = Array.Empty<int>(),
                    FirstDataRowIndex = firstNonBlankRow,
                    Confidence = 0.3,
                    Reason = "All rows contain numeric data - no clear header detected"
                };
            }

            // Look for text row followed by consistent data types
            for (int candidateRow = firstNonBlankRow; candidateRow < Math.Min(firstRows.Count - 1, 5); candidateRow++)
            {
                var currentRow = firstRows[candidateRow];

                // Header candidate: mostly text, few blanks
                if (IsLikelyHeaderRow(currentRow))
                {
                    // Check if next rows have consistent data types
                    if (candidateRow + 1 < firstRows.Count)
                    {
                        var nextRow = firstRows[candidateRow + 1];

                        // If next row is also text, might be multi-row header
                        if (IsLikelyHeaderRow(nextRow))
                        {
                            return HeaderDetectionResult.MultiRowHeaders(
                                new[] { candidateRow, candidateRow + 1 },
                                candidateRow + 2,
                                0.7,
                                "Multi-row header detected (text followed by text)");
                        }

                        // Single header
                        return HeaderDetectionResult.SingleHeader(
                            candidateRow,
                            0.8,
                            "Text row followed by data rows");
                    }
                }
            }

            // Fallback: assume first non-blank row is header
            return HeaderDetectionResult.SingleHeader(
                firstNonBlankRow,
                0.5,
                "Assumed first non-blank row as header");
        }

        private bool IsBlankRow(SACellData[] row)
        {
            return row.All(cell => cell.Value.IsEmpty);
        }

        private bool IsNumericRow(SACellData[] row)
        {
            var nonEmptyCells = row.Where(cell => !cell.Value.IsEmpty).ToList();
            if (nonEmptyCells.Count == 0) return false;

            // Check both Integer (long) and Number (double) for numeric detection
            return nonEmptyCells.Count(cell => cell.Value.IsInteger || cell.Value.IsNumber) >= nonEmptyCells.Count * 0.7;
        }

        private bool IsLikelyHeaderRow(SACellData[] row)
        {
            var nonEmptyCells = row.Where(cell => !cell.Value.IsEmpty).ToList();
            if (nonEmptyCells.Count == 0) return false;

            // Header: mostly text, not dates/numbers
            int textCount = nonEmptyCells.Count(cell => cell.Value.IsText);
            return textCount >= nonEmptyCells.Count * 0.6;
        }

        #endregion

        #region Format Helpers

        private bool IsDateFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            var lower = format.ToLowerInvariant();
            return lower.Contains("mm") || lower.Contains("dd") ||
                   lower.Contains("yyyy") || lower.Contains("yy") ||
                   lower.Contains("m/d") || lower.Contains("d/m");
        }

        private bool IsCurrencyFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Contains("$") || format.Contains("€") ||
                   format.Contains("£") || format.Contains("¥") ||
                   format.Contains("₹") || format.Contains("₽");
        }

        private bool IsPercentageFormat(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Contains("%");
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
