using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// Event arguments for file-related actions requested from FileDetailsViewModel.
/// Used for actions like Remove, Clean, Retry, etc.
/// </summary>
public class FileActionEventArgs : EventArgs
{
    /// <summary>
    /// The file involved in the action
    /// </summary>
    public IFileLoadResultViewModel? File { get; }

    public FileActionEventArgs(IFileLoadResultViewModel? file)
    {
        File = file;
    }
}

/// <summary>
/// Event arguments raised when a template is saved.
/// </summary>
public class TemplateSavedEventArgs : EventArgs
{
    /// <summary>
    /// The template that was saved.
    /// </summary>
    public ExcelTemplate Template { get; }

    public TemplateSavedEventArgs(ExcelTemplate template)
    {
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }
}

/// <summary>
/// Event arguments raised when data normalization is completed.
/// </summary>
public class NormalizationCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Number of values that were modified during normalization.
    /// </summary>
    public int ModifiedCount { get; }

    /// <summary>
    /// Total number of rows processed.
    /// </summary>
    public int TotalRows { get; }

    /// <summary>
    /// Total number of columns processed.
    /// </summary>
    public int TotalColumns { get; }

    public NormalizationCompletedEventArgs(int modifiedCount, int totalRows, int totalColumns)
    {
        ModifiedCount = modifiedCount;
        TotalRows = totalRows;
        TotalColumns = totalColumns;
    }
}

/// <summary>
/// Event arguments raised when export is completed.
/// </summary>
public class ExportCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Path to the exported file.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Format of export (Excel, CSV).
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Number of rows exported.
    /// </summary>
    public int RowsExported { get; }

    /// <summary>
    /// Number of cells with normalization applied.
    /// </summary>
    public int NormalizedCellCount { get; }

    public ExportCompletedEventArgs(string outputPath, string format, int rowsExported, int normalizedCellCount)
    {
        OutputPath = outputPath;
        Format = format;
        RowsExported = rowsExported;
        NormalizedCellCount = normalizedCellCount;
    }
}

/// <summary>
/// Event arguments for clearing all regions of a specific file.
/// </summary>
public class ClearFileRegionsEventArgs : EventArgs
{
    public string FilePath { get; }
    public string FileName { get; }

    public ClearFileRegionsEventArgs(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;
    }
}

/// <summary>
/// Event arguments raised when the user renames a DataRegion via the sidebar.
/// </summary>
public class RenameRegionEventArgs : EventArgs
{
    public string FilePath { get; }
    public string SheetName { get; }
    public DataRegion Region { get; }
    public string NewName { get; }

    public RenameRegionEventArgs(string filePath, string sheetName, DataRegion region, string newName)
    {
        FilePath = filePath;
        SheetName = sheetName;
        Region = region;
        NewName = newName;
    }
}

/// <summary>
/// Event arguments for DataRegion creation/deletion.
/// </summary>
public class RegionEventArgs : EventArgs
{
    public string FilePath { get; }
    public string SheetName { get; }
    public DataRegion Region { get; }

    public RegionEventArgs(string filePath, string sheetName, DataRegion region)
    {
        FilePath = filePath;
        SheetName = sheetName;
        Region = region;
    }
}

/// <summary>
/// Event arguments for requesting file selection from the regions sidebar.
/// </summary>
public class FileSelectRequestedEventArgs : EventArgs
{
    public string FilePath { get; }

    public FileSelectRequestedEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}
