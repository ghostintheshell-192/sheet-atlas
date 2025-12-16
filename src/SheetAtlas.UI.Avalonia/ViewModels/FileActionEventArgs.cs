using SheetAtlas.Core.Domain.Entities;

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
