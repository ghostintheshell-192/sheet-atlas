using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Efficient sheet storage with flat array architecture.
    /// Uses single contiguous SACellData[] instead of List of arrays.
    /// Benefits: zero fragmentation, cache-friendly, GC can release memory properly.
    /// Memory: ~2-3x overhead instead of 10-14x like DataTable.
    /// </summary>
    public class SASheetData : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Sheet name as it appears in Excel file.
        /// </summary>
        public string SheetName { get; }

        /// <summary>
        /// Column names (headers from first row).
        /// </summary>
        public string[] ColumnNames { get; }

        /// <summary>
        /// Flat array of all cells: cells[row * ColumnCount + col].
        /// Single contiguous allocation = zero fragmentation, excellent cache locality.
        /// </summary>
        private SACellData[] _cells;

        /// <summary>
        /// Current number of rows stored.
        /// </summary>
        private int _rowCount;

        /// <summary>
        /// Initial capacity (rows) when not known upfront.
        /// </summary>
        private const int DefaultInitialCapacity = 100;

        // Optional metadata (lazy-loaded, not allocated until needed)
        private Dictionary<string, MergedRange>? _mergedCells;
        private Dictionary<int, ColumnMetadata>? _columnMetadata;

        public SASheetData(string sheetName, string[] columnNames, int initialCapacity = DefaultInitialCapacity)
        {
            SheetName = sheetName ?? throw new ArgumentNullException(nameof(sheetName));
            ColumnNames = columnNames ?? throw new ArgumentNullException(nameof(columnNames));

            if (columnNames.Length == 0)
                throw new ArgumentException("Column names cannot be empty", nameof(columnNames));

            // Allocate flat array: initialCapacity rows * columnCount columns
            _cells = new SACellData[initialCapacity * ColumnNames.Length];
            _rowCount = 0;

            // Metadata collections NOT allocated until first use (lazy)
        }

        /// <summary>
        /// Add a row to the sheet (building phase during file load).
        /// Automatically grows capacity when needed (2x growth).
        /// </summary>
        public void AddRow(SACellData[] rowData)
        {
            if (rowData == null)
                throw new ArgumentNullException(nameof(rowData));

            if (rowData.Length != ColumnNames.Length)
                throw new ArgumentException($"Row has {rowData.Length} cells, expected {ColumnNames.Length}");

            // Check if we need to grow the array
            if (_rowCount * ColumnNames.Length + ColumnNames.Length > _cells.Length)
            {
                GrowCapacity();
            }

            // Copy row data into flat array at correct offset
            int offset = _rowCount * ColumnNames.Length;
            Array.Copy(rowData, 0, _cells, offset, ColumnNames.Length);
            _rowCount++;
        }

        /// <summary>
        /// Grow flat array capacity by 2x when full.
        /// </summary>
        private void GrowCapacity()
        {
            int currentCapacityRows = _cells.Length / ColumnNames.Length;
            int newCapacityRows = currentCapacityRows * 2;

            var newCells = new SACellData[newCapacityRows * ColumnNames.Length];
            Array.Copy(_cells, newCells, _rowCount * ColumnNames.Length);
            _cells = newCells;
        }

        /// <summary>
        /// Trim excess capacity after all rows loaded (optional optimization).
        /// Call after file load complete to reduce memory footprint.
        /// </summary>
        public void TrimExcess()
        {
            int actualSize = _rowCount * ColumnNames.Length;
            if (actualSize < _cells.Length)
            {
                var trimmed = new SACellData[actualSize];
                Array.Copy(_cells, trimmed, actualSize);
                _cells = trimmed;
            }
        }

        /// <summary>
        /// Get cell value at specified position (fast path, no allocation).
        /// Returns SACellValue.Empty if out of bounds.
        /// </summary>
        public SACellValue GetCellValue(int row, int column)
        {
            if (row < 0 || row >= _rowCount)
                return SACellValue.Empty;
            if (column < 0 || column >= ColumnNames.Length)
                return SACellValue.Empty;

            int index = row * ColumnNames.Length + column;
            return _cells[index].EffectiveValue;
        }

        /// <summary>
        /// Get cell metadata at specified position (slow path, rare).
        /// Returns null if no metadata or out of bounds.
        /// </summary>
        public CellMetadata? GetCellMetadata(int row, int column)
        {
            if (row < 0 || row >= _rowCount)
                return null;
            if (column < 0 || column >= ColumnNames.Length)
                return null;

            int index = row * ColumnNames.Length + column;
            return _cells[index].Metadata;
        }

        /// <summary>
        /// Get complete cell data (value + metadata) at specified position.
        /// </summary>
        public SACellData GetCellData(int row, int column)
        {
            if (row < 0 || row >= _rowCount)
                return new SACellData(SACellValue.Empty);
            if (column < 0 || column >= ColumnNames.Length)
                return new SACellData(SACellValue.Empty);

            int index = row * ColumnNames.Length + column;
            return _cells[index];
        }

        /// <summary>
        /// Get entire row as SACellData array (creates new array - use sparingly).
        /// For iteration, prefer GetCellValue(row, col) to avoid allocation.
        /// </summary>
        public SACellData[] GetRow(int row)
        {
            if (row < 0 || row >= _rowCount)
                return Array.Empty<SACellData>();

            var rowData = new SACellData[ColumnNames.Length];
            int offset = row * ColumnNames.Length;
            Array.Copy(_cells, offset, rowData, 0, ColumnNames.Length);
            return rowData;
        }

        /// <summary>
        /// Enumerate all rows without allocation (yields row by row).
        /// Preferred over GetRow() for iteration.
        /// </summary>
        public IEnumerable<RowView> EnumerateRows()
        {
            for (int row = 0; row < _rowCount; row++)
            {
                yield return new RowView(this, row);
            }
        }

        /// <summary>
        /// Number of rows in sheet (excluding header).
        /// </summary>
        public int RowCount => _rowCount;

        /// <summary>
        /// Number of columns in sheet.
        /// </summary>
        public int ColumnCount => ColumnNames.Length;

        /// <summary>
        /// Total number of cells in sheet.
        /// </summary>
        public int CellCount => _rowCount * ColumnNames.Length;

        /// <summary>
        /// Number of cells with metadata (for diagnostics/debugging).
        /// </summary>
        public int MetadataCellCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _rowCount * ColumnNames.Length; i++)
                {
                    if (_cells[i].HasMetadata)
                        count++;
                }
                return count;
            }
        }

        // === Merged Cells Support (Future) ===

        /// <summary>
        /// Merged cell ranges in this sheet.
        /// Lazy-loaded: not allocated until first merge cell added.
        /// </summary>
        public IReadOnlyDictionary<string, MergedRange> MergedCells
        {
            get
            {
                _mergedCells ??= new Dictionary<string, MergedRange>();
                return _mergedCells;
            }
        }

        /// <summary>
        /// Add a merged cell range (lazy allocation).
        /// </summary>
        public void AddMergedCell(string cellRef, MergedRange range)
        {
            _mergedCells ??= new Dictionary<string, MergedRange>();
            _mergedCells[cellRef] = range;
        }

        // === Column Metadata Support (Future) ===

        /// <summary>
        /// Get column metadata (width, hidden state).
        /// Returns null if no metadata set for column.
        /// </summary>
        public ColumnMetadata? GetColumnMetadata(int columnIndex)
        {
            if (_columnMetadata == null)
                return null;

            return _columnMetadata.TryGetValue(columnIndex, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// Set column metadata (lazy allocation).
        /// </summary>
        public void SetColumnMetadata(int columnIndex, ColumnMetadata metadata)
        {
            _columnMetadata ??= new Dictionary<int, ColumnMetadata>();
            _columnMetadata[columnIndex] = metadata;
        }

        /// <summary>
        /// Dispose sheet data (clear flat array and metadata).
        /// Called when file removed or application closed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Clear flat array (single allocation, easy for GC)
                _cells = Array.Empty<SACellData>();
                _rowCount = 0;

                _mergedCells?.Clear();
                _columnMetadata?.Clear();
            }

            _disposed = true;
        }

        /// <summary>
        /// Lightweight row view for iteration without allocation.
        /// Provides indexer access to cells in a row.
        /// </summary>
        public readonly struct RowView
        {
            private readonly SASheetData _sheet;
            private readonly int _row;

            internal RowView(SASheetData sheet, int row)
            {
                _sheet = sheet;
                _row = row;
            }

            public SACellData this[int column] => _sheet.GetCellData(_row, column);
            public int ColumnCount => _sheet.ColumnCount;
        }
    }

    /// <summary>
    /// Merged cell range information.
    /// Represents cells that are merged in Excel (span multiple rows/columns).
    /// </summary>
    public record MergedRange(int StartRow, int StartCol, int EndRow, int EndCol);

    /// <summary>
    /// Column-level metadata (width, hidden state, detected type, etc.).
    /// Shared for all cells in the column.
    /// Extended by Foundation Layer for type detection and quality analysis.
    /// </summary>
    public record ColumnMetadata
    {
        // Original fields
        public double? Width { get; init; }
        public bool IsHidden { get; init; }

        // Foundation Layer extensions
        /// <summary>
        /// Detected data type for column (from Foundation Layer analysis).
        /// </summary>
        public ValueObjects.DataType? DetectedType { get; init; }

        /// <summary>
        /// Type confidence score (0.0 - 1.0). >0.8 = strong type.
        /// </summary>
        public double? TypeConfidence { get; init; }

        /// <summary>
        /// Currency info if column contains monetary values.
        /// </summary>
        public ValueObjects.CurrencyInfo? Currency { get; init; }

        /// <summary>
        /// Number of data quality warnings in column sample.
        /// </summary>
        public int QualityWarningCount { get; init; }
    }
}
