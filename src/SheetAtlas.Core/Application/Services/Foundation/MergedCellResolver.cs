using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Resolves merged cells using configurable strategies.
    /// Implements IMergedCellResolver interface.
    /// </summary>
    public class MergedCellResolver : IMergedCellResolver
    {
        public SASheetData ResolveMergedCells(
            SASheetData sheetData,
            MergeStrategy strategy = MergeStrategy.ExpandValue,
            Action<MergeWarning>? warningCallback = null)
        {
            ArgumentNullException.ThrowIfNull(sheetData);

            // If no merged cells, return original
            if (sheetData.MergedCells.Count == 0)
                return sheetData;

            // Analyze complexity first
            var analysis = AnalyzeMergeComplexity(sheetData.MergedCells);

            // Warn if high complexity
            if (analysis.MergedCellPercentage > 0.20 && warningCallback != null)
            {
                warningCallback(MergeWarning.HighComplexity(
                    analysis.Level,
                    analysis.MergedCellPercentage,
                    $"High merge percentage ({analysis.MergedCellPercentage:P0}) detected"));
            }

            // Create new SASheetData with resolved merges
            var resolvedSheet = ApplyMergeStrategy(sheetData, strategy, warningCallback);

            return resolvedSheet;
        }

        public MergeComplexityAnalysis AnalyzeMergeComplexity(
            IReadOnlyDictionary<string, MergedRange> mergedCells)
        {
            if (mergedCells == null || mergedCells.Count == 0)
            {
                return MergeComplexityAnalysis.Simple(
                    0.0,
                    MergeStrategy.ExpandValue,
                    "No merged cells detected");
            }

            // Calculate total cells involved in merges
            int totalMergedCells = 0;
            int verticalMerges = 0;
            int horizontalMerges = 0;
            int maxRows = 0;
            int maxCols = 0;

            foreach (var range in mergedCells.Values)
            {
                int rows = range.EndRow - range.StartRow + 1;
                int cols = range.EndCol - range.StartCol + 1;
                int cellsInMerge = rows * cols;

                totalMergedCells += cellsInMerge;

                // Track sheet bounds
                maxRows = Math.Max(maxRows, range.EndRow + 1);
                maxCols = Math.Max(maxCols, range.EndCol + 1);

                // Classify merge direction
                if (rows > cols)
                    verticalMerges++;
                else if (cols > rows)
                    horizontalMerges++;
            }

            // Calculate bounding box (actual area containing merges)
            int boundingBoxCells = maxRows * maxCols;
            double mergePercentage = boundingBoxCells > 0
                ? (double)totalMergedCells / boundingBoxCells
                : 0.0;

            // Determine complexity level
            MergeComplexity level;
            MergeStrategy recommendedStrategy;
            string explanation;

            // Chaos if high percentage AND many ranges (not just a single large merge)
            if (mergePercentage > 0.20 && mergedCells.Count >= 5)
            {
                level = MergeComplexity.Chaos;
                recommendedStrategy = MergeStrategy.KeepTopLeft;
                explanation = $"High merge density ({mergePercentage:P0}) with {mergedCells.Count} ranges - use caution";
            }
            else if (verticalMerges > horizontalMerges * 2)
            {
                level = MergeComplexity.Complex;
                recommendedStrategy = MergeStrategy.KeepTopLeft;
                explanation = $"Predominantly vertical merges ({verticalMerges} vertical vs {horizontalMerges} horizontal)";
            }
            else if (verticalMerges > 0)
            {
                level = MergeComplexity.Complex;
                recommendedStrategy = MergeStrategy.ExpandValue;
                explanation = $"Mixed merge patterns ({verticalMerges} vertical, {horizontalMerges} horizontal)";
            }
            else
            {
                level = MergeComplexity.Simple;
                recommendedStrategy = MergeStrategy.ExpandValue;
                explanation = $"Simple horizontal merges only ({mergedCells.Count} ranges)";
            }

            return new MergeComplexityAnalysis
            {
                Level = level,
                MergedCellPercentage = mergePercentage,
                RecommendedStrategy = recommendedStrategy,
                Explanation = explanation,
                VerticalMergeCount = verticalMerges,
                HorizontalMergeCount = horizontalMerges,
                TotalMergeRanges = mergedCells.Count
            };
        }

        private static SASheetData ApplyMergeStrategy(
            SASheetData original,
            MergeStrategy strategy,
            Action<MergeWarning>? warningCallback)
        {
            // Create new SASheetData with same structure
            SASheetData resolved = new(original.SheetName, original.ColumnNames, original.RowCount);

            // Build lookup for merged cells (row,col) -> MergedRange
            var mergeMap = BuildMergeMap(original.MergedCells);

            // Process each row
            for (int row = 0; row < original.RowCount; row++)
            {
                var rowData = new SACellData[original.ColumnCount];

                for (int col = 0; col < original.ColumnCount; col++)
                {
                    var originalCell = original.GetCellData(row, col);

                    // Check if this cell is in a merged range
                    if (mergeMap.TryGetValue((row, col), out var range))
                    {
                        rowData[col] = ApplyCellStrategy(original, range, row, col, strategy);
                    }
                    else
                    {
                        // Not in merge range, copy as-is
                        rowData[col] = originalCell;
                    }
                }

                resolved.AddRow(rowData);
            }

            return resolved;
        }

        private static Dictionary<(int row, int col), MergedRange> BuildMergeMap(
            IReadOnlyDictionary<string, MergedRange> mergedCells)
        {
            Dictionary<(int, int), MergedRange> map = new();

            foreach (var range in mergedCells.Values)
            {
                for (int row = range.StartRow; row <= range.EndRow; row++)
                {
                    for (int col = range.StartCol; col <= range.EndCol; col++)
                    {
                        map[(row, col)] = range;
                    }
                }
            }

            return map;
        }

        private static SACellData ApplyCellStrategy(
            SASheetData original,
            MergedRange range,
            int row,
            int col,
            MergeStrategy strategy)
        {
            bool isTopLeft = (row == range.StartRow && col == range.StartCol);

            switch (strategy)
            {
                case MergeStrategy.ExpandValue:
                    // Replicate top-left value to all cells
                    var sourceValue = original.GetCellValue(range.StartRow, range.StartCol);
                    return new SACellData(sourceValue);

                case MergeStrategy.KeepTopLeft:
                    // Only top-left keeps value, others empty
                    if (isTopLeft)
                        return original.GetCellData(row, col);
                    else
                        return new SACellData(SACellValue.Empty);

                case MergeStrategy.FlattenToString:
                    if (isTopLeft)
                    {
                        // Collect all non-empty values in range
                        List<string> values = new();
                        for (int r = range.StartRow; r <= range.EndRow; r++)
                        {
                            for (int c = range.StartCol; c <= range.EndCol; c++)
                            {
                                var cellValue = original.GetCellValue(r, c);
                                if (!cellValue.IsEmpty)
                                {
                                    values.Add(cellValue.AsText());
                                }
                            }
                        }
                        var concatenated = string.Join(" ", values);
                        return new SACellData(SACellValue.FromText(concatenated));
                    }
                    else
                    {
                        return new SACellData(SACellValue.Empty);
                    }

                case MergeStrategy.TreatAsHeader:
                    // For headers, expand value (best for search)
                    var headerValue = original.GetCellValue(range.StartRow, range.StartCol);
                    return new SACellData(headerValue);

                default:
                    return original.GetCellData(row, col);
            }
        }
    }
}
