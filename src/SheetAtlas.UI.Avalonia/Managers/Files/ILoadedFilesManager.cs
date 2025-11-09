using System.Collections.ObjectModel;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Managers.Files;

/// <summary>
/// Manages the collection of loaded Excel files and their lifecycle.
/// Handles loading, removal, and retry operations for failed loads.
/// </summary>
public interface ILoadedFilesManager : IDisposable
{
    /// <summary>
    /// Gets the read-only collection of currently loaded files.
    /// </summary>
    ReadOnlyObservableCollection<IFileLoadResultViewModel> LoadedFiles { get; }

    /// <summary>
    /// Loads Excel files from the specified file paths.
    /// Automatically checks for duplicates and handles errors.
    /// </summary>
    /// <param name="filePaths">Collection of file paths to load</param>
    /// <returns>Task representing the async operation</returns>
    Task LoadFilesAsync(IEnumerable<string> filePaths);

    /// <summary>
    /// Removes a file from the loaded files collection.
    /// </summary>
    /// <param name="file">The file to remove</param>
    /// <param name="isRetry">True if this removal is part of a retry operation (preserves UI selection)</param>
    void RemoveFile(IFileLoadResultViewModel? file, bool isRetry = false);

    /// <summary>
    /// Retries loading a file that previously failed.
    /// Removes the old failed entry and attempts to reload.
    /// </summary>
    /// <param name="filePath">Path of the file to retry loading</param>
    /// <returns>Task representing the async operation</returns>
    Task RetryLoadAsync(string filePath);
    new void Dispose();

    /// <summary>
    /// Raised when a file is successfully loaded (or loaded with errors).
    /// </summary>
    event EventHandler<FileLoadedEventArgs>? FileLoaded;

    /// <summary>
    /// Raised when a file is removed from the collection.
    /// </summary>
    event EventHandler<FileRemovedEventArgs>? FileRemoved;

    /// <summary>
    /// Raised when a file load operation fails completely.
    /// </summary>
    event EventHandler<FileLoadFailedEventArgs>? FileLoadFailed;

    /// <summary>
    /// Raised when a file is reloaded (during retry operation).
    /// </summary>
    event EventHandler<FileReloadedEventArgs>? FileReloaded;
}

/// <summary>
/// Event args for file loaded event.
/// </summary>
public class FileLoadedEventArgs : EventArgs
{
    public IFileLoadResultViewModel File { get; }
    public bool HasErrors { get; }

    public FileLoadedEventArgs(IFileLoadResultViewModel file, bool hasErrors)
    {
        File = file;
        HasErrors = hasErrors;
    }
}

/// <summary>
/// Event args for file removed event.
/// </summary>
public class FileRemovedEventArgs : EventArgs
{
    public IFileLoadResultViewModel File { get; }

    /// <summary>
    /// Indicates whether this removal is part of a retry operation.
    /// When true, UI should preserve selection state to avoid flickering.
    /// </summary>
    public bool IsRetry { get; }

    public FileRemovedEventArgs(IFileLoadResultViewModel file, bool isRetry = false)
    {
        File = file;
        IsRetry = isRetry;
    }
}

/// <summary>
/// Event args for file load failed event.
/// </summary>
public class FileLoadFailedEventArgs : EventArgs
{
    public string FilePath { get; }
    public Exception Exception { get; }

    public FileLoadFailedEventArgs(string filePath, Exception exception)
    {
        FilePath = filePath;
        Exception = exception;
    }
}

/// <summary>
/// Event args for file reloaded event (during retry).
/// </summary>
public class FileReloadedEventArgs : EventArgs
{
    public IFileLoadResultViewModel NewFile { get; }
    public string FilePath { get; }

    public FileReloadedEventArgs(IFileLoadResultViewModel newFile, string filePath)
    {
        NewFile = newFile;
        FilePath = filePath;
    }
}
