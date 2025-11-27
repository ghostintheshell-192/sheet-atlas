using System.Collections.ObjectModel;
using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class FileResultGroup : ViewModelBase, IDisposable
{
    private bool _disposed = false;
    private bool _isExpanded = true;
    private ObservableCollection<SheetResultGroup> _sheetGroups = new();

    public ExcelFile File { get; }
    public string FileName => File.FileName;
    public int TotalResults { get; }
    public string DisplayText => $"{FileName} ({TotalResults:N0} hits)";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public ObservableCollection<SheetResultGroup> SheetGroups
    {
        get => _sheetGroups;
        set => SetField(ref _sheetGroups, value);
    }

    public FileResultGroup(ExcelFile file, List<SearchResult> results)
    {
        File = file;
        TotalResults = results.Count;

        // Group results by sheet
        var sheetGroups = results
            .GroupBy(r => r.SheetName)
            .Select(sheetGroup => new SheetResultGroup(sheetGroup.Key, sheetGroup.ToList()))
            .OrderBy(sg => sg.SheetName)
            .ToList();

        SheetGroups = new ObservableCollection<SheetResultGroup>(sheetGroups);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var sheetGroup in SheetGroups)
            {
                sheetGroup.Dispose();
            }
            SheetGroups.Clear();
        }

        _disposed = true;
    }
}
