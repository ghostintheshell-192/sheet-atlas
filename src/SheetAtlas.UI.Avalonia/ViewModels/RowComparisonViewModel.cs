using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.Managers;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels
{
    public class RowComparisonViewModel : ViewModelBase, IDisposable
    {
        private readonly ILogService _logger;
        private readonly IThemeManager? _themeManager;
        private RowComparison? _comparison;

        private bool _disposed = false;
        private ObservableCollection<RowComparisonColumnViewModel> _columns = new();
        private List<RowComparisonCellViewModel> _allCells = new(); // Flat cache for O(n) theme refresh

        public RowComparison? Comparison
        {
            get => _comparison;
            set
            {
                if (SetField(ref _comparison, value))
                {
                    RefreshColumns();
                }
            }
        }

        public ObservableCollection<RowComparisonColumnViewModel> Columns
        {
            get => _columns;
            set => SetField(ref _columns, value);
        }

        public string Title => Comparison?.Name ?? "Row Comparison";
        public int RowCount => Comparison?.Rows.Count ?? 0;
        public bool HasRows => RowCount > 0;
        public DateTime CreatedAt => Comparison?.CreatedAt ?? DateTime.MinValue;

        public ICommand CloseCommand { get; }

        public event EventHandler? CloseRequested;

        public RowComparisonViewModel(ILogService logger, IThemeManager? themeManager = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _themeManager = themeManager;

            CloseCommand = new RelayCommand(() =>
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            });

            if (_themeManager != null)
            {
                _themeManager.ThemeChanged += OnThemeChanged;
            }
        }

        public RowComparisonViewModel(RowComparison comparison, ILogService logger, IThemeManager? themeManager = null)
            : this(logger, themeManager)
        {
            Comparison = comparison;
        }

        private void OnThemeChanged(object? sender, Theme newTheme)
        {
            // Force re-evaluation of all cell background bindings
            RefreshCellColors();
            _logger.LogInfo($"Refreshed cell colors for theme: {newTheme}", "RowComparisonViewModel");
        }

        private void RefreshCellColors()
        {
            // Use flat cache for O(n) instead of O(n×m) nested iteration
            // Same pattern as SearchHistoryItem - much faster on theme change
            foreach (var cell in _allCells)
            {
                cell.RefreshColors();
            }
        }

        private void RefreshColumns()
        {
            Columns.Clear();

            if (Comparison == null)
            {
                OnPropertyChanged(nameof(RowCount));
                OnPropertyChanged(nameof(HasRows));
                return;
            }

            var allHeaders = Comparison.GetAllColumnHeaders();

            if (Comparison.Warnings.Any())
            {
                _logger.LogWarning($"Row comparison detected {Comparison.Warnings.Count} structural inconsistencies in column headers", "RowComparisonViewModel");
                foreach (var warning in Comparison.Warnings)
                {
                    _logger.LogWarning($"Column '{warning.ColumnName}': {warning.Message} (Files: {string.Join(", ", warning.AffectedFiles)})", "RowComparisonViewModel");
                }
            }

            for (int i = 0; i < allHeaders.Count; i++)
            {
                var header = allHeaders[i];
                var columnViewModel = new RowComparisonColumnViewModel(header, i, Comparison.Rows);
                Columns.Add(columnViewModel);
            }

            // Populate flat cache of all cells for fast theme refresh (O(n) instead of O(n×m))
            _allCells = Columns.SelectMany(col => col.Cells).ToList();

            _logger.LogInfo($"Created row comparison with {allHeaders.Count} columns for {Comparison.Rows.Count} rows using intelligent header mapping", "RowComparisonViewModel");

            OnPropertyChanged(nameof(RowCount));
            OnPropertyChanged(nameof(HasRows));
        }
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
                if (_themeManager != null)
                {
                    _themeManager.ThemeChanged -= OnThemeChanged;
                }

                if (Columns != null)
                {
                    foreach (var column in Columns.OfType<IDisposable>())
                    {
                        column.Dispose();
                    }
                    Columns.Clear();
                }

                _comparison = null;
            }

            _disposed = true;
        }
    }

    public class RowComparisonColumnViewModel : ViewModelBase, IDisposable
    {
        private bool _disposed = false;

        public string Header { get; }
        public int ColumnIndex { get; }
        public ObservableCollection<RowComparisonCellViewModel> Cells { get; }

        public RowComparisonColumnViewModel(string header, int columnIndex, IReadOnlyList<ExcelRow> rows)
        {
            Header = header;
            ColumnIndex = columnIndex;
            Cells = new ObservableCollection<RowComparisonCellViewModel>();

            // Use intelligent header-based mapping instead of positional mapping
            var allValues = rows.Select(row => row.GetCellAsStringByHeader(header) ?? string.Empty).ToList();

            // PRE-COMPUTE column-level data ONCE instead of N times (N = row count)
            // This eliminates massive computational waste in DetermineComparisonResult
            var columnData = PrecomputeColumnComparisonData(allValues);

            // Reuse allValues[i] instead of calling GetCellAsStringByHeader again (eliminates duplicate calls)
            for (int i = 0; i < rows.Count; i++)
            {
                var cellValue = allValues[i]; // Reuse already-computed value
                var comparisonResult = DetermineComparisonResult(cellValue, columnData);
                var cellViewModel = new RowComparisonCellViewModel(rows[i], columnIndex, cellValue, comparisonResult);

                Cells.Add(cellViewModel);
            }
        }

        private static ColumnComparisonData PrecomputeColumnComparisonData(IList<string> allValues)
        {
            // Normalize values first
            var normalizedAllValues = allValues.Select(v => (v ?? "").Trim()).ToList();
            var allNonEmptyValues = normalizedAllValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var distinctNonEmptyValues = allNonEmptyValues.Distinct().ToList();

            // Pre-compute value groups for ranking algorithm
            var valueGroups = allNonEmptyValues
                .GroupBy(v => v)
                .Select(g => new ValueGroup(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)      // Primary: Most frequent first (rank 0)
                .ThenBy(g => g.Value)                 // Secondary: Alphabetical for determinism
                .ToList();

            return new ColumnComparisonData(
                normalizedAllValues,
                allNonEmptyValues,
                distinctNonEmptyValues,
                valueGroups,
                allValues.Count
            );
        }

        private record ValueGroup(string Value, int Count);

        private record ColumnComparisonData(
            List<string> NormalizedAllValues,
            List<string> AllNonEmptyValues,
            List<string> DistinctNonEmptyValues,
            List<ValueGroup> ValueGroups,
            int TotalCount
        );

        private static CellComparisonResult DetermineComparisonResult(string currentValue, ColumnComparisonData columnData)
        {
            var normalizedCurrentValue = (currentValue ?? "").Trim();
            var hasValue = !string.IsNullOrWhiteSpace(normalizedCurrentValue);

            if (!hasValue)
            {
                return columnData.AllNonEmptyValues.Count != 0
                    ? CellComparisonResult.CreateMissing(columnData.AllNonEmptyValues.Count)
                    : CellComparisonResult.CreateMatch(columnData.TotalCount, columnData.TotalCount);
            }

            if (columnData.DistinctNonEmptyValues.Count <= 1)
            {
                return CellComparisonResult.CreateMatch(columnData.AllNonEmptyValues.Count, columnData.AllNonEmptyValues.Count);
            }

            var valueGroups = columnData.ValueGroups;

            var currentRank = valueGroups.FindIndex(g => g.Value == normalizedCurrentValue);
            var currentFrequency = valueGroups[currentRank].Count;
            var totalGroups = valueGroups.Count;

            return CellComparisonResult.CreateDifferent(currentFrequency, currentRank, totalGroups, columnData.AllNonEmptyValues.Count);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                Cells?.Clear();
            }
            _disposed = true;
        }
    }

    public class RowComparisonCellViewModel : ViewModelBase
    {

        public ExcelRow SourceRow { get; }
        public int ColumnIndex { get; }
        public string Value { get; }
        public string RowInfo { get; }
        public bool HasValue => !string.IsNullOrWhiteSpace(Value);
        public CellComparisonResult ComparisonResult { get; }

        // Backward compatibility - expose the type for existing bindings
        public ComparisonType ComparisonType => ComparisonResult.Type;

        public RowComparisonCellViewModel(ExcelRow sourceRow, int columnIndex, string value, CellComparisonResult comparisonResult)
        {
            SourceRow = sourceRow ?? throw new ArgumentNullException(nameof(sourceRow));
            ColumnIndex = columnIndex;
            Value = value ?? string.Empty;
            ComparisonResult = comparisonResult ?? CellComparisonResult.CreateMatch();
            RowInfo = $"{sourceRow.FileName} - {sourceRow.SheetName} - R{sourceRow.RowIndex + 1}";
        }

        /// <summary>
        /// Forces re-evaluation of the ComparisonResult binding to update cell background colors
        /// Called when theme changes to refresh colors without recreating the entire comparison
        /// </summary>
        public void RefreshColors()
        {
            OnPropertyChanged(nameof(ComparisonResult));
        }
    }
}
