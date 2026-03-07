using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Detects DataRegion boundaries in target sheets using header-anchored matching.
    /// Phase 2 algorithm: case-insensitive header match, boundary = empty row or known header pattern.
    /// Fallback: source region row count as maximum extent.
    /// See ADR-012 for design decisions.
    /// </summary>
    public class RegionDetectionService : IRegionDetectionService
    {
        private readonly ILogService _logger;

        public RegionDetectionService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RegionDetectionResult DetectRegion(
            DataRegion sourceRegion,
            SASheetData sourceSheet,
            SASheetData targetSheet)
        {
            ArgumentNullException.ThrowIfNull(sourceRegion);
            ArgumentNullException.ThrowIfNull(sourceSheet);
            ArgumentNullException.ThrowIfNull(targetSheet);

            // Step 1: Extract source header names from the source sheet
            var sourceHeaders = ExtractHeaders(sourceRegion, sourceSheet);
            if (sourceHeaders.Length == 0)
            {
                return new RegionDetectionResult(false, null, "No headers found in source region");
            }

            int startCol = sourceRegion.StartColumn ?? 0;
            int endCol = sourceRegion.EndColumn ?? (sourceSheet.ColumnCount - 1);
            int headerRowCount = sourceRegion.HeaderRowCount;

            // Step 2: Scan target sheet rows for matching headers
            int headerMatchRow = FindHeaderRow(sourceHeaders, startCol, endCol, targetSheet);
            if (headerMatchRow < 0)
            {
                _logger.LogInfo(
                    $"No header match found in target sheet '{targetSheet.SheetName}' for region '{sourceRegion.Name}'",
                    "RegionDetectionService");
                return new RegionDetectionResult(false, null, "Headers not found");
            }

            // Step 3: Calculate data boundaries
            int dataStartRow = headerMatchRow + headerRowCount;
            int sourceDataRowCount = sourceRegion.DataEndRow.HasValue
                ? sourceRegion.DataEndRow.Value - sourceRegion.DataStartRow + 1
                : 0; // 0 = unbounded source, no cap applied
            var (dataEndRow, wasTruncated) = FindDataEndRow(startCol, endCol, dataStartRow, sourceDataRowCount, targetSheet);

            int dataRowCount = dataEndRow >= dataStartRow ? dataEndRow - dataStartRow + 1 : 0;
            string? warningMessage = wasTruncated
                ? $"Boundary capped at source row count ({dataRowCount} rows). Target sheet may have more data — adjust if needed."
                : null;

            var detectedRegion = new DataRegion
            {
                Name = sourceRegion.Name,
                HeaderStartRow = headerMatchRow,
                HeaderEndRow = headerRowCount > 1 ? headerMatchRow + headerRowCount - 1 : headerMatchRow,
                DataStartRow = dataStartRow,
                DataEndRow = dataEndRow >= dataStartRow ? dataEndRow : null,
                StartColumn = startCol,
                EndColumn = endCol,
                IsAutoDetected = true,
                WarningMessage = warningMessage
            };

            string message = $"Headers matched at row {headerMatchRow + 1}, {dataRowCount} data rows detected";
            if (wasTruncated)
                message += " (truncated — target has more data)";
            _logger.LogInfo(
                $"Region '{sourceRegion.Name}' detected in '{targetSheet.SheetName}': {message}",
                "RegionDetectionService");

            return new RegionDetectionResult(true, detectedRegion, message, wasTruncated);
        }

        /// <summary>
        /// Extract header values from the source region's header row(s).
        /// Returns an array of trimmed, non-null header strings for the column range.
        /// </summary>
        private static string[] ExtractHeaders(DataRegion region, SASheetData sheet)
        {
            int headerRow = region.HeaderStartRow ?? region.DataStartRow;
            int startCol = region.StartColumn ?? 0;
            int endCol = region.EndColumn ?? (sheet.ColumnCount - 1);
            int colCount = endCol - startCol + 1;

            var headers = new string[colCount];
            for (int c = 0; c < colCount; c++)
            {
                var value = sheet.GetCellValue(headerRow, startCol + c);
                headers[c] = value.ToString()?.Trim() ?? "";
            }

            return headers;
        }

        /// <summary>
        /// Scan target sheet to find the first row where all columns match the source headers
        /// (case-insensitive). Returns -1 if not found.
        /// </summary>
        private static int FindHeaderRow(string[] sourceHeaders, int startCol, int endCol, SASheetData targetSheet)
        {
            // Only scan rows that could be headers (typically first ~20 rows)
            int maxScanRows = Math.Min(targetSheet.RowCount, 50);

            for (int row = 0; row < maxScanRows; row++)
            {
                if (RowMatchesHeaders(sourceHeaders, startCol, endCol, row, targetSheet))
                    return row;
            }

            return -1;
        }

        /// <summary>
        /// Check if a specific row in the target sheet matches all source headers.
        /// All columns must match (case-insensitive, trimmed).
        /// </summary>
        private static bool RowMatchesHeaders(string[] sourceHeaders, int startCol, int endCol, int row, SASheetData sheet)
        {
            // Ensure target sheet has enough columns
            if (endCol >= sheet.ColumnCount)
                return false;

            int colCount = endCol - startCol + 1;
            if (colCount != sourceHeaders.Length)
                return false;

            for (int c = 0; c < colCount; c++)
            {
                var targetValue = sheet.GetCellValue(row, startCol + c).ToString()?.Trim() ?? "";
                if (!string.Equals(sourceHeaders[c], targetValue, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Find the last data row by scanning downward from dataStartRow.
        /// Stops at first completely empty row (definite break).
        /// If no empty row is found, applies source row count as maximum extent (fallback cap).
        /// Returns (lastDataRow, wasTruncated) where wasTruncated indicates the cap was applied
        /// and the target sheet had more non-empty rows beyond the boundary.
        /// </summary>
        private static (int lastDataRow, bool wasTruncated) FindDataEndRow(
            int startCol, int endCol,
            int dataStartRow, int sourceDataRowCount, SASheetData sheet)
        {
            int lastNonEmptyRow = dataStartRow - 1;
            bool foundDefiniteBreak = false;

            for (int row = dataStartRow; row < sheet.RowCount; row++)
            {
                if (IsRowEmpty(startCol, endCol, row, sheet))
                {
                    foundDefiniteBreak = true;
                    break;
                }

                lastNonEmptyRow = row;
            }

            // Fallback: if no definite break found, cap at source row count
            bool wasTruncated = false;
            if (!foundDefiniteBreak && sourceDataRowCount > 0)
            {
                int maxRow = dataStartRow + sourceDataRowCount - 1;
                if (lastNonEmptyRow > maxRow)
                {
                    lastNonEmptyRow = maxRow;
                    wasTruncated = true;
                }
            }

            return (lastNonEmptyRow, wasTruncated);
        }

        /// <summary>
        /// Check if all cells in the column range of a row are empty.
        /// </summary>
        private static bool IsRowEmpty(int startCol, int endCol, int row, SASheetData sheet)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                var value = sheet.GetCellValue(row, c);
                if (!value.IsEmpty)
                    return false;
            }

            return true;
        }
    }
}
