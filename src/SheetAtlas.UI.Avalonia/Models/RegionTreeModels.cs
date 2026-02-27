using System.Collections.ObjectModel;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Models;

/// <summary>
/// Top-level group in the Regions sidebar: one per loaded file.
/// </summary>
public class FileRegionGroup : ViewModelBase
{
    private bool _isExpanded;

    public string FileName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public ObservableCollection<SheetRegionGroup> Sheets { get; } = new();

    public int TotalRegionCount => Sheets.Sum(s => s.Regions.Count);
    public bool HasAnyWarnings => Sheets.Any(s => s.HasAnyWarnings);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}

/// <summary>
/// Second-level group: one per sheet within a file.
/// </summary>
public class SheetRegionGroup : ViewModelBase
{
    private bool _isExpanded;

    public string SheetName { get; init; } = "";
    public ObservableCollection<RegionItem> Regions { get; } = new();
    public bool HasAnyWarnings => Regions.Any(r => r.HasWarnings);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}

/// <summary>
/// Shared helper for formatting DataRegion bounds as "A1:E50 (50x5)".
/// </summary>
public static class RegionBoundsFormatter
{
    public static string Format(DataRegion region)
    {
        int startRow = region.HeaderStartRow ?? region.DataStartRow;
        int endRow = region.DataEndRow ?? startRow;
        int startCol = region.StartColumn ?? 0;
        int endCol = region.EndColumn ?? startCol;

        string startCell = $"{GetColumnLetter(startCol)}{startRow + 1}";
        string endCell = $"{GetColumnLetter(endCol)}{endRow + 1}";
        int rows = endRow - startRow + 1;
        int cols = endCol - startCol + 1;

        return $"{startCell}:{endCell} ({rows}x{cols})";
    }

    private static string GetColumnLetter(int colIndex)
    {
        string result = "";
        int col = colIndex;
        do
        {
            result = (char)('A' + col % 26) + result;
            col = col / 26 - 1;
        } while (col >= 0);
        return result;
    }
}

/// <summary>
/// Leaf item: represents a single DataRegion.
/// Extends ViewModelBase to support inline rename (IsEditing/EditName).
/// </summary>
public class RegionItem : ViewModelBase
{
    private bool _isEditing;
    private string _editName = "";

    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string SheetName { get; init; } = "";
    public DataRegion Region { get; init; } = null!;

    public string BoundsText => RegionBoundsFormatter.Format(Region);

    public bool IsAutoDetected => Region.IsAutoDetected;
    public bool HasWarnings => !string.IsNullOrEmpty(Region.WarningMessage);
    public string? WarningMessage => Region.WarningMessage;

    /// <summary>
    /// Whether the inline rename TextBox is active for this item.
    /// Setting to true initialises EditName with the current Name.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetField(ref _isEditing, value) && value)
                EditName = Name;
        }
    }

    /// <summary>
    /// Mutable name used while the TextBox is active. Committed on Enter/LostFocus.
    /// </summary>
    public string EditName
    {
        get => _editName;
        set => SetField(ref _editName, value);
    }
}

/// <summary>
/// Group in the "By Region" view: one per unique region name across all files.
/// </summary>
public class RegionNameGroup : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public string RegionName { get; init; } = "";
    public ObservableCollection<RegionFileEntry> FileEntries { get; } = new();
    public int FileCount => FileEntries.Count;
    public bool IsMultiFile => FileCount > 1;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}

/// <summary>
/// Leaf in the "By Region" view: one file+sheet occurrence of a named region.
/// </summary>
public class RegionFileEntry
{
    public string FileName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string SheetName { get; init; } = "";
    public DataRegion Region { get; init; } = null!;
    public string BoundsText => RegionBoundsFormatter.Format(Region);
}
