using System.Collections.ObjectModel;
using System.Windows.Input;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.Logging.Services;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class FileDetailsViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private readonly IFileLogService _fileLogService;
    private IFileLoadResultViewModel? _selectedFile;
    private bool _isLoadingHistory;
    private bool _disposed;

    public IFileLoadResultViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetField(ref _selectedFile, value))
            {
                UpdateDetails();
            }
        }
    }

    public ObservableCollection<FileDetailProperty> Properties { get; } = new();
    public ObservableCollection<ErrorLogRowViewModel> ErrorLogs { get; } = new();

    public bool IsLoadingHistory
    {
        get => _isLoadingHistory;
        set => SetField(ref _isLoadingHistory, value);
    }

    // Basic information properties (for direct binding)
    public string FilePath => SelectedFile?.FilePath ?? string.Empty;
    public string FileSize => SelectedFile != null ? FormatFileSize(SelectedFile.FilePath) : string.Empty;
    public bool HasErrorLogs => ErrorLogs.Count > 0;

    public ICommand RemoveFromListCommand { get; }
    public ICommand CleanAllDataCommand { get; }
    public ICommand RemoveNotificationCommand { get; }
    public ICommand TryAgainCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand ClearCommand { get; }

    public FileDetailsViewModel(
        ILogService logger,
        IFileLogService fileLogService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogService = fileLogService ?? throw new ArgumentNullException(nameof(fileLogService));

        RemoveFromListCommand = new RelayCommand(() => { ExecuteRemoveFromList(); return Task.CompletedTask; });
        CleanAllDataCommand = new RelayCommand(() => { ExecuteCleanAllData(); return Task.CompletedTask; });
        RemoveNotificationCommand = new RelayCommand(() => { ExecuteRemoveNotification(); return Task.CompletedTask; });
        TryAgainCommand = new RelayCommand(() => { ExecuteTryAgain(); return Task.CompletedTask; });
        ViewErrorLogCommand = new RelayCommand(OpenErrorLogAsync);
        RetryCommand = new RelayCommand(ExecuteRetryAsync);
        ClearCommand = new RelayCommand(ExecuteClearAsync);
    }

    private void UpdateDetails()
    {
        Properties.Clear();
        ErrorLogs.Clear();

        if (SelectedFile == null) return;

        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSize));

        _ = LoadErrorHistoryAsync();
    }

    private void AddSuccessDetails()
    {
        Properties.Add(new FileDetailProperty("Load Results", ""));
        Properties.Add(new FileDetailProperty("", ""));

        Properties.Add(new FileDetailProperty("Status", "Success"));
        Properties.Add(new FileDetailProperty("Warnings", "No problems detected"));

        if (SelectedFile?.File?.Sheets != null)
        {
            var sheetNames = string.Join(", ", SelectedFile.File.Sheets.Keys.Take(3));
            if (SelectedFile.File.Sheets.Count > 3)
                sheetNames += $" (+{SelectedFile.File.Sheets.Count - 3} more)";

            Properties.Add(new FileDetailProperty("Sheets", $"{SelectedFile.File.Sheets.Count} ({sheetNames})"));
        }
    }

    private void AddPartialSuccessDetails()
    {
        Properties.Add(new FileDetailProperty("Load Results", ""));

        var separator = new FileDetailProperty("", "");
        if (SelectedFile?.File?.Errors?.Any() == true)
        {
            separator.ActionText = "View Error Log";
            separator.ActionCommand = ViewErrorLogCommand;
        }
        Properties.Add(separator);

        Properties.Add(new FileDetailProperty("Status", "Partially Loaded"));

        if (SelectedFile?.File?.Errors?.Any() == true)
        {
            var errorCount = SelectedFile.File.Errors.Count;
            var issueWord = errorCount == 1 ? "issue" : "issues";
            Properties.Add(new FileDetailProperty("Warnings", $"{errorCount} {issueWord} detected"));
        }

        if (SelectedFile?.File?.Sheets != null && SelectedFile.File.Sheets.Count > 0)
        {
            var sheetNames = string.Join(", ", SelectedFile.File.Sheets.Keys);
            Properties.Add(new FileDetailProperty("Sheets", $"{SelectedFile.File.Sheets.Count} ({sheetNames})"));
        }
    }

    public ICommand ViewErrorLogCommand { get; }

    private async Task LoadErrorHistoryAsync()
    {
        if (SelectedFile == null || IsLoadingHistory)
            return;

        IsLoadingHistory = true;

        try
        {
            var logEntries = await _fileLogService.GetFileLogHistoryAsync(SelectedFile.FilePath);

            ErrorLogs.Clear();

            // Flatten all errors from all attempts into a single list
            foreach (var entry in logEntries.OrderByDescending(e => e.LoadAttempt.Timestamp))
            {
                if (entry.Errors == null || entry.Errors.Count == 0)
                {
                    ErrorLogs.Add(new ErrorLogRowViewModel(
                        timestamp: entry.LoadAttempt.Timestamp,
                        logLevel: LogSeverity.Info,
                        message: "File loaded successfully"
                    ));
                }
                else
                {
                    foreach (var error in entry.Errors)
                    {
                        ErrorLogs.Add(new ErrorLogRowViewModel(
                            timestamp: error.Timestamp,
                            logLevel: error.Level,
                            message: error.Message
                        ));
                    }
                }
            }

            OnPropertyChanged(nameof(HasErrorLogs));
            _logger.LogInfo($"Loaded {ErrorLogs.Count} error log entries for file: {SelectedFile.FileName}", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load error history for file: {SelectedFile?.FileName}", ex, "FileDetailsViewModel");
        }
        finally
        {
            IsLoadingHistory = false;
            OnPropertyChanged(nameof(HasErrorLogs));
        }
    }

    private Task ExecuteRetryAsync()
    {
        if (SelectedFile == null) return Task.CompletedTask;

        _logger.LogInfo($"Retry requested for file: {SelectedFile.FileName}", "FileDetailsViewModel");

        TryAgainRequested?.Invoke(this, new FileActionEventArgs(SelectedFile));
        return Task.CompletedTask;
    }

    private async Task ExecuteClearAsync()
    {
        if (SelectedFile == null) return;

        _logger.LogInfo($"Clear logs requested for file: {SelectedFile.FileName}", "FileDetailsViewModel");

        try
        {
            await _fileLogService.DeleteFileLogsAsync(SelectedFile.FilePath);

            ErrorLogs.Clear();
            OnPropertyChanged(nameof(HasErrorLogs));

            _logger.LogInfo($"Logs cleared successfully for file: {SelectedFile.FileName}", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to clear logs for file: {SelectedFile.FileName}", ex, "FileDetailsViewModel");
        }
    }

    private Task OpenErrorLogAsync()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(appDataPath, "SheetAtlas", "Logs");
        var logFile = Path.Combine(logDirectory, string.Format("app-{0:yyyy-MM-dd}.log", DateTime.Now));

        if (!File.Exists(logFile))
        {
            _logger.LogInfo("Error log viewer opened - no log file found", "FileDetailsViewModel");
            return Task.CompletedTask;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = logFile,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            _logger.LogInfo($"Opened error log file: {logFile}", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to open error log file", ex, "FileDetailsViewModel");
        }

        return Task.CompletedTask;
    }

    private static string GetFileFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ".xlsx (Excel 2007+)",
            ".xls" => ".xls (Legacy Excel)",
            ".xlsm" => ".xlsm (Excel Macro)",
            ".csv" => ".csv (Comma Separated)",
            _ => $"{extension} (Unknown)"
        };
    }

    private static string FormatFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return "Unknown";

            var bytes = fileInfo.Length;
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        return string.Concat("...", path.AsSpan(path.Length - maxLength + 3));
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

    // Action handlers - these will be implemented to communicate with MainWindowViewModel
    private void ExecuteRemoveFromList()
    {
        _logger.LogInfo($"Remove from list requested for: {SelectedFile?.FileName}", "FileDetailsViewModel");
        // Will be handled by MainWindowViewModel
        RemoveFromListRequested?.Invoke(this, new FileActionEventArgs(SelectedFile));
    }

    private void ExecuteCleanAllData()
    {
        _logger.LogInfo($"Clean all data requested for: {SelectedFile?.FileName}", "FileDetailsViewModel");
        CleanAllDataRequested?.Invoke(this, new FileActionEventArgs(SelectedFile));
    }

    private void ExecuteRemoveNotification()
    {
        _logger.LogInfo($"Remove notification requested for: {SelectedFile?.FileName}", "FileDetailsViewModel");
        RemoveNotificationRequested?.Invoke(this, new FileActionEventArgs(SelectedFile));
    }

    private void ExecuteTryAgain()
    {
        _logger.LogInfo($"Try again requested for: {SelectedFile?.FileName}", "FileDetailsViewModel");
        TryAgainRequested?.Invoke(this, new FileActionEventArgs(SelectedFile));
    }

    // Events to communicate with parent ViewModels
    public event EventHandler<FileActionEventArgs>? RemoveFromListRequested;
    public event EventHandler<FileActionEventArgs>? CleanAllDataRequested;
    public event EventHandler<FileActionEventArgs>? RemoveNotificationRequested;
    public event EventHandler<FileActionEventArgs>? TryAgainRequested;

    public void Dispose()
    {
        if (_disposed) return;

        // Clear event subscribers to prevent memory leaks
        RemoveFromListRequested = null;
        CleanAllDataRequested = null;
        RemoveNotificationRequested = null;
        TryAgainRequested = null;

        // Clear collections
        Properties.Clear();
        ErrorLogs.Clear();

        // Release reference
        _selectedFile = null;

        _disposed = true;
    }
}
