using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Models;

/// <summary>
/// UI model for a single cross-file detection result.
/// Displayed as a card in the detection results panel.
/// </summary>
public class CrossFileApplyViewModel : ViewModelBase
{
    private bool _isSelected;

    public string FileName { get; }
    public string FilePath { get; }
    public string SheetName { get; }
    public bool IsMatch { get; }
    public string BoundsText { get; }
    public bool HasWarnings { get; }
    public string? WarningMessage { get; }
    public RegionDetectionResult Detection { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public CrossFileApplyViewModel(
        string filePath,
        string fileName,
        string sheetName,
        RegionDetectionResult detection)
    {
        FilePath = filePath;
        FileName = fileName;
        SheetName = sheetName;
        Detection = detection;
        IsMatch = detection.Found;

        if (detection.Found && detection.DetectedRegion != null)
        {
            var r = detection.DetectedRegion;
            int startRow = r.HeaderStartRow ?? r.DataStartRow;
            int endRow = r.DataEndRow ?? startRow;
            int dataRows = endRow - r.DataStartRow + 1;
            BoundsText = $"{sheetName}: rows {startRow + 1}-{endRow + 1} ({dataRows} rows)";
            _isSelected = true;

            if (detection.WasTruncated)
            {
                HasWarnings = true;
                WarningMessage = $"Stopped at source row count ({dataRows} rows). Target sheet continues — adjust boundary after applying.";
            }
        }
        else
        {
            BoundsText = "";
            WarningMessage = detection.Message ?? "Headers not found";
            _isSelected = false;
        }
    }
}
