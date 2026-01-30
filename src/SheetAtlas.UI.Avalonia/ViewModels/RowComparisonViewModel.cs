using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.HeaderResolvers;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.Managers;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels
{
    public class RowComparisonViewModel : ViewModelBase, IDisposable
    {
        private readonly ILogService _logger;
        private readonly IHeaderGroupingService _headerGroupingService;
        private readonly IThemeManager? _themeManager;
        private readonly Func<string, string?>? _semanticNameResolver;
        private Func<IEnumerable<string>>? _includedColumnsProvider;
        private RowComparison? _comparison;

        // Export services (injected via SetExportServices pattern)
        private IComparisonExportService? _exportService;
        private IFilePickerService? _filePickerService;
        private ISettingsService? _settingsService;
        private bool _isExporting;

        private bool _disposed = false;
        private bool _isExpanded = true;
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

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }

        public ICommand CloseCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public bool IsExporting
        {
            get => _isExporting;
            private set => SetField(ref _isExporting, value);
        }

        public bool CanExport => HasRows && _exportService != null && !IsExporting;

        public event EventHandler? CloseRequested;

        public RowComparisonViewModel(
            ILogService logger,
            IHeaderGroupingService headerGroupingService,
            IThemeManager? themeManager = null,
            Func<string, string?>? semanticNameResolver = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _headerGroupingService = headerGroupingService ?? throw new ArgumentNullException(nameof(headerGroupingService));
            _themeManager = themeManager;
            _semanticNameResolver = semanticNameResolver;

            CloseCommand = new RelayCommand(() =>
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            });

            ExportExcelCommand = new RelayCommand(ExecuteExportExcelAsync, () => CanExport);
            ExportCsvCommand = new RelayCommand(ExecuteExportCsvAsync, () => CanExport);

            if (_themeManager != null)
            {
                _themeManager.ThemeChanged += OnThemeChanged;
            }
        }

        public RowComparisonViewModel(
            RowComparison comparison,
            ILogService logger,
            IHeaderGroupingService headerGroupingService,
            IThemeManager? themeManager = null,
            Func<string, string?>? semanticNameResolver = null)
            : this(logger, headerGroupingService, themeManager, semanticNameResolver)
        {
            Comparison = comparison;
        }

        /// <summary>
        /// Sets a provider function that returns the included column names for filtering.
        /// Call RefreshColumns after setting to apply the filter.
        /// </summary>
        public void SetIncludedColumnsProvider(Func<IEnumerable<string>>? provider)
        {
            _includedColumnsProvider = provider;
        }

        /// <summary>
        /// Sets the export services for this ViewModel.
        /// Must be called before export commands can be used.
        /// </summary>
        public void SetExportServices(
            IComparisonExportService exportService,
            IFilePickerService filePickerService,
            ISettingsService settingsService)
        {
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            OnPropertyChanged(nameof(CanExport));
            ((RelayCommand)ExportExcelCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
        }

        private async Task ExecuteExportExcelAsync()
        {
            if (Comparison == null || _exportService == null || _filePickerService == null || _settingsService == null)
                return;

            try
            {
                IsExporting = true;
                RaiseExportCanExecuteChanged();

                var suggestedFilename = _exportService.GenerateFilename(Comparison, "xlsx");
                var defaultDir = _settingsService.Current.FileLocations.OutputFolder;

                // Build suggested path (picker extracts directory and filename from full path)
                var suggestedPath = string.IsNullOrEmpty(defaultDir)
                    ? suggestedFilename
                    : Path.Combine(defaultDir, suggestedFilename);

                var outputPath = await _filePickerService.SaveFileAsync(
                    "Export Comparison to Excel",
                    suggestedPath,
                    new[] { "*.xlsx" });

                if (string.IsNullOrEmpty(outputPath))
                {
                    _logger.LogInfo("Excel export cancelled by user", "RowComparisonViewModel");
                    return;
                }

                var includedColumns = _includedColumnsProvider?.Invoke();
                var semanticNames = BuildSemanticNamesMap();
                var result = await _exportService.ExportToExcelAsync(Comparison, outputPath, includedColumns, semanticNames);

                if (result.IsSuccess)
                {
                    _logger.LogInfo($"Excel export completed: {result.OutputPath}", "RowComparisonViewModel");
                }
                else
                {
                    _logger.LogError($"Excel export failed: {result.ErrorMessage}", null, "RowComparisonViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Excel export failed", ex, "RowComparisonViewModel");
            }
            finally
            {
                IsExporting = false;
                RaiseExportCanExecuteChanged();
            }
        }

        private async Task ExecuteExportCsvAsync()
        {
            if (Comparison == null || _exportService == null || _filePickerService == null || _settingsService == null)
                return;

            try
            {
                IsExporting = true;
                RaiseExportCanExecuteChanged();

                var suggestedFilename = _exportService.GenerateFilename(Comparison, "csv");
                var defaultDir = _settingsService.Current.FileLocations.OutputFolder;

                // Build suggested path (picker extracts directory and filename from full path)
                var suggestedPath = string.IsNullOrEmpty(defaultDir)
                    ? suggestedFilename
                    : Path.Combine(defaultDir, suggestedFilename);

                var outputPath = await _filePickerService.SaveFileAsync(
                    "Export Comparison to CSV",
                    suggestedPath,
                    new[] { "*.csv" });

                if (string.IsNullOrEmpty(outputPath))
                {
                    _logger.LogInfo("CSV export cancelled by user", "RowComparisonViewModel");
                    return;
                }

                var includedColumns = _includedColumnsProvider?.Invoke();
                var semanticNames = BuildSemanticNamesMap();
                var result = await _exportService.ExportToCsvAsync(Comparison, outputPath, includedColumns, semanticNames);

                if (result.IsSuccess)
                {
                    _logger.LogInfo($"CSV export completed: {result.OutputPath}", "RowComparisonViewModel");
                }
                else
                {
                    _logger.LogError($"CSV export failed: {result.ErrorMessage}", null, "RowComparisonViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("CSV export failed", ex, "RowComparisonViewModel");
            }
            finally
            {
                IsExporting = false;
                RaiseExportCanExecuteChanged();
            }
        }

        private void RaiseExportCanExecuteChanged()
        {
            OnPropertyChanged(nameof(CanExport));
            ((RelayCommand)ExportExcelCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
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

        /// <summary>
        /// Builds a mapping from original column names to their semantic names for export.
        /// </summary>
        private IReadOnlyDictionary<string, string>? BuildSemanticNamesMap()
        {
            if (_semanticNameResolver == null || Comparison == null)
                return null;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var originalName in Comparison.GetAllColumnHeaders())
            {
                var semanticName = _semanticNameResolver(originalName);
                if (!string.IsNullOrEmpty(semanticName) && !string.Equals(semanticName, originalName, StringComparison.OrdinalIgnoreCase))
                {
                    result[originalName] = semanticName;
                }
            }

            return result.Count > 0 ? result : null;
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

            // Filter headers if provider is set
            HashSet<string>? includedColumnsSet = null;
            if (_includedColumnsProvider != null)
            {
                var includedColumns = _includedColumnsProvider();
                if (includedColumns != null)
                {
                    includedColumnsSet = new HashSet<string>(includedColumns, StringComparer.OrdinalIgnoreCase);
                }
            }

            if (Comparison.Warnings.Any())
            {
                _logger.LogWarning($"Row comparison detected {Comparison.Warnings.Count} structural inconsistencies in column headers", "RowComparisonViewModel");
                foreach (var warning in Comparison.Warnings)
                {
                    _logger.LogWarning($"Column '{warning.ColumnName}': {warning.Message} (Files: {string.Join(", ", warning.AffectedFiles)})", "RowComparisonViewModel");
                }
            }

            // Group raw headers by their semantic name (or by themselves if no semantic name)
            var resolver = new FunctionHeaderResolver(_semanticNameResolver);
            var headerGroups = _headerGroupingService.GroupHeaders(
                allHeaders,
                resolver,
                includedColumnsSet);

            int columnIndex = 0;
            int mergedColumns = 0;
            foreach (var group in headerGroups)
            {
                if (group.OriginalHeaders.Count > 1)
                {
                    mergedColumns++;
                }

                var columnViewModel = new RowComparisonColumnViewModel(
                    group.DisplayName,
                    group.OriginalHeaders,
                    columnIndex++,
                    Comparison.Rows);
                Columns.Add(columnViewModel);
            }

            // Populate flat cache of all cells for fast theme refresh (O(n) instead of O(n×m))
            _allCells = Columns.SelectMany(col => col.Cells).ToList();

            if (mergedColumns > 0)
            {
                _logger.LogInfo($"Created row comparison with {headerGroups.Count} columns ({mergedColumns} merged from {allHeaders.Count} raw headers) for {Comparison.Rows.Count} rows", "RowComparisonViewModel");
            }
            else
            {
                _logger.LogInfo($"Created row comparison with {allHeaders.Count} columns for {Comparison.Rows.Count} rows using intelligent header mapping", "RowComparisonViewModel");
            }

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

        /// <summary>
        /// Display header (semantic name if available, otherwise raw header).
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Raw header names from files (used for cell value lookup).
        /// Multiple headers when columns with different names are merged by semantic name.
        /// </summary>
        public IReadOnlyList<string> RawHeaders { get; }

        public int ColumnIndex { get; }
        public ObservableCollection<RowComparisonCellViewModel> Cells { get; }

        public RowComparisonColumnViewModel(string displayHeader, IReadOnlyList<string> rawHeaders, int columnIndex, IReadOnlyList<ExcelRow> rows)
        {
            Header = displayHeader;
            RawHeaders = rawHeaders;
            ColumnIndex = columnIndex;
            Cells = new ObservableCollection<RowComparisonCellViewModel>();

            // Try each raw header to find a value (supports merged columns with different original names)
            var allValues = rows.Select(row => GetCellValueFromAnyHeader(row, rawHeaders)).ToList();

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

        /// <summary>
        /// Try to get a cell value using any of the provided headers.
        /// Returns the first non-empty value found, or empty string if none.
        /// </summary>
        private static string GetCellValueFromAnyHeader(ExcelRow row, IReadOnlyList<string> headers)
        {
            foreach (var header in headers)
            {
                var value = row.GetCellAsStringByHeader(header);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return string.Empty;
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
