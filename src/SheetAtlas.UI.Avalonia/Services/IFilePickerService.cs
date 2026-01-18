namespace SheetAtlas.UI.Avalonia.Services;

public interface IFilePickerService
{
    Task<IEnumerable<string>?> OpenFilesAsync(string title, string[]? fileTypeFilters = null);
    Task<string?> SaveFileAsync(string title, string? defaultExtension = null, string[]? fileTypeFilters = null);
    Task<string?> SelectFolderAsync(string title);
}
