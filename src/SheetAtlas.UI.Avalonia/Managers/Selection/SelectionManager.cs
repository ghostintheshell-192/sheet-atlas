using System.Collections.ObjectModel;
using SheetAtlas.UI.Avalonia.Models.Search;
using SheetAtlas.UI.Avalonia.ViewModels;
using Microsoft.Extensions.Logging;

namespace SheetAtlas.UI.Avalonia.Managers.Selection;

/// <summary>
/// Manages selection of cells and sheets in search results. Tracks selected items and notifies listeners of changes.
/// </summary>
public class SelectionManager : ISelectionManager
{
    private readonly ILogger<SelectionManager> _logger;
    private readonly ObservableCollection<ICellOccurrence> _selectedCells = new();
    private readonly ObservableCollection<ISheetOccurrence> _selectedSheets = new();
    private IEnumerable<IGroupedSearchResult> _groupedResults = Enumerable.Empty<IGroupedSearchResult>();

    public IReadOnlyList<ICellOccurrence> SelectedCells =>
        new ReadOnlyObservableCollection<ICellOccurrence>(_selectedCells);

    public IReadOnlyList<ISheetOccurrence> SelectedSheets =>
        new ReadOnlyObservableCollection<ISheetOccurrence>(_selectedSheets);

    public event EventHandler<EventArgs>? SelectionChanged;
    public event EventHandler<EventArgs>? VisibilityChanged;

    public SelectionManager(ILogger<SelectionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void UpdateGroupedResults(IEnumerable<IGroupedSearchResult> results)
    {
        _groupedResults = results ?? throw new ArgumentNullException(nameof(results));
    }

    public void ClearSelections()
    {
        // Clear selected items
        foreach (var cell in _selectedCells.ToList())
        {
            cell.IsSelected = false;
        }

        foreach (var sheet in _selectedSheets.ToList())
        {
            sheet.IsSelected = false;
        }

        _selectedCells.Clear();
        _selectedSheets.Clear();

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleCellSelection(ICellOccurrence cell)
    {
        if (cell == null) return;

        cell.IsSelected = !cell.IsSelected;

        if (cell.IsSelected)
        {
            if (!_selectedCells.Contains(cell))
                _selectedCells.Add(cell);
        }
        else
        {
            _selectedCells.Remove(cell);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleSheetSelection(ISheetOccurrence sheet)
    {
        if (sheet == null) return;

        sheet.IsSelected = !sheet.IsSelected;

        if (sheet.IsSelected)
        {
            if (!_selectedSheets.Contains(sheet))
                _selectedSheets.Add(sheet);

            // Select all cells in the sheet
            foreach (var cell in sheet.CellOccurrences)
            {
                cell.IsSelected = true;
                if (!_selectedCells.Contains(cell))
                    _selectedCells.Add(cell);
            }
        }
        else
        {
            _selectedSheets.Remove(sheet);

            // Deselect all cells in the sheet
            foreach (var cell in sheet.CellOccurrences)
            {
                cell.IsSelected = false;
                _selectedCells.Remove(cell);
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleFileVisibility(IFileLoadResultViewModel file)
    {
        if (file == null) return;

        // Find file in grouped results and toggle visibility
        foreach (var group in _groupedResults)
        {
            var fileOccurrence = group.FileOccurrences.FirstOrDefault(f => f.File == file);
            if (fileOccurrence != null)
            {
                fileOccurrence.IsVisible = !fileOccurrence.IsVisible;
            }
        }

        UpdateVisibilityStats();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetFileVisibility(IFileLoadResultViewModel? file = null, bool? visible = null)
    {
        if (file == null || visible == null) return;

        foreach (var group in _groupedResults)
        {
            var fileOccurrence = group.FileOccurrences.FirstOrDefault(f => f.File == file);
            if (fileOccurrence != null)
            {
                fileOccurrence.IsVisible = visible.Value;
            }
        }

        UpdateVisibilityStats();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ShowAllFiles()
    {
        foreach (var group in _groupedResults)
        {
            foreach (var fileOccurrence in group.FileOccurrences)
            {
                fileOccurrence.IsVisible = true;
            }
        }

        UpdateVisibilityStats();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ShowOnlyFile(IFileLoadResultViewModel file)
    {
        if (file == null) return;

        foreach (var group in _groupedResults)
        {
            foreach (var fileOccurrence in group.FileOccurrences)
            {
                fileOccurrence.IsVisible = fileOccurrence.File == file;
            }
        }

        UpdateVisibilityStats();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateVisibilityStats()
    {
        foreach (var group in _groupedResults)
        {
            group.UpdateVisibilityStats();
        }
    }
}
