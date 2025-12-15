using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.Commands;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel item for displaying a ColumnLink in the sidebar.
/// </summary>
public class ColumnLinkViewModel : ViewModelBase
{
    private ColumnLink _link;
    private bool _isExpanded;
    private string _semanticName;
    private bool _isEditing;
    private bool _isHighlighted;

    public ColumnLinkViewModel(ColumnLink link)
    {
        _link = link;
        _semanticName = link.SemanticName;
        LinkedColumns = new ObservableCollection<LinkedColumnViewModel>(
            link.LinkedColumns.Select(c => new LinkedColumnViewModel(c)));
    }

    /// <summary>
    /// Clear internal references for garbage collection.
    /// </summary>
    public void Cleanup()
    {
        LinkedColumns.Clear();
        _link = null!;
    }

    public string SemanticName
    {
        get => _semanticName;
        set => SetField(ref _semanticName, value);
    }

    public DataType DominantType => _link.DominantType;

    public string TypeDisplay => DominantType.ToString();

    public int ColumnCount => _link.ColumnCount;

    public int SourceCount => _link.SourceCount;

    public bool IsGrouped => ColumnCount > 1;

    public bool IsAutoGrouped => _link.IsAutoGrouped;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetField(ref _isHighlighted, value);
    }

    public ObservableCollection<LinkedColumnViewModel> LinkedColumns { get; }

    /// <summary>
    /// Display text for the column header (name + type).
    /// </summary>
    public string DisplayText => $"{SemanticName} ({TypeDisplay})";

    /// <summary>
    /// Source count display for grouped columns.
    /// </summary>
    public string SourceCountText => SourceCount > 1 ? $"{SourceCount} files" : "";

    public ColumnLink GetUpdatedLink() =>
        _link with { SemanticName = _semanticName };
}

/// <summary>
/// ViewModel item for a single linked column (child of ColumnLinkViewModel).
/// </summary>
public class LinkedColumnViewModel : ViewModelBase
{
    private readonly LinkedColumn _column;

    public LinkedColumnViewModel(LinkedColumn column)
    {
        _column = column;
    }

    public string Name => _column.Name;

    public string? SourceFile => _column.SourceFile;

    public string SourceDisplay => _column.SourceDisplay;

    public DataType DetectedType => _column.DetectedType;
}

/// <summary>
/// ViewModel for the Columns sidebar, managing column linking.
/// </summary>
public class ColumnLinkingViewModel : ViewModelBase, IDisposable
{
    private readonly IColumnLinkingService _columnLinkingService;
    private readonly Func<IEnumerable<Core.Domain.Entities.ExcelFile>> _getLoadedFiles;
    private readonly Managers.Files.ILoadedFilesManager? _filesManager;
    private bool _disposed;
    private ExcelTemplate? _currentHighlightTemplate;

    public ColumnLinkingViewModel(
        IColumnLinkingService columnLinkingService,
        Func<IEnumerable<Core.Domain.Entities.ExcelFile>> getLoadedFiles,
        Managers.Files.ILoadedFilesManager? filesManager = null)
    {
        _columnLinkingService = columnLinkingService;
        _getLoadedFiles = getLoadedFiles;
        _filesManager = filesManager;

        ColumnLinks = new ObservableCollection<ColumnLinkViewModel>();

        RefreshCommand = new RelayCommand(() => { Refresh(); return Task.CompletedTask; });

        // Auto-refresh when files change
        if (_filesManager != null)
        {
            _filesManager.FileLoaded += OnFilesChanged;
            _filesManager.FileRemoved += OnFilesChanged;
        }
    }

    /// <summary>
    /// Merge source column(s) into target.
    /// </summary>
    public void MergeColumns(ColumnLinkViewModel target, ColumnLinkViewModel source)
    {
        if (target == source) return;

        var mergedLink = _columnLinkingService.MergeGroups(
            target.GetUpdatedLink(),
            source.GetUpdatedLink());

        // Remove source and replace target
        var targetIndex = ColumnLinks.IndexOf(target);
        source.Cleanup();
        ColumnLinks.Remove(source);
        target.Cleanup();
        ColumnLinks.Remove(target);

        // Insert merged at original target position
        var mergedVm = new ColumnLinkViewModel(mergedLink);
        if (targetIndex >= 0 && targetIndex <= ColumnLinks.Count)
            ColumnLinks.Insert(targetIndex, mergedVm);
        else
            ColumnLinks.Add(mergedVm);

        OnPropertyChanged(nameof(HasColumns));

        // Re-apply highlighting after merge
        ApplyHighlighting();
    }

    /// <summary>
    /// Ungroup a column link into individual columns.
    /// </summary>
    public void UngroupColumn(ColumnLinkViewModel column)
    {
        var ungrouped = _columnLinkingService.Ungroup(column.GetUpdatedLink());
        if (ungrouped.Count <= 1) return;

        var index = ColumnLinks.IndexOf(column);
        column.Cleanup();
        ColumnLinks.Remove(column);

        // Insert ungrouped items at the original position
        foreach (var link in ungrouped.Reverse())
        {
            var vm = new ColumnLinkViewModel(link);
            if (index >= 0 && index <= ColumnLinks.Count)
                ColumnLinks.Insert(index, vm);
            else
                ColumnLinks.Add(vm);
        }

        OnPropertyChanged(nameof(HasColumns));

        // Re-apply highlighting after ungroup
        ApplyHighlighting();
    }

    private void OnFilesChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>
    /// Called when a column is renamed. Re-applies highlighting.
    /// </summary>
    public void NotifyColumnRenamed()
    {
        ApplyHighlighting();
    }

    public ObservableCollection<ColumnLinkViewModel> ColumnLinks { get; }

    public ICommand RefreshCommand { get; }

    /// <summary>
    /// Whether there are any columns to display.
    /// </summary>
    public bool HasColumns => ColumnLinks.Count > 0;

    /// <summary>
    /// Refresh column links from loaded files.
    /// </summary>
    public void Refresh()
    {
        var files = _getLoadedFiles();
        var columns = _columnLinkingService.ExtractColumnsFromFiles(files);
        var links = _columnLinkingService.CreateInitialGroups(columns);

        // Cleanup old items before clearing
        foreach (var item in ColumnLinks)
        {
            item.Cleanup();
        }
        ColumnLinks.Clear();

        foreach (var link in links)
        {
            ColumnLinks.Add(new ColumnLinkViewModel(link));
        }

        OnPropertyChanged(nameof(HasColumns));

        // Re-apply highlighting after refresh
        ApplyHighlighting();
    }

    /// <summary>
    /// Update highlighting based on the selected template.
    /// Columns matching the template (by name AND type) are highlighted.
    /// </summary>
    public void SetHighlightedColumns(ExcelTemplate? template)
    {
        _currentHighlightTemplate = template;
        ApplyHighlighting();
    }

    /// <summary>
    /// Re-apply highlighting using the current template.
    /// Called internally after operations that modify ColumnLinks.
    /// Matching is based on column name + source file (stronger than just name + type).
    /// </summary>
    private void ApplyHighlighting()
    {
        foreach (var columnLink in ColumnLinks)
        {
            if (_currentHighlightTemplate == null)
            {
                columnLink.IsHighlighted = false;
                continue;
            }

            // Get the template's source file for precise matching
            var templateSourceFile = _currentHighlightTemplate.SourceFilePath;

            // Check if any linked column matches by name AND comes from the same file as the template
            var matchesOriginal = columnLink.LinkedColumns.Any(lc =>
            {
                var nameMatches = _currentHighlightTemplate.FindColumn(lc.Name) != null;
                if (!nameMatches) return false;

                // If template has source file info, require exact file match
                if (!string.IsNullOrEmpty(templateSourceFile))
                {
                    return FileNamesMatch(templateSourceFile, lc.SourceFile);
                }

                // Fallback: if no source file info in template, match by name only
                return true;
            });

            columnLink.IsHighlighted = matchesOriginal;
        }
    }

    /// <summary>
    /// Check if two file references match (comparing just the file name).
    /// templateSourcePath is a full path, columnSourceFile is just the file name.
    /// </summary>
    private static bool FileNamesMatch(string templateSourcePath, string? columnSourceFile)
    {
        if (string.IsNullOrEmpty(templateSourcePath) || string.IsNullOrEmpty(columnSourceFile))
            return false;

        // Extract just the file name from the template path (which is a full path)
        var templateFileName = Path.GetFileName(templateSourcePath);

        return string.Equals(templateFileName, columnSourceFile, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Unsubscribe from events
            if (_filesManager != null)
            {
                _filesManager.FileLoaded -= OnFilesChanged;
                _filesManager.FileRemoved -= OnFilesChanged;
            }

            // Cleanup all items
            foreach (var item in ColumnLinks)
            {
                item.Cleanup();
            }
            ColumnLinks.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
