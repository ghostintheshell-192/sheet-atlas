using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.Commands;
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
    private bool _isDetectionActive;
    private string _detectionTitle = "";
    private bool _disposed;

    private IRegionDetectionService? _detectionService;
    private Func<IEnumerable<IFileLoadResultViewModel>>? _loadedFilesProvider;

    public ObservableCollection<FileRegionGroup> FileGroups { get; } = new();
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

    public ICommand DeleteRegionCommand { get; }
    public ICommand EditRegionCommand { get; }
    public RelayCommand DetectSimilarFilesCommand { get; }
    public RelayCommand ApplyToSelectedFilesCommand { get; }
    public ICommand CancelDetectionCommand { get; }

    /// <summary>Raised when user deletes a region from the sidebar.</summary>
    public event EventHandler<RegionEventArgs>? RegionDeleteRequested;

    /// <summary>Raised when user wants to navigate to a region on the canvas.</summary>
    public event EventHandler<RegionEventArgs>? EditRegionRequested;

    /// <summary>Raised when user confirms applying detected regions to selected files.</summary>
    public event EventHandler<ApplyDetectedRegionsEventArgs>? ApplyDetectedRegionsRequested;

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
    }

    public void Dispose()
    {
        if (_disposed) return;
        RegionDeleteRequested = null;
        EditRegionRequested = null;
        ApplyDetectedRegionsRequested = null;
        FileGroups.Clear();
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
