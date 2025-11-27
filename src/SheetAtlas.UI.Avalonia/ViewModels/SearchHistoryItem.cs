using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Commands;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class SearchHistoryItem : ViewModelBase, IDisposable
{
    private bool _disposed = false;
    private bool _isExpanded = true;
    private ObservableCollection<FileResultGroup> _fileGroups = new();
    private readonly List<SearchResultItem> _flattenedItems; // Flat cache for O(n) operations

    public string Query { get; }
    public int TotalResults { get; }
    public string DisplayText => $"Search: \"{Query}\" ({TotalResults:N0} hits)";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public ObservableCollection<FileResultGroup> FileGroups
    {
        get => _fileGroups;
        set => SetField(ref _fileGroups, value);
    }

    public int SelectedCount => _flattenedItems
        .Count(item => item.IsSelected && item.CanBeCompared);

    public string SelectionText => SelectedCount switch
    {
        0 => "no selected rows",
        1 => "1 selected row",
        _ => $"{SelectedCount} selected rows"
    };

    public ICommand ClearSelectionCommand { get; private set; }

    public event EventHandler? SelectionChanged;

    public SearchHistoryItem(string query, IReadOnlyList<SearchResult> results)
    {
        Query = query;
        TotalResults = results.Count;

        // Initialize Clear command
        ClearSelectionCommand = new RelayCommand(() => { ClearSelection(); return Task.CompletedTask; });

        // Group results by file
        var fileGroups = results
            .GroupBy(r => r.SourceFile)
            .Select(fileGroup => new FileResultGroup(fileGroup.Key, fileGroup.ToList()))
            .OrderBy(fg => fg.FileName)
            .ToList();

        FileGroups = new ObservableCollection<FileResultGroup>(fileGroups);

        // Flatten hierarchy ONCE during construction for O(n) operations
        _flattenedItems = FileGroups
            .SelectMany(fg => fg.SheetGroups)
            .SelectMany(sg => sg.Results)
            .ToList(); // Materialize once!

        // Setup selection events: O(n) instead of O(n³)
        SetupSelectionEvents();
    }

    // O(n) instead of O(n³) - uses flat cache
    private void ClearSelection()
    {
        foreach (var item in _flattenedItems.Where(i => i.IsSelected))
        {
            item.IsSelected = false;
        }
        NotifySelectionChanged();
    }

    // O(n) instead of O(n³) - uses flat cache
    private void SetupSelectionEvents()
    {
        foreach (var item in _flattenedItems)
        {
            item.SelectionChanged += OnItemSelectionChanged;
        }
    }

    private void OnItemSelectionChanged(object? sender, EventArgs e)
    {
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionText));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // O(n) instead of O(n³) - uses flat cache
    protected void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Unsubscribe from selection changed events
            foreach (var item in _flattenedItems)
            {
                item.SelectionChanged -= OnItemSelectionChanged;
            }

            // Dispose all FileGroups (which will dispose SheetGroups)
            foreach (var fileGroup in FileGroups)
            {
                fileGroup.Dispose();
            }
            FileGroups.Clear();
        }
        _disposed = true;
    }
}
