using System.Collections.ObjectModel;
using System.ComponentModel;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
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
    private readonly ColumnLinkingViewModel? _columnLinkingViewModel;

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
        IThemeManager themeManager,
        ColumnLinkingViewModel? columnLinkingViewModel = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _comparisonViewModelLogger = comparisonViewModelLogger ?? throw new ArgumentNullException(nameof(comparisonViewModelLogger));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _columnLinkingViewModel = columnLinkingViewModel;

        RowComparisons = new ReadOnlyObservableCollection<RowComparisonViewModel>(_rowComparisons);
    }

    public void CreateComparison(RowComparison comparison)
    {
        if (comparison == null)
        {
            _logger.LogWarning("CreateComparison called with null comparison", "RowComparisonCoordinator");
            return;
        }

        // Build semantic name resolver from current column links
        Func<string, string?>? semanticNameResolver = null;
        if (_columnLinkingViewModel != null)
        {
            var currentLinks = _columnLinkingViewModel.ColumnLinks
                .Select(vm => vm.GetUpdatedLink())
                .ToList();

            semanticNameResolver = rawHeader =>
            {
                foreach (var link in currentLinks)
                {
                    if (link.Matches(rawHeader))
                        return link.SemanticName;
                }
                return null;
            };
        }

        var comparisonViewModel = new RowComparisonViewModel(
            _comparisonViewModelLogger,
            _themeManager,
            semanticNameResolver);

        // Connect column filter BEFORE setting Comparison (which triggers RefreshColumns)
        if (_columnLinkingViewModel != null)
        {
            comparisonViewModel.SetIncludedColumnsProvider(() => _columnLinkingViewModel.GetIncludedColumnNames());
        }

        // Now set Comparison - this triggers RefreshColumns with the filter in place
        comparisonViewModel.Comparison = comparison;

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

                // Re-build semantic name resolver
                Func<string, string?>? resolver = null;
                if (_columnLinkingViewModel != null)
                {
                    var currentLinks = _columnLinkingViewModel.ColumnLinks
                        .Select(vm => vm.GetUpdatedLink())
                        .ToList();

                    resolver = rawHeader =>
                    {
                        foreach (var link in currentLinks)
                        {
                            if (link.Matches(rawHeader))
                                return link.SemanticName;
                        }
                        return null;
                    };
                }

                var newViewModel = new RowComparisonViewModel(_comparisonViewModelLogger, _themeManager, resolver);

                // Connect column filter BEFORE setting Comparison
                if (_columnLinkingViewModel != null)
                {
                    newViewModel.SetIncludedColumnsProvider(() => _columnLinkingViewModel.GetIncludedColumnNames());
                }

                // Now set Comparison - this triggers RefreshColumns with the filter in place
                newViewModel.Comparison = updatedComparison;

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
