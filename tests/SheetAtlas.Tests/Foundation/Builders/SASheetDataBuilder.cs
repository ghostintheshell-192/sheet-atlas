using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Foundation.Builders
{
    /// <summary>
    /// Fluent builder for constructing SASheetData test objects.
    /// Provides convenient API for setting up test fixtures.
    /// </summary>
    /// <example>
    /// var sheet = new SASheetDataBuilder()
    ///     .WithName("TestData")
    ///     .WithColumns(new[] { "Name", "Amount", "Date" })
    ///     .WithRows(10)
    ///     .WithCellValue(0, 1, 1234.56m)
    ///     .WithCellValue(0, 2, "2024-11-05")
    ///     .Build();
    /// </example>
    public class SASheetDataBuilder
    {
        private string _sheetName = "Sheet1";
        private string[] _columnNames = Array.Empty<string>();
        private int _rowCount = 0;
        private int _columnCount = 0;
        private readonly Dictionary<(int row, int col), object?> _cellValues = new();
        private readonly Dictionary<string, Core.Domain.Entities.MergedRange> _mergedCells = new();
        private readonly Dictionary<int, ColumnMetadata> _columnMetadata = new();
        private int _initialCapacity = 100;

        public SASheetDataBuilder WithName(string sheetName)
        {
            _sheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            return this;
        }

        public SASheetDataBuilder WithColumns(params string[] columnNames)
        {
            if (columnNames == null || columnNames.Length == 0)
                throw new ArgumentException("Column names cannot be empty", nameof(columnNames));

            _columnNames = columnNames;
            _columnCount = columnNames.Length;
            return this;
        }

        public SASheetDataBuilder WithColumnCount(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Column count must be > 0", nameof(count));

            _columnCount = count;
            // Auto-generate column names if not provided
            if (_columnNames.Length == 0)
            {
                _columnNames = Enumerable.Range(0, count)
                    .Select(i => $"Column{i + 1}")
                    .ToArray();
            }
            return this;
        }

        public SASheetDataBuilder WithRows(int count)
        {
            if (count < 0)
                throw new ArgumentException("Row count cannot be negative", nameof(count));

            _rowCount = count;
            return this;
        }

        public SASheetDataBuilder WithInitialCapacity(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentException("Capacity must be > 0", nameof(capacity));

            _initialCapacity = capacity;
            return this;
        }

        public SASheetDataBuilder WithCellValue(int rowIndex, int columnIndex, object? value)
        {
            if (rowIndex < 0 || columnIndex < 0)
                throw new ArgumentException("Row and column indices must be non-negative");

            if (columnIndex >= _columnCount && _columnCount > 0)
                throw new ArgumentException($"Column index {columnIndex} exceeds column count {_columnCount}");

            _cellValues[(rowIndex, columnIndex)] = value;
            return this;
        }

        public SASheetDataBuilder WithMergedCells(string range, int startRow, int startCol, int endRow, int endCol)
        {
            if (string.IsNullOrEmpty(range))
                throw new ArgumentException("Range cannot be empty", nameof(range));

            _mergedCells[range] = new Core.Domain.Entities.MergedRange(startRow, startCol, endRow, endCol);
            return this;
        }

        /// <summary>
        /// Simplified overload for horizontal merges (common case: header rows).
        /// Parses simple ranges like "A1:C1" into coordinates.
        /// </summary>
        public SASheetDataBuilder WithMergedCells(string range, object? value = null)
        {
            if (string.IsNullOrEmpty(range))
                throw new ArgumentException("Range cannot be empty", nameof(range));

            // Simple parser for ranges like "A1:C1"
            var (startRow, startCol, endRow, endCol) = ParseSimpleRange(range);
            _mergedCells[range] = new Core.Domain.Entities.MergedRange(startRow, startCol, endRow, endCol);
            return this;
        }

        private static (int startRow, int startCol, int endRow, int endCol) ParseSimpleRange(string range)
        {
            // Parse ranges like "A1:C1", "A1:A3", etc.
            var parts = range.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid range format: {range}. Expected format: A1:C1");

            var start = ParseCellRef(parts[0]);
            var end = ParseCellRef(parts[1]);
            return (start.row, start.col, end.row, end.col);
        }

        private static (int row, int col) ParseCellRef(string cellRef)
        {
            // Parse cell references like "A1", "Z10", "AA100"
            int i = 0;
            int col = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                col = col * 26 + (char.ToUpper(cellRef[i]) - 'A' + 1);
                i++;
            }
            col--; // Convert to 0-based

            int row = int.Parse(cellRef.Substring(i)) - 1; // Convert to 0-based
            return (row, col);
        }

        public SASheetDataBuilder WithColumnMetadata(int columnIndex, ColumnMetadata metadata)
        {
            if (columnIndex < 0)
                throw new ArgumentException("Column index must be non-negative", nameof(columnIndex));

            ArgumentNullException.ThrowIfNull(metadata);

            _columnMetadata[columnIndex] = metadata;
            return this;
        }

        public SASheetData Build()
        {
            // Validate configuration
            if (_columnNames.Length == 0 && _columnCount == 0)
                throw new InvalidOperationException("Must specify columns via WithColumns() or WithColumnCount()");

            if (_columnNames.Length == 0)
                _columnNames = Enumerable.Range(0, _columnCount)
                    .Select(i => $"Column{i + 1}")
                    .ToArray();

            _columnCount = _columnNames.Length;

            // Create sheet
            var sheet = new SASheetData(_sheetName, _columnNames, _initialCapacity);

            // Add rows
            for (int r = 0; r < _rowCount; r++)
            {
                var row = new SACellData[_columnCount];

                for (int c = 0; c < _columnCount; c++)
                {
                    var hasValue = _cellValues.TryGetValue((r, c), out var cellValue);
                    var cellValueObj = hasValue ? ConvertToSACellValue(cellValue) : SACellValue.Empty;
                    row[c] = new SACellData(cellValueObj);
                }

                sheet.AddRow(row);
            }

            // Apply merged cells
            foreach (var (range, mergeInfo) in _mergedCells)
            {
                sheet.AddMergedCell(range, mergeInfo);
            }

            // Apply column metadata
            foreach (var (columnIndex, metadata) in _columnMetadata)
            {
                sheet.SetColumnMetadata(columnIndex, metadata);
            }

            return sheet;
        }

        private static SACellValue ConvertToSACellValue(object? value)
        {
            return value switch
            {
                null => SACellValue.Empty,
                double d => SACellValue.FromFloatingPoint(d),
                decimal m => SACellValue.FromFloatingPoint((double)m),
                int i => SACellValue.FromInteger(i),
                long l => SACellValue.FromInteger(l),
                bool b => SACellValue.FromBoolean(b),
                DateTime dt => SACellValue.FromDateTime(dt),
                string s => SACellValue.FromText(s),
                _ => SACellValue.FromText(value.ToString() ?? string.Empty)
            };
        }
    }
}
