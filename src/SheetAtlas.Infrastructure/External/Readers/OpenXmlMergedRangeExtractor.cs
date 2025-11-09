using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Extracts merged cell range information from OpenXML (.xlsx) worksheets.
    /// Implements generic IMergedRangeExtractor for WorksheetPart context.
    /// </summary>
    public class OpenXmlMergedRangeExtractor : IMergedRangeExtractor<WorksheetPart>
    {
        private readonly ICellReferenceParser _cellParser;

        public OpenXmlMergedRangeExtractor(ICellReferenceParser cellParser)
        {
            _cellParser = cellParser ?? throw new ArgumentNullException(nameof(cellParser));
        }

        /// <summary>
        /// Extracts merged cell ranges from OpenXML worksheet.
        /// Returns structural information only (no value expansion).
        /// Reports invalid range references as warnings to the errors collection.
        /// </summary>
        public MergedRange[] ExtractMergedRanges(WorksheetPart worksheetPart, string sheetName, List<ExcelError> errors)
        {
            ArgumentNullException.ThrowIfNull(worksheetPart);
            ArgumentNullException.ThrowIfNull(errors);

            var mergeCellsElement = worksheetPart.Worksheet.Elements<MergeCells>().FirstOrDefault();
            if (mergeCellsElement == null)
                return Array.Empty<MergedRange>();

            var ranges = new List<MergedRange>();

            foreach (var mergeCell in mergeCellsElement.Elements<MergeCell>())
            {
                if (mergeCell.Reference?.Value == null)
                    continue;

                var cellReference = mergeCell.Reference.Value;
                var range = ParseMergedRange(cellReference, sheetName, errors);

                if (range != null)
                    ranges.Add(range);
            }

            return ranges.ToArray();
        }

        /// <summary>
        /// Parses OpenXML merge cell reference (e.g., "A1:C3") to MergedRange.
        /// Returns null if format is invalid or cell references cannot be parsed.
        /// Reports validation errors as warnings to the errors collection.
        /// </summary>
        private MergedRange? ParseMergedRange(string cellReference, string sheetName, List<ExcelError> errors)
        {
            var parts = cellReference.Split(':');
            if (parts.Length != 2)
            {
                errors.Add(ExcelError.Warning(
                    $"Sheet:{sheetName}",
                    $"Invalid merge range format: '{cellReference}' (expected format: A1:B2)"));
                return null;
            }

            var startCell = parts[0];
            var endCell = parts[1];

            // Parse start coordinates
            // Note: CellReferenceParser returns -1 for invalid column, 0 for invalid row
            int startCol = _cellParser.GetColumnIndex(startCell);
            int startRow = _cellParser.GetRowIndex(startCell);

            // Parse end coordinates
            int endCol = _cellParser.GetColumnIndex(endCell);
            int endRow = _cellParser.GetRowIndex(endCell);

            // Validate parsed values (CellReferenceParser returns fallback values instead of throwing)
            if (startCol < 0 || startRow < 0 || endCol < 0 || endRow < 0)
            {
                errors.Add(ExcelError.Warning(
                    $"Sheet:{sheetName}",
                    $"Invalid cell reference in merge range '{cellReference}': " +
                    $"could not parse '{startCell}' or '{endCell}'"));
                return null;
            }

            // Validate range is valid (end >= start)
            if (endRow < startRow || endCol < startCol)
            {
                errors.Add(ExcelError.Warning(
                    $"Sheet:{sheetName}",
                    $"Invalid merge range '{cellReference}': end cell ({endRow},{endCol}) is before start cell ({startRow},{startCol})"));
                return null;
            }

            return new MergedRange(startRow, startCol, endRow, endCol);
        }
    }
}
