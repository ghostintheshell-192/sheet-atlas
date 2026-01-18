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

        // Subscribe to child IsIncluded changes to update GroupIncluded
        foreach (var column in LinkedColumns)
        {
            column.PropertyChanged += OnLinkedColumnPropertyChanged;
        }
    }

    private void OnLinkedColumnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinkedColumnViewModel.IsIncluded))
        {
            NotifyGroupIncludedChanged();
        }
    }

    /// <summary>
    /// Clear internal references for garbage collection.
    /// </summary>
    public void Cleanup()
    {
        foreach (var column in LinkedColumns)
        {
            column.PropertyChanged -= OnLinkedColumnPropertyChanged;
        }
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

    // === Warning Properties ===

    public bool HasCaseVariations => _link.HasCaseVariations;

    public bool HasTypeVariations => _link.HasTypeVariations;

    public bool HasWarnings => _link.HasWarnings;

    public string? WarningMessage => _link.WarningMessage;

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

    /// <summary>
    /// Three-state inclusion for the group: true if all included, false if none, null if mixed.
    /// </summary>
    public bool? GroupIncluded
    {
        get
        {
            var includedCount = LinkedColumns.Count(c => c.IsIncluded);
            if (includedCount == 0) return false;
            if (includedCount == LinkedColumns.Count) return true;
            return null; // mixed state
        }
        set
        {
            // Three-state checkbox cycles: checked -> indeterminate -> unchecked -> checked
            // When user clicks on checked, it goes to indeterminate (null), not unchecked
            // We interpret null as "toggle off" when coming from checked state
            if (value.HasValue)
            {
                SetGroupInclusion(value.Value);
            }
            else
            {
                // null (indeterminate) clicked - toggle all off
                SetGroupInclusion(false);
            }
        }
    }

    /// <summary>
    /// Set inclusion state for all columns in this group.
    /// </summary>
    public void SetGroupInclusion(bool include)
    {
        foreach (var column in LinkedColumns)
        {
            column.IsIncluded = include;
        }
        OnPropertyChanged(nameof(GroupIncluded));
    }

    /// <summary>
    /// Notify that GroupIncluded may have changed (called when child IsIncluded changes).
    /// </summary>
    public void NotifyGroupIncludedChanged()
    {
        OnPropertyChanged(nameof(GroupIncluded));
    }

    public ColumnLink GetUpdatedLink() =>
        _link with { SemanticName = _semanticName };
}

/// <summary>
/// ViewModel item for a single linked column (child of ColumnLinkViewModel).
/// </summary>
public class LinkedColumnViewModel : ViewModelBase
{
    private readonly LinkedColumn _column;
    private bool _isIncluded = true;

    public LinkedColumnViewModel(LinkedColumn column)
    {
        _column = column;
    }

    public string Name => _column.Name;

    public string? SourceFile => _column.SourceFile;

    public string SourceDisplay => _column.SourceDisplay;

    public DataType DetectedType => _column.DetectedType;

    public string TypeDisplay => _column.DetectedType.ToString();

    /// <summary>
    /// Whether this column is included in search/comparison/export operations.
    /// </summary>
    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetField(ref _isIncluded, value);
    }
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
        SelectAllCommand = new RelayCommand(() => { SelectAll(); return Task.CompletedTask; });
        ClearAllCommand = new RelayCommand(() => { ClearAll(); return Task.CompletedTask; });
        InvertCommand = new RelayCommand(() => { InvertSelection(); return Task.CompletedTask; });

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
        source.PropertyChanged -= OnColumnLinkPropertyChanged;
        source.Cleanup();
        ColumnLinks.Remove(source);
        target.PropertyChanged -= OnColumnLinkPropertyChanged;
        target.Cleanup();
        ColumnLinks.Remove(target);

        // Insert merged at original target position
        var mergedVm = new ColumnLinkViewModel(mergedLink);
        mergedVm.PropertyChanged += OnColumnLinkPropertyChanged;
        if (targetIndex >= 0 && targetIndex <= ColumnLinks.Count)
            ColumnLinks.Insert(targetIndex, mergedVm);
        else
            ColumnLinks.Add(mergedVm);

        OnPropertyChanged(nameof(HasColumns));
        NotifyFilterChanged();

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
        column.PropertyChanged -= OnColumnLinkPropertyChanged;
        column.Cleanup();
        ColumnLinks.Remove(column);

        // Insert ungrouped items at the original position
        foreach (var link in ungrouped.Reverse())
        {
            var vm = new ColumnLinkViewModel(link);
            vm.PropertyChanged += OnColumnLinkPropertyChanged;
            if (index >= 0 && index <= ColumnLinks.Count)
                ColumnLinks.Insert(index, vm);
            else
                ColumnLinks.Add(vm);
        }

        OnPropertyChanged(nameof(HasColumns));
        NotifyFilterChanged();

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

    /// <summary>
    /// Get semantic names for columns from a specific file.
    /// Returns a dictionary mapping original column names to their semantic names.
    /// Only includes columns where SemanticName differs from the original Name.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetSemanticNamesForFile(string fileName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columnLink in ColumnLinks)
        {
            // Find linked columns that belong to this file
            foreach (var linkedColumn in columnLink.LinkedColumns)
            {
                if (string.Equals(linkedColumn.SourceFile, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Only add if semantic name differs from original
                    if (!string.Equals(columnLink.SemanticName, linkedColumn.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        result[linkedColumn.Name] = columnLink.SemanticName;
                    }
                }
            }
        }

        return result;
    }

    public ObservableCollection<ColumnLinkViewModel> ColumnLinks { get; }

    public ICommand RefreshCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand InvertCommand { get; }

    /// <summary>
    /// Whether there are any columns to display.
    /// </summary>
    public bool HasColumns => ColumnLinks.Count > 0;

    /// <summary>
    /// Total number of column groups (semantic columns).
    /// </summary>
    public int TotalCount => ColumnLinks.Count;

    /// <summary>
    /// Number of column groups currently included (at least one LinkedColumn included).
    /// </summary>
    public int IncludedCount => ColumnLinks.Count(g => g.LinkedColumns.Any(c => c.IsIncluded));

    /// <summary>
    /// Status text showing inclusion count (e.g., "5 of 10 groups").
    /// </summary>
    public string FilterStatusText => $"{IncludedCount} of {TotalCount}";

    /// <summary>
    /// Get the semantic names of all included columns.
    /// Returns the semantic name of the group for each included child column.
    /// </summary>
    public IEnumerable<string> GetIncludedColumns()
    {
        foreach (var group in ColumnLinks)
        {
            foreach (var column in group.LinkedColumns)
            {
                if (column.IsIncluded)
                {
                    yield return group.SemanticName;
                }
            }
        }
    }

    /// <summary>
    /// Get included column names grouped by their original names (for search filtering).
    /// Returns all original column names that are included.
    /// </summary>
    public IEnumerable<string> GetIncludedColumnNames()
    {
        var includedNames = new List<string>();
        foreach (var group in ColumnLinks)
        {
            foreach (var column in group.LinkedColumns)
            {
                if (column.IsIncluded)
                {
                    includedNames.Add(column.Name);
                }
            }
        }
        System.Diagnostics.Debug.WriteLine($"[ColumnLinking] GetIncludedColumnNames: {includedNames.Count} columns included");
        return includedNames;
    }

    /// <summary>
    /// Select all columns.
    /// </summary>
    public void SelectAll()
    {
        foreach (var group in ColumnLinks)
        {
            group.SetGroupInclusion(true);
        }
        NotifyFilterChanged();
    }

    /// <summary>
    /// Clear all column selections.
    /// </summary>
    public void ClearAll()
    {
        foreach (var group in ColumnLinks)
        {
            group.SetGroupInclusion(false);
        }
        NotifyFilterChanged();
    }

    /// <summary>
    /// Invert current selection.
    /// </summary>
    public void InvertSelection()
    {
        foreach (var group in ColumnLinks)
        {
            foreach (var column in group.LinkedColumns)
            {
                column.IsIncluded = !column.IsIncluded;
            }
            group.NotifyGroupIncludedChanged();
        }
        NotifyFilterChanged();
    }

    private void NotifyFilterChanged()
    {
        OnPropertyChanged(nameof(IncludedCount));
        OnPropertyChanged(nameof(FilterStatusText));
    }

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
            item.PropertyChanged -= OnColumnLinkPropertyChanged;
            item.Cleanup();
        }
        ColumnLinks.Clear();

        foreach (var link in links)
        {
            var vm = new ColumnLinkViewModel(link);
            vm.PropertyChanged += OnColumnLinkPropertyChanged;
            ColumnLinks.Add(vm);
        }

        OnPropertyChanged(nameof(HasColumns));
        NotifyFilterChanged();

        // Re-apply highlighting after refresh
        ApplyHighlighting();
    }

    private void OnColumnLinkPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnLinkViewModel.GroupIncluded))
        {
            NotifyFilterChanged();
        }
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
                item.PropertyChanged -= OnColumnLinkPropertyChanged;
                item.Cleanup();
            }
            ColumnLinks.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
