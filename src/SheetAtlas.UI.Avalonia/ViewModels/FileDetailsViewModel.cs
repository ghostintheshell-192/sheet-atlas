using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.Logging.Services;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for file details display. Shows basic file information,
/// notifications/errors, and export functionality.
/// Template management has been moved to TemplateManagementViewModel.
/// </summary>
public class FileDetailsViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private readonly IFileLogService _fileLogService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDataNormalizationService _dataNormalizationService;
    private readonly IExcelWriterService _excelWriterService;
    private readonly ISettingsService _settingsService;

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
    public bool HasSelectedFile => SelectedFile != null;

    // Commands
    public ICommand RemoveFromListCommand { get; }
    public ICommand CleanAllDataCommand { get; }
    public ICommand RemoveNotificationCommand { get; }
    public ICommand TryAgainCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ViewErrorLogCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public FileDetailsViewModel(
        ILogService logger,
        IFileLogService fileLogService,
        IFilePickerService filePickerService,
        IDataNormalizationService dataNormalizationService,
        IExcelWriterService excelWriterService,
        ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogService = fileLogService ?? throw new ArgumentNullException(nameof(fileLogService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _dataNormalizationService = dataNormalizationService ?? throw new ArgumentNullException(nameof(dataNormalizationService));
        _excelWriterService = excelWriterService ?? throw new ArgumentNullException(nameof(excelWriterService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        RemoveFromListCommand = new RelayCommand(() => { ExecuteRemoveFromList(); return Task.CompletedTask; });
        CleanAllDataCommand = new RelayCommand(() => { ExecuteCleanAllData(); return Task.CompletedTask; });
        RemoveNotificationCommand = new RelayCommand(() => { ExecuteRemoveNotification(); return Task.CompletedTask; });
        TryAgainCommand = new RelayCommand(() => { ExecuteTryAgain(); return Task.CompletedTask; });
        ViewErrorLogCommand = new RelayCommand(OpenErrorLogAsync);
        RetryCommand = new RelayCommand(ExecuteRetryAsync);
        ClearCommand = new RelayCommand(ExecuteClearAsync);
        ExportExcelCommand = new RelayCommand(ExecuteExportExcelAsync);
        ExportCsvCommand = new RelayCommand(ExecuteExportCsvAsync);
    }

    private void UpdateDetails()
    {
        Properties.Clear();
        ErrorLogs.Clear();

        OnPropertyChanged(nameof(HasSelectedFile));

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

    private async Task LoadErrorHistoryAsync()
    {
        if (SelectedFile == null || IsLoadingHistory)
            return;

        IsLoadingHistory = true;

        try
        {
            var logEntries = await _fileLogService.GetFileLogHistoryAsync(SelectedFile.FilePath);

            ErrorLogs.Clear();

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

    // Action handlers
    private void ExecuteRemoveFromList()
    {
        _logger.LogInfo($"Remove from list requested for: {SelectedFile?.FileName}", "FileDetailsViewModel");
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

    #region Export Methods

    private async Task ExecuteExportExcelAsync()
    {
        if (SelectedFile?.File == null)
            return;

        try
        {
            var sheet = SelectedFile.File.Sheets.Values.FirstOrDefault();
            if (sheet == null)
            {
                _logger.LogWarning("No sheet found to export", "FileDetailsViewModel");
                return;
            }

            var originalPath = SelectedFile.FilePath;
            var outputFolder = _settingsService.Current.FileLocations.OutputFolder;
            var baseName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(outputFolder, $"{baseName}_normalized.xlsx");

            var savedPath = await _filePickerService.SaveFileAsync(
                "Export Normalized Excel",
                outputPath,
                new[] { "*.xlsx" });

            if (string.IsNullOrEmpty(savedPath))
                return;

            _logger.LogInfo($"Exporting to Excel: {savedPath}", "FileDetailsViewModel");

            var result = await _excelWriterService.WriteToExcelAsync(sheet, savedPath);

            if (result.IsSuccess)
            {
                _logger.LogInfo($"Excel export completed: {result.RowsExported} rows, {result.NormalizedCellCount} normalized cells, {result.FileSizeBytes} bytes in {result.Duration.TotalMilliseconds:F0}ms", "FileDetailsViewModel");

                ExportCompleted?.Invoke(this, new ExportCompletedEventArgs(
                    savedPath,
                    "Excel",
                    result.RowsExported,
                    result.NormalizedCellCount));
            }
            else
            {
                _logger.LogError($"Excel export failed: {result.ErrorMessage}", "FileDetailsViewModel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to export Excel: {ex.Message}", ex, "FileDetailsViewModel");
        }
    }

    private async Task ExecuteExportCsvAsync()
    {
        if (SelectedFile?.File == null)
            return;

        try
        {
            var sheet = SelectedFile.File.Sheets.Values.FirstOrDefault();
            if (sheet == null)
            {
                _logger.LogWarning("No sheet found to export", "FileDetailsViewModel");
                return;
            }

            var originalPath = SelectedFile.FilePath;
            var outputFolder = _settingsService.Current.FileLocations.OutputFolder;
            var baseName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(outputFolder, $"{baseName}_normalized.csv");

            var savedPath = await _filePickerService.SaveFileAsync(
                "Export Normalized CSV",
                outputPath,
                new[] { "*.csv" });

            if (string.IsNullOrEmpty(savedPath))
                return;

            _logger.LogInfo($"Exporting to CSV: {savedPath}", "FileDetailsViewModel");

            var result = await _excelWriterService.WriteToCsvAsync(sheet, savedPath);

            if (result.IsSuccess)
            {
                _logger.LogInfo($"CSV export completed: {result.RowsExported} rows, {result.NormalizedCellCount} normalized cells, {result.FileSizeBytes} bytes in {result.Duration.TotalMilliseconds:F0}ms", "FileDetailsViewModel");

                ExportCompleted?.Invoke(this, new ExportCompletedEventArgs(
                    savedPath,
                    "CSV",
                    result.RowsExported,
                    result.NormalizedCellCount));
            }
            else
            {
                _logger.LogError($"CSV export failed: {result.ErrorMessage}", "FileDetailsViewModel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to export CSV: {ex.Message}", ex, "FileDetailsViewModel");
        }
    }

    #endregion

    // Events
    public event EventHandler<ExportCompletedEventArgs>? ExportCompleted;
    public event EventHandler<FileActionEventArgs>? RemoveFromListRequested;
    public event EventHandler<FileActionEventArgs>? CleanAllDataRequested;
    public event EventHandler<FileActionEventArgs>? RemoveNotificationRequested;
    public event EventHandler<FileActionEventArgs>? TryAgainRequested;

    public void Dispose()
    {
        if (_disposed) return;

        RemoveFromListRequested = null;
        CleanAllDataRequested = null;
        RemoveNotificationRequested = null;
        TryAgainRequested = null;
        ExportCompleted = null;

        Properties.Clear();
        ErrorLogs.Clear();

        _selectedFile = null;

        _disposed = true;
    }
}
