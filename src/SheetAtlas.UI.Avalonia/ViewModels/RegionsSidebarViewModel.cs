using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Managers.Search;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Regions sidebar. Displays a File → Sheet → Region hierarchy
/// and supports cross-file region detection (ADR-012 Phase 2).
/// </summary>
public class RegionsSidebarViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private RegionItem? _selectedRegion;
    private bool _isRegionView;
    private RegionNameGroup? _selectedRegionGroup;
    private bool _isDetectionActive;
    private string _detectionTitle = "";
    private bool _disposed;

    private IRegionDetectionService? _detectionService;
    private Func<IEnumerable<IFileLoadResultViewModel>>? _loadedFilesProvider;

    public ObservableCollection<FileRegionGroup> FileGroups { get; } = new();
    public ObservableCollection<RegionNameGroup> RegionNameGroups { get; } = new();
    public ObservableCollection<CrossFileApplyViewModel> DetectionResults { get; } = new();

    public RegionItem? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (SetField(ref _selectedRegion, value))
            {
                OnPropertyChanged(nameof(HasSelectedRegion));
                OnPropertyChanged(nameof(CanDetectSimilarFiles));
                DetectSimilarFilesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedRegion => SelectedRegion != null;

    /// <summary>
    /// When true, the sidebar shows the "By Region" view (grouped by region name across files).
    /// When false (default), shows the "By File" view (File → Sheet → Region hierarchy).
    /// </summary>
    public bool IsRegionView
    {
        get => _isRegionView;
        set
        {
            if (SetField(ref _isRegionView, value))
            {
                if (!_isRegionView)
                    SelectedRegionGroup = null;
            }
        }
    }

    /// <summary>
    /// The selected group in the "By Region" view. Setting this activates cross-file filtering.
    /// </summary>
    public RegionNameGroup? SelectedRegionGroup
    {
        get => _selectedRegionGroup;
        set
        {
            var old = _selectedRegionGroup;
            if (SetField(ref _selectedRegionGroup, value))
            {
                if (old != null) old.IsSelected = false;
                if (value != null) value.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// Whether the detection results panel is visible.
    /// </summary>
    public bool IsDetectionActive
    {
        get => _isDetectionActive;
        private set => SetField(ref _isDetectionActive, value);
    }

    /// <summary>
    /// Title shown in the detection panel header (e.g. "Apply "Sales Data" to files").
    /// </summary>
    public string DetectionTitle
    {
        get => _detectionTitle;
        private set => SetField(ref _detectionTitle, value);
    }

    /// <summary>
    /// Whether there are enough conditions to run detection:
    /// a region is selected and there are other loaded files.
    /// </summary>
    public bool CanDetectSimilarFiles =>
        SelectedRegion != null && _detectionService != null && _loadedFilesProvider != null;

    public int TotalRegionCount => FileGroups.Sum(f => f.TotalRegionCount);
    public bool HasAnyRegions => TotalRegionCount > 0;

    public ICommand ClearRegionCommand { get; }
    public ICommand ClearItemCommand { get; }
    public ICommand ClearAllRegionsCommand { get; }
    public ICommand ClearFileRegionsCommand { get; }
    public ICommand EditRegionCommand { get; }
    public RelayCommand DetectSimilarFilesCommand { get; }
    public RelayCommand ApplyToSelectedFilesCommand { get; }
    public ICommand CancelDetectionCommand { get; }

    /// <summary>Raised when user requests to clear a single region.</summary>
    public event EventHandler<RegionEventArgs>? RegionClearRequested;

    /// <summary>Raised when user confirms a rename from the inline TextBox.</summary>
    public event EventHandler<RenameRegionEventArgs>? RenameRegionRequested;

    /// <summary>Raised when user requests to clear all regions across all files.</summary>
    public event EventHandler? ClearAllRegionsRequested;

    /// <summary>Raised when user requests to clear all regions of a specific file.</summary>
    public event EventHandler<ClearFileRegionsEventArgs>? ClearFileRegionsRequested;

    /// <summary>Raised when user wants to navigate to a region on the canvas.</summary>
    public event EventHandler<RegionEventArgs>? EditRegionRequested;

    /// <summary>Raised when user confirms applying detected regions to selected files.</summary>
    public event EventHandler<ApplyDetectedRegionsEventArgs>? ApplyDetectedRegionsRequested;

    public RegionsSidebarViewModel(ILogService logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ClearRegionCommand = new RelayCommand(() =>
        {
            if (SelectedRegion != null)
            {
                RegionClearRequested?.Invoke(this, new RegionEventArgs(
                    SelectedRegion.FilePath, SelectedRegion.SheetName, SelectedRegion.Region));
            }
            return Task.CompletedTask;
        });

        ClearItemCommand = new RelayCommand<RegionItem>(item =>
        {
            if (item != null)
                RegionClearRequested?.Invoke(this, new RegionEventArgs(item.FilePath, item.SheetName, item.Region));
        });

        ClearAllRegionsCommand = new RelayCommand(() =>
        {
            ClearAllRegionsRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        });

        ClearFileRegionsCommand = new RelayCommand<FileRegionGroup>(group =>
        {
            if (group != null)
                ClearFileRegionsRequested?.Invoke(this, new ClearFileRegionsEventArgs(group.FilePath, group.FileName));
        });

        EditRegionCommand = new RelayCommand<RegionItem>(item =>
        {
            EditRegionRequested?.Invoke(this, new RegionEventArgs(
                item.FilePath, item.SheetName, item.Region));
        });

        DetectSimilarFilesCommand = new RelayCommand(
            () => { RunDetection(); return Task.CompletedTask; },
            () => CanDetectSimilarFiles,
            logger);

        ApplyToSelectedFilesCommand = new RelayCommand(
            () => { ApplySelectedDetections(); return Task.CompletedTask; },
            () => IsDetectionActive && DetectionResults.Any(r => r.IsSelected && r.IsMatch),
            logger);

        CancelDetectionCommand = new RelayCommand(
            () => { CancelDetection(); return Task.CompletedTask; });
    }

    /// <summary>
    /// Inject detection dependencies after construction (called by MainWindowViewModel).
    /// </summary>
    public void SetDetectionDependencies(
        IRegionDetectionService detectionService,
        Func<IEnumerable<IFileLoadResultViewModel>> loadedFilesProvider)
    {
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
        _loadedFilesProvider = loadedFilesProvider ?? throw new ArgumentNullException(nameof(loadedFilesProvider));
        OnPropertyChanged(nameof(CanDetectSimilarFiles));
        DetectSimilarFilesCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Run detection for the selected region against all other loaded files.
    /// </summary>
    private void RunDetection()
    {
        if (SelectedRegion == null || _detectionService == null || _loadedFilesProvider == null)
            return;

        var selected = SelectedRegion;
        DetectionResults.Clear();
        DetectionTitle = $"Apply \"{selected.Name}\" to files";

        // Find the source sheet
        var loadedFiles = _loadedFilesProvider().ToList();
        var sourceFileVm = loadedFiles.FirstOrDefault(f =>
            f.FilePath.Equals(selected.FilePath, StringComparison.OrdinalIgnoreCase));
        var sourceSheet = sourceFileVm?.File?.GetSheet(selected.SheetName);
        if (sourceSheet == null)
        {
            _logger.LogWarning($"Source sheet not found for region '{selected.Name}'", "RegionsSidebarViewModel");
            return;
        }

        // Detect in all other files (same sheet name)
        foreach (var fileVm in loadedFiles)
        {
            if (fileVm.FilePath.Equals(selected.FilePath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (fileVm.File?.Sheets == null)
                continue;

            foreach (var (sheetName, targetSheet) in fileVm.File.Sheets)
            {
                // Skip sheets that already have a region with the same name
                if (targetSheet.GetDataRegion(selected.Name) != null)
                    continue;

                var result = _detectionService.DetectRegion(selected.Region, sourceSheet, targetSheet);
                DetectionResults.Add(new CrossFileApplyViewModel(
                    fileVm.FilePath, fileVm.FileName, sheetName, result));
            }
        }

        IsDetectionActive = true;
        ApplyToSelectedFilesCommand.RaiseCanExecuteChanged();
        _logger.LogInfo(
            $"Detection complete for '{selected.Name}': {DetectionResults.Count(r => r.IsMatch)} matches in {DetectionResults.Count} sheets",
            "RegionsSidebarViewModel");
    }

    /// <summary>
    /// Raise event with selected detection results for MainWindowViewModel to process.
    /// </summary>
    private void ApplySelectedDetections()
    {
        if (SelectedRegion == null) return;

        var toApply = DetectionResults
            .Where(r => r.IsSelected && r.IsMatch && r.Detection.DetectedRegion != null)
            .ToList();

        if (toApply.Count == 0) return;

        ApplyDetectedRegionsRequested?.Invoke(this, new ApplyDetectedRegionsEventArgs(toApply));

        CancelDetection();
    }

    private void CancelDetection()
    {
        IsDetectionActive = false;
        DetectionResults.Clear();
        DetectionTitle = "";
        ApplyToSelectedFilesCommand.RaiseCanExecuteChanged();
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

                sheetGroup.IsExpanded = sheetGroup.HasAnyWarnings;
                fileGroup.Sheets.Add(sheetGroup);
            }

            if (fileGroup.Sheets.Count > 0)
            {
                fileGroup.IsExpanded = fileGroup.HasAnyWarnings;
                FileGroups.Add(fileGroup);
            }
        }

        OnPropertyChanged(nameof(TotalRegionCount));
        OnPropertyChanged(nameof(HasAnyRegions));
        BuildRegionNameGroups();
    }

    /// <summary>
    /// Add a single region without full refresh.
    /// </summary>
    public void AddRegion(string filePath, string fileName, string sheetName, DataRegion region)
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
        OnPropertyChanged(nameof(HasAnyRegions));
        BuildRegionNameGroups();
    }

    /// <summary>
    /// Update a single region's bounds in-place (no tree rebuild, preserves expansion).
    /// </summary>
    public void UpdateRegion(string filePath, string sheetName, DataRegion updatedRegion)
    {
        var fileGroup = FileGroups.FirstOrDefault(f => f.FilePath == filePath);
        if (fileGroup == null) return;

        var sheetGroup = fileGroup.Sheets.FirstOrDefault(s => s.SheetName == sheetName);
        if (sheetGroup == null) return;

        for (int i = 0; i < sheetGroup.Regions.Count; i++)
        {
            if (sheetGroup.Regions[i].Name == updatedRegion.Name)
            {
                sheetGroup.Regions[i] = new RegionItem
                {
                    Name = updatedRegion.Name,
                    FilePath = filePath,
                    SheetName = sheetName,
                    Region = updatedRegion
                };
                break;
            }
        }
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
        OnPropertyChanged(nameof(HasAnyRegions));
        BuildRegionNameGroups();
    }

    /// <summary>
    /// Collects all region entries across all files that match the given region name.
    /// </summary>
    public List<RegionFilterEntry> CollectRegionsByName(string name)
    {
        var entries = new List<RegionFilterEntry>();
        foreach (var fileGroup in FileGroups)
        {
            foreach (var sheetGroup in fileGroup.Sheets)
            {
                foreach (var regionItem in sheetGroup.Regions)
                {
                    if (regionItem.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        entries.Add(new RegionFilterEntry(
                            regionItem.FilePath, regionItem.SheetName, regionItem.Region));
                    }
                }
            }
        }
        return entries;
    }

    /// <summary>
    /// Rebuild the "By Region" groups from the current FileGroups data.
    /// </summary>
    private void BuildRegionNameGroups()
    {
        var previousSelection = _selectedRegionGroup?.RegionName;
        RegionNameGroups.Clear();
        _selectedRegionGroup = null;

        var groups = FileGroups
            .SelectMany(fg => fg.Sheets.SelectMany(sg => sg.Regions.Select(r => new { fg.FileName, fg.FilePath, sg.SheetName, Region = r })))
            .GroupBy(x => x.Region.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var nameGroup = new RegionNameGroup { RegionName = group.Key };
            foreach (var entry in group)
            {
                nameGroup.FileEntries.Add(new RegionFileEntry
                {
                    FileName = entry.FileName,
                    FilePath = entry.FilePath,
                    SheetName = entry.SheetName,
                    Region = entry.Region.Region
                });
            }
            RegionNameGroups.Add(nameGroup);
        }

        // Restore selection if the region name still exists
        if (previousSelection != null)
        {
            var restored = RegionNameGroups.FirstOrDefault(g =>
                g.RegionName.Equals(previousSelection, StringComparison.OrdinalIgnoreCase));
            if (restored != null)
                SelectedRegionGroup = restored;
            else
                OnPropertyChanged(nameof(SelectedRegionGroup));
        }
    }

    /// <summary>
    /// Called from code-behind when the user commits a rename (Enter key or LostFocus).
    /// Validates, fires RenameRegionRequested, then exits editing mode.
    /// </summary>
    public void CommitRegionRename(RegionItem item)
    {
        var newName = item.EditName?.Trim();
        item.IsEditing = false;

        if (string.IsNullOrEmpty(newName) || newName == item.Name)
            return;

        RenameRegionRequested?.Invoke(this, new RenameRegionEventArgs(
            item.FilePath, item.SheetName, item.Region, newName));
    }

    public void Dispose()
    {
        if (_disposed) return;
        RegionClearRequested = null;
        RenameRegionRequested = null;
        ClearAllRegionsRequested = null;
        ClearFileRegionsRequested = null;
        EditRegionRequested = null;
        ApplyDetectedRegionsRequested = null;
        FileGroups.Clear();
        RegionNameGroups.Clear();
        DetectionResults.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Event args for when the user confirms applying detected regions to files.
/// </summary>
public class ApplyDetectedRegionsEventArgs : EventArgs
{
    public IReadOnlyList<CrossFileApplyViewModel> Selections { get; }

    public ApplyDetectedRegionsEventArgs(IReadOnlyList<CrossFileApplyViewModel> selections)
    {
        Selections = selections;
    }
}
