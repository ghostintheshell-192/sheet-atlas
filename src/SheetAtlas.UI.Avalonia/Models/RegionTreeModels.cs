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
/// Leaf item: represents a single DataRegion.
/// </summary>
public class RegionItem
{
    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string SheetName { get; init; } = "";
    public DataRegion Region { get; init; } = null!;

    public string BoundsText
    {
        get
        {
            int startRow = Region.HeaderStartRow ?? Region.DataStartRow;
            int endRow = Region.DataEndRow ?? startRow;
            int startCol = Region.StartColumn ?? 0;
            int endCol = Region.EndColumn ?? startCol;

            string startCell = $"{GetColumnLetter(startCol)}{startRow + 1}";
            string endCell = $"{GetColumnLetter(endCol)}{endRow + 1}";
            int rows = endRow - startRow + 1;
            int cols = endCol - startCol + 1;

            return $"{startCell}:{endCell} ({rows}x{cols})";
        }
    }

    public bool IsAutoDetected => Region.IsAutoDetected;
    public bool HasWarnings => !string.IsNullOrEmpty(Region.WarningMessage);
    public string? WarningMessage => Region.WarningMessage;

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
