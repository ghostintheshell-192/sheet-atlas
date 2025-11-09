using System.Collections.ObjectModel;
using System.ComponentModel;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.ViewModels;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Managers.Comparison;

/// <summary>
/// Coordinates the lifecycle of row comparison ViewModels.
/// Manages creation, selection, and removal of comparisons.
/// </summary>
public class RowComparisonCoordinator : IRowComparisonCoordinator, IDisposable
{
    private readonly ILogService _logger;
    private readonly ILogService _comparisonViewModelLogger;
    private readonly IThemeManager _themeManager;

    private readonly ObservableCollection<RowComparisonViewModel> _rowComparisons = new();
    private RowComparisonViewModel? _selectedComparison;
    private bool _disposed = false;

    public ReadOnlyObservableCollection<RowComparisonViewModel> RowComparisons { get; }

    public RowComparisonViewModel? SelectedComparison
    {
        get => _selectedComparison;
        set
        {
            if (_selectedComparison != value)
            {
                var oldSelection = _selectedComparison;
                _selectedComparison = value;
                OnPropertyChanged(nameof(SelectedComparison));

                SelectionChanged?.Invoke(this, new ComparisonSelectionChangedEventArgs(oldSelection, value));
            }
        }
    }

    public event EventHandler<ComparisonAddedEventArgs>? ComparisonAdded;
    public event EventHandler<ComparisonRemovedEventArgs>? ComparisonRemoved;
    public event EventHandler<ComparisonSelectionChangedEventArgs>? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public RowComparisonCoordinator(
        ILogService logger,
        ILogService comparisonViewModelLogger,
        IThemeManager themeManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _comparisonViewModelLogger = comparisonViewModelLogger ?? throw new ArgumentNullException(nameof(comparisonViewModelLogger));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        RowComparisons = new ReadOnlyObservableCollection<RowComparisonViewModel>(_rowComparisons);
    }

    public void CreateComparison(RowComparison comparison)
    {
        if (comparison == null)
        {
            _logger.LogWarning("CreateComparison called with null comparison", "RowComparisonCoordinator");
            return;
        }

        var comparisonViewModel = new RowComparisonViewModel(comparison, _comparisonViewModelLogger, _themeManager);

        comparisonViewModel.CloseRequested += OnComparisonCloseRequested;

        _rowComparisons.Add(comparisonViewModel);
        SelectedComparison = comparisonViewModel;

        _logger.LogInfo($"Created row comparison: {comparison.Name} with {comparison.Rows.Count} rows", "RowComparisonCoordinator");

        ComparisonAdded?.Invoke(this, new ComparisonAddedEventArgs(comparisonViewModel));
    }

    public void RemoveComparison(RowComparisonViewModel comparison)
    {
        if (comparison == null)
        {
            _logger.LogWarning("RemoveComparison called with null comparison", "RowComparisonCoordinator");
            return;
        }

        if (!_rowComparisons.Contains(comparison))
        {
            _logger.LogWarning($"Attempted to remove comparison not in collection: {comparison.Title}", "RowComparisonCoordinator");
            return;
        }

        comparison.CloseRequested -= OnComparisonCloseRequested;

        _rowComparisons.Remove(comparison);
        _logger.LogInfo($"Removed row comparison: {comparison.Title}", "RowComparisonCoordinator");

        if (SelectedComparison == comparison)
        {
            SelectedComparison = null;
        }

        ComparisonRemoved?.Invoke(this, new ComparisonRemovedEventArgs(comparison));
    }

    public void RemoveComparisonsForFile(ExcelFile file)
    {
        if (file == null)
            return;

        var comparisonsToRemove = new List<RowComparisonViewModel>();

        // NOTE: Scan-based approach for robustness
        // Future optimization: Consider event-driven tracking if we have 10+ active comparisons
        foreach (var comparisonViewModel in _rowComparisons.ToList())
        {
            if (comparisonViewModel.Comparison == null)
                continue;

            var hasRemovedFile = comparisonViewModel.Comparison.Rows.Any(row => row.SourceFile == file);

            if (!hasRemovedFile)
                continue;

            var remainingRows = comparisonViewModel.Comparison.Rows
                .Where(row => row.SourceFile != file)
                .ToList();

            if (remainingRows.Count >= 2)
            {
                // Comparison still valid with remaining rows - update it
                var updatedComparison = new RowComparison(
                    remainingRows.AsReadOnly(),
                    comparisonViewModel.Comparison.Name
                );

                var newViewModel = new RowComparisonViewModel(updatedComparison, _comparisonViewModelLogger, _themeManager);
                newViewModel.CloseRequested += OnComparisonCloseRequested;

                var index = _rowComparisons.IndexOf(comparisonViewModel);
                _rowComparisons[index] = newViewModel;

                if (SelectedComparison == comparisonViewModel)
                {
                    SelectedComparison = newViewModel;
                }

                comparisonViewModel.CloseRequested -= OnComparisonCloseRequested;

                _logger.LogInfo($"Updated comparison '{updatedComparison.Name}': removed rows from {file.FilePath}, {remainingRows.Count} rows remaining", "RowComparisonCoordinator");
            }
            else
            {
                // Less than 2 rows remaining - remove entire comparison
                comparisonsToRemove.Add(comparisonViewModel);
            }
        }

        foreach (var comparison in comparisonsToRemove)
        {
            RemoveComparison(comparison);
        }

        _logger.LogInfo($"Processed comparisons for removed file: {file.FilePath} (removed {comparisonsToRemove.Count} comparisons)", "RowComparisonCoordinator");
    }

    private void OnComparisonCloseRequested(object? sender, EventArgs e)
    {
        if (sender is RowComparisonViewModel comparisonViewModel)
        {
            RemoveComparison(comparisonViewModel);
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var comparison in _rowComparisons)
            {
                comparison.CloseRequested -= OnComparisonCloseRequested;
                comparison.Dispose();
            }
            _rowComparisons.Clear();
        }
    }
}
