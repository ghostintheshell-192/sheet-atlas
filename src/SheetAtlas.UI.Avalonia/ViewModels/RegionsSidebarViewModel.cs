using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Regions sidebar. Displays a File → Sheet → Region hierarchy.
/// </summary>
public class RegionsSidebarViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private RegionItem? _selectedRegion;
    private bool _disposed;

    public ObservableCollection<FileRegionGroup> FileGroups { get; } = new();

    public RegionItem? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (SetField(ref _selectedRegion, value))
                OnPropertyChanged(nameof(HasSelectedRegion));
        }
    }

    public bool HasSelectedRegion => SelectedRegion != null;

    public int TotalRegionCount => FileGroups.Sum(f => f.TotalRegionCount);

    public ICommand DeleteRegionCommand { get; }

    public event EventHandler<RegionEventArgs>? RegionDeleteRequested;

    public RegionsSidebarViewModel(ILogService logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DeleteRegionCommand = new RelayCommand(() =>
        {
            if (SelectedRegion != null)
            {
                RegionDeleteRequested?.Invoke(this, new RegionEventArgs(
                    SelectedRegion.FilePath, SelectedRegion.SheetName, SelectedRegion.Region));
            }
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Rebuild the hierarchy from loaded files.
    /// </summary>
    public void RefreshFromFiles(IEnumerable<IFileLoadResultViewModel> loadedFiles)
    {
        FileGroups.Clear();

        foreach (var fileVm in loadedFiles)
        {
            if (fileVm.File?.Sheets == null) continue;

            var fileGroup = new FileRegionGroup
            {
                FileName = fileVm.FileName,
                FilePath = fileVm.FilePath
            };

            foreach (var (sheetName, sheetData) in fileVm.File.Sheets)
            {
                var regions = sheetData.DataRegions;
                if (regions.Count == 0) continue;

                var sheetGroup = new SheetRegionGroup { SheetName = sheetName };
                foreach (var region in regions.Values)
                {
                    sheetGroup.Regions.Add(new RegionItem
                    {
                        Name = region.Name,
                        FilePath = fileVm.FilePath,
                        SheetName = sheetName,
                        Region = region
                    });
                }

                fileGroup.Sheets.Add(sheetGroup);
            }

            if (fileGroup.Sheets.Count > 0)
                FileGroups.Add(fileGroup);
        }

        OnPropertyChanged(nameof(TotalRegionCount));
    }

    /// <summary>
    /// Add a single region without full refresh.
    /// </summary>
    public void AddRegion(string filePath, string fileName, string sheetName, Core.Domain.ValueObjects.DataRegion region)
    {
        var fileGroup = FileGroups.FirstOrDefault(f => f.FilePath == filePath);
        if (fileGroup == null)
        {
            fileGroup = new FileRegionGroup { FileName = fileName, FilePath = filePath };
            FileGroups.Add(fileGroup);
        }

        var sheetGroup = fileGroup.Sheets.FirstOrDefault(s => s.SheetName == sheetName);
        if (sheetGroup == null)
        {
            sheetGroup = new SheetRegionGroup { SheetName = sheetName };
            fileGroup.Sheets.Add(sheetGroup);
        }

        sheetGroup.Regions.Add(new RegionItem
        {
            Name = region.Name,
            FilePath = filePath,
            SheetName = sheetName,
            Region = region
        });

        OnPropertyChanged(nameof(TotalRegionCount));
    }

    /// <summary>
    /// Remove a single region.
    /// </summary>
    public void RemoveRegion(string filePath, string sheetName, string regionName)
    {
        var fileGroup = FileGroups.FirstOrDefault(f => f.FilePath == filePath);
        if (fileGroup == null) return;

        var sheetGroup = fileGroup.Sheets.FirstOrDefault(s => s.SheetName == sheetName);
        if (sheetGroup == null) return;

        var item = sheetGroup.Regions.FirstOrDefault(r => r.Name == regionName);
        if (item != null)
        {
            sheetGroup.Regions.Remove(item);

            if (sheetGroup.Regions.Count == 0)
                fileGroup.Sheets.Remove(sheetGroup);

            if (fileGroup.Sheets.Count == 0)
                FileGroups.Remove(fileGroup);

            if (SelectedRegion == item)
                SelectedRegion = null;
        }

        OnPropertyChanged(nameof(TotalRegionCount));
    }

    public void Dispose()
    {
        if (_disposed) return;
        RegionDeleteRequested = null;
        FileGroups.Clear();
        _disposed = true;
    }
}
