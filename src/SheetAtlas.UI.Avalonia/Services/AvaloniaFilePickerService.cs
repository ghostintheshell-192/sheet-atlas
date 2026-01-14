using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Services;

public class AvaloniaFilePickerService : IFilePickerService
{
    private readonly ILogService _logger;
    private static readonly string[] _excelFileFilters = new[] { "*.xlsx", "*.xlsm", "*.xltx", "*.xltm", "*.xls", "*.xlt", "*.csv" };

    public AvaloniaFilePickerService(ILogService logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }
        return null;
    }

    public async Task<IEnumerable<string>?> OpenFilesAsync(string title, string[]? fileTypeFilters = null)
    {
        try
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
            {
                _logger.LogWarning("StorageProvider not available for file picker", "AvaloniaFilePickerService");
                return null;
            }

            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true
            };

            if (fileTypeFilters != null && fileTypeFilters.Any())
            {
                var fileTypes = new List<FilePickerFileType>();

                // Excel files filter
                if (fileTypeFilters.Any(f => f.Contains("xlsx") || f.Contains("xls") || f.Contains("csv")))
                {
                    // All supported spreadsheet formats
                    fileTypes.Add(new FilePickerFileType("All Supported Formats")
                    {
                        Patterns = _excelFileFilters
                    });

                    // Modern Excel formats
                    fileTypes.Add(new FilePickerFileType("Excel Files (Modern)")
                    {
                        Patterns = new[] { "*.xlsx", "*.xlsm", "*.xltx", "*.xltm" }
                    });

                    // Legacy Excel formats
                    fileTypes.Add(new FilePickerFileType("Excel Files (Legacy)")
                    {
                        Patterns = new[] { "*.xls", "*.xlt" }
                    });

                    // CSV files
                    fileTypes.Add(new FilePickerFileType("CSV Files")
                    {
                        Patterns = new[] { "*.csv" }
                    });
                }

                // All files filter
                fileTypes.Add(FilePickerFileTypes.All);

                options.FileTypeFilter = fileTypes;
            }

            var result = await storageProvider.OpenFilePickerAsync(options);

            if (result == null || !result.Any())
            {
                _logger.LogInfo("User cancelled file picker or selected no files", "AvaloniaFilePickerService");
                return null;
            }

            var filePaths = result.Select(f => f.Path.LocalPath).ToList();
            _logger.LogInfo($"User selected {filePaths.Count} files", "AvaloniaFilePickerService");

            return filePaths;
        }
        catch (OperationCanceledException)
        {
            // User cancelled - this is normal operation
            _logger.LogInfo("File picker cancelled by user", "AvaloniaFilePickerService");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Permission denied to access file system
            _logger.LogError("Access denied when opening file picker", ex, "AvaloniaFilePickerService");
            return null;
        }
        catch (Exception ex)
        {
            // Unexpected errors - platform issues, file system errors, etc.
            _logger.LogError("Unexpected error opening file picker", ex, "AvaloniaFilePickerService");
            return null;
        }
    }

    public async Task<string?> SaveFileAsync(string title, string? defaultExtension = null, string[]? fileTypeFilters = null)
    {
        try
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
            {
                _logger.LogWarning("StorageProvider not available for save file picker", "AvaloniaFilePickerService");
                return null;
            }

            var options = new FilePickerSaveOptions
            {
                Title = title
            };

            if (!string.IsNullOrEmpty(defaultExtension))
            {
                options.DefaultExtension = defaultExtension;
            }

            if (fileTypeFilters != null && fileTypeFilters.Any())
            {
                var fileTypes = new List<FilePickerFileType>();

                foreach (var filter in fileTypeFilters)
                {
                    var extension = filter.Replace("*", "").Replace(".", "");
                    fileTypes.Add(new FilePickerFileType($"{extension.ToUpper()} Files")
                    {
                        Patterns = new[] { filter }
                    });
                }

                fileTypes.Add(FilePickerFileTypes.All);
                options.FileTypeChoices = fileTypes;
            }

            var result = await storageProvider.SaveFilePickerAsync(options);

            if (result == null)
            {
                _logger.LogInfo("User cancelled save file picker", "AvaloniaFilePickerService");
                return null;
            }

            var filePath = result.Path.LocalPath;
            _logger.LogInfo($"User selected save location: {filePath}", "AvaloniaFilePickerService");

            return filePath;
        }
        catch (OperationCanceledException)
        {
            // User cancelled - this is normal operation
            _logger.LogInfo("Save file picker cancelled by user", "AvaloniaFilePickerService");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Permission denied to access file system
            _logger.LogError("Access denied when opening save file picker", ex, "AvaloniaFilePickerService");
            return null;
        }
        catch (Exception ex)
        {
            // Unexpected errors - platform issues, file system errors, etc.
            _logger.LogError("Unexpected error opening save file picker", ex, "AvaloniaFilePickerService");
            return null;
        }
    }

    public async Task<string?> SelectFolderAsync(string title)
    {
        try
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
            {
                _logger.LogWarning("StorageProvider not available for folder picker", "AvaloniaFilePickerService");
                return null;
            }

            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result == null || !result.Any())
            {
                _logger.LogInfo("User cancelled folder picker", "AvaloniaFilePickerService");
                return null;
            }

            var folderPath = result[0].Path.LocalPath;
            _logger.LogInfo($"User selected folder: {folderPath}", "AvaloniaFilePickerService");

            return folderPath;
        }
        catch (OperationCanceledException)
        {
            // User cancelled - this is normal operation
            _logger.LogInfo("Folder picker cancelled by user", "AvaloniaFilePickerService");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Permission denied to access file system
            _logger.LogError("Access denied when opening folder picker", ex, "AvaloniaFilePickerService");
            return null;
        }
        catch (Exception ex)
        {
            // Unexpected errors - platform issues, file system errors, etc.
            _logger.LogError("Unexpected error opening folder picker", ex, "AvaloniaFilePickerService");
            return null;
        }
    }
}
