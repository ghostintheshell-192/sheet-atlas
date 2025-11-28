using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.Logging.Services;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.UI.Avalonia.ViewModels;

public class FileDetailsViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private readonly IFileLogService _fileLogService;
    private readonly ITemplateValidationService _templateValidationService;
    private readonly ITemplateRepository _templateRepository;
    private readonly IFilePickerService _filePickerService;
    private readonly IDataNormalizationService _dataNormalizationService;
    private readonly IExcelWriterService _excelWriterService;

    private IFileLoadResultViewModel? _selectedFile;
    private bool _isLoadingHistory;
    private bool _disposed;

    // Template validation fields
    private ExcelTemplate? _selectedTemplate;
    private ValidationReport? _validationReport;
    private bool _isValidating;
    private bool _isLoadingTemplates;

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

    // Template validation properties
    public ObservableCollection<ExcelTemplate> AvailableTemplates { get; } = new();
    public ObservableCollection<ValidationIssueViewModel> ValidationIssues { get; } = new();

    public ExcelTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetField(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(CanValidate));
                // Clear previous validation when template changes
                ClearValidationResult();
            }
        }
    }

    public bool IsValidating
    {
        get => _isValidating;
        set => SetField(ref _isValidating, value);
    }

    public bool CanValidate => SelectedTemplate != null && SelectedFile?.File != null && !IsValidating;

    public bool HasValidationResult => _validationReport != null;

    public bool HasValidationIssues => _validationReport?.AllIssues.Count > 0;

    public string ValidationStatusIcon => _validationReport?.Status switch
    {
        ValidationStatus.Valid => "\u2705",            // Green checkmark
        ValidationStatus.ValidWithWarnings => "\u26A0", // Warning
        ValidationStatus.Invalid => "\u274C",           // Red X
        ValidationStatus.Failed => "\u26D4",            // No entry
        _ => ""
    };

    public string ValidationStatusText => _validationReport?.Status switch
    {
        ValidationStatus.Valid => "Valid",
        ValidationStatus.ValidWithWarnings => $"{_validationReport.TotalWarningCount} warning(s)",
        ValidationStatus.Invalid => $"{_validationReport.TotalErrorCount} error(s)",
        ValidationStatus.Failed => "Failed",
        _ => ""
    };

    public string ValidationResultStatus => _validationReport?.Status switch
    {
        ValidationStatus.Valid => "VALID",
        ValidationStatus.ValidWithWarnings => "VALID",
        ValidationStatus.Invalid => "INVALID",
        ValidationStatus.Failed => "FAILED",
        _ => ""
    };

    public IBrush ValidationResultBackground => _validationReport?.Status switch
    {
        ValidationStatus.Valid => new SolidColorBrush(Color.Parse("#22C55E")),          // Green
        ValidationStatus.ValidWithWarnings => new SolidColorBrush(Color.Parse("#F59E0B")), // Amber
        ValidationStatus.Invalid => new SolidColorBrush(Color.Parse("#EF4444")),        // Red
        ValidationStatus.Failed => new SolidColorBrush(Color.Parse("#6B7280")),         // Gray
        _ => new SolidColorBrush(Colors.Transparent)
    };

    public string ValidationSummary => _validationReport?.Summary ?? "";

    public ICommand RemoveFromListCommand { get; }
    public ICommand CleanAllDataCommand { get; }
    public ICommand RemoveNotificationCommand { get; }
    public ICommand TryAgainCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand SaveAsTemplateCommand { get; }
    public ICommand LoadTemplateCommand { get; }
    public ICommand NormalizeAllCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public bool CanNormalize => HasValidationIssues && SelectedFile?.File != null;

    public FileDetailsViewModel(
        ILogService logger,
        IFileLogService fileLogService,
        ITemplateValidationService templateValidationService,
        ITemplateRepository templateRepository,
        IFilePickerService filePickerService,
        IDataNormalizationService dataNormalizationService,
        IExcelWriterService excelWriterService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogService = fileLogService ?? throw new ArgumentNullException(nameof(fileLogService));
        _templateValidationService = templateValidationService ?? throw new ArgumentNullException(nameof(templateValidationService));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _dataNormalizationService = dataNormalizationService ?? throw new ArgumentNullException(nameof(dataNormalizationService));
        _excelWriterService = excelWriterService ?? throw new ArgumentNullException(nameof(excelWriterService));

        RemoveFromListCommand = new RelayCommand(() => { ExecuteRemoveFromList(); return Task.CompletedTask; });
        CleanAllDataCommand = new RelayCommand(() => { ExecuteCleanAllData(); return Task.CompletedTask; });
        RemoveNotificationCommand = new RelayCommand(() => { ExecuteRemoveNotification(); return Task.CompletedTask; });
        TryAgainCommand = new RelayCommand(() => { ExecuteTryAgain(); return Task.CompletedTask; });
        ViewErrorLogCommand = new RelayCommand(OpenErrorLogAsync);
        RetryCommand = new RelayCommand(ExecuteRetryAsync);
        ClearCommand = new RelayCommand(ExecuteClearAsync);
        ValidateCommand = new RelayCommand(ExecuteValidateAsync);
        SaveAsTemplateCommand = new RelayCommand(ExecuteSaveAsTemplateAsync);
        LoadTemplateCommand = new RelayCommand(ExecuteLoadTemplateAsync);
        NormalizeAllCommand = new RelayCommand(ExecuteNormalizeAllAsync);
        ExportExcelCommand = new RelayCommand(ExecuteExportExcelAsync);
        ExportCsvCommand = new RelayCommand(ExecuteExportCsvAsync);

        // Load available templates on startup
        _ = LoadAvailableTemplatesAsync();
    }

    private void UpdateDetails()
    {
        Properties.Clear();
        ErrorLogs.Clear();

        // Clear validation when file changes
        ClearValidationResult();

        OnPropertyChanged(nameof(HasSelectedFile));

        if (SelectedFile == null) return;

        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSize));
        OnPropertyChanged(nameof(CanValidate));

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

    #region Template Validation Methods

    private async Task LoadAvailableTemplatesAsync()
    {
        if (_isLoadingTemplates) return;

        _isLoadingTemplates = true;

        try
        {
            var templates = await _templateRepository.ListTemplatesAsync();

            AvailableTemplates.Clear();
            foreach (var summary in templates)
            {
                var template = await _templateRepository.LoadTemplateAsync(summary.Name);
                if (template != null)
                {
                    AvailableTemplates.Add(template);
                }
            }

            _logger.LogInfo($"Loaded {AvailableTemplates.Count} templates", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load templates", ex, "FileDetailsViewModel");
        }
        finally
        {
            _isLoadingTemplates = false;
        }
    }

    private async Task ExecuteValidateAsync()
    {
        if (SelectedFile?.File == null || SelectedTemplate == null)
            return;

        IsValidating = true;
        ClearValidationResult();

        try
        {
            _logger.LogInfo($"Validating file '{SelectedFile.FileName}' against template '{SelectedTemplate.Name}'", "FileDetailsViewModel");

            _validationReport = await _templateValidationService.ValidateAsync(
                SelectedFile.File,
                SelectedTemplate);

            // Populate issues for display
            foreach (var issue in _validationReport.AllIssues.OrderByDescending(i => i.Severity))
            {
                ValidationIssues.Add(new ValidationIssueViewModel(issue));
            }

            NotifyValidationPropertiesChanged();

            _logger.LogInfo($"Validation completed: {_validationReport.Status} ({_validationReport.TotalErrorCount} errors, {_validationReport.TotalWarningCount} warnings)", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Validation failed for file: {SelectedFile.FileName}", ex, "FileDetailsViewModel");
        }
        finally
        {
            IsValidating = false;
            OnPropertyChanged(nameof(CanValidate));
        }
    }

    private async Task ExecuteSaveAsTemplateAsync()
    {
        if (SelectedFile?.File == null)
            return;

        try
        {
            // Generate template name from file name
            var templateName = Path.GetFileNameWithoutExtension(SelectedFile.FileName);

            // Check if template already exists
            if (_templateRepository.TemplateExists(templateName))
            {
                // For now, just add a suffix. In the future, we could show a dialog
                templateName = $"{templateName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            _logger.LogInfo($"Creating template '{templateName}' from file '{SelectedFile.FileName}'", "FileDetailsViewModel");

            var template = await _templateValidationService.CreateTemplateFromFileAsync(
                SelectedFile.File,
                templateName);

            await _templateRepository.SaveTemplateAsync(template);

            // Refresh template list
            await LoadAvailableTemplatesAsync();

            // Select the newly created template
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.Name == templateName);

            _logger.LogInfo($"Template '{templateName}' created successfully with {template.Columns.Count} columns", "FileDetailsViewModel");

            // Raise event so UI can show confirmation
            TemplateSaved?.Invoke(this, new TemplateSavedEventArgs(template));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save template from file: {SelectedFile.FileName}", ex, "FileDetailsViewModel");
        }
    }

    private void ClearValidationResult()
    {
        _validationReport = null;
        ValidationIssues.Clear();
        NotifyValidationPropertiesChanged();
    }

    private void NotifyValidationPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasValidationResult));
        OnPropertyChanged(nameof(HasValidationIssues));
        OnPropertyChanged(nameof(ValidationStatusIcon));
        OnPropertyChanged(nameof(ValidationStatusText));
        OnPropertyChanged(nameof(ValidationResultStatus));
        OnPropertyChanged(nameof(ValidationResultBackground));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(CanNormalize));
    }

    /// <summary>
    /// Refresh the available templates list.
    /// Call this when templates are added/removed externally.
    /// </summary>
    public Task RefreshTemplatesAsync() => LoadAvailableTemplatesAsync();

    private async Task ExecuteLoadTemplateAsync()
    {
        try
        {
            var files = await _filePickerService.OpenFilesAsync(
                "Select Template File",
                new[] { "*.json" });

            var filePath = files?.FirstOrDefault();
            if (string.IsNullOrEmpty(filePath))
                return;

            _logger.LogInfo($"Loading template from: {filePath}", "FileDetailsViewModel");

            var template = await _templateRepository.LoadTemplateFromPathAsync(filePath);
            if (template == null)
            {
                _logger.LogWarning($"Failed to load template from: {filePath}", "FileDetailsViewModel");
                return;
            }

            // Import the template to the templates directory
            await _templateRepository.ImportTemplateAsync(filePath, overwrite: true);

            // Refresh the list and select the imported template
            await LoadAvailableTemplatesAsync();
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.Name == template.Name);

            _logger.LogInfo($"Template '{template.Name}' loaded successfully", "FileDetailsViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load template from file", ex, "FileDetailsViewModel");
        }
    }

    private async Task ExecuteNormalizeAllAsync()
    {
        if (SelectedFile?.File == null || !HasValidationIssues)
            return;

        try
        {
            _logger.LogInfo($"Normalizing data from file: {SelectedFile.FileName}", "FileDetailsViewModel");

            var sheet = SelectedFile.File.Sheets.Values.FirstOrDefault();
            if (sheet == null)
            {
                _logger.LogWarning("No sheet found to normalize", "FileDetailsViewModel");
                return;
            }

            var csvBuilder = new StringBuilder();
            int normalizedCount = 0;

            // Add header row
            csvBuilder.AppendLine(string.Join("\t", sheet.ColumnNames));

            // Process each data row
            foreach (var row in sheet.EnumerateDataRows())
            {
                var normalizedValues = new List<string>();

                for (int colIndex = 0; colIndex < row.ColumnCount; colIndex++)
                {
                    var cell = row[colIndex];
                    var effectiveValue = cell.EffectiveValue;
                    var numberFormat = cell.Metadata?.NumberFormat;

                    // Normalize the cell value
                    var result = _dataNormalizationService.Normalize(
                        effectiveValue.ToString(),
                        numberFormat,
                        CellDataType.General,
                        DateSystem.Date1900);

                    string normalizedValue;
                    if (result.IsSuccess && result.CleanedValue != null)
                    {
                        normalizedValue = result.CleanedValue.ToString() ?? string.Empty;
                        // Check if value was modified
                        if (!string.Equals(result.OriginalValue.ToString(), normalizedValue, StringComparison.Ordinal))
                            normalizedCount++;
                    }
                    else
                    {
                        normalizedValue = effectiveValue.ToString() ?? string.Empty;
                    }

                    // Escape for TSV (tab-separated)
                    normalizedValue = normalizedValue.Replace("\t", " ").Replace("\n", " ").Replace("\r", "");
                    normalizedValues.Add(normalizedValue);
                }

                csvBuilder.AppendLine(string.Join("\t", normalizedValues));
            }

            // Copy to clipboard via Avalonia
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            var clipboard = mainWindow?.Clipboard;

            if (clipboard != null)
            {
                await clipboard.SetTextAsync(csvBuilder.ToString());
                _logger.LogInfo($"Normalized data copied to clipboard ({normalizedCount} values modified, {sheet.DataRowCount} rows)", "FileDetailsViewModel");

                // Notify user via event
                NormalizationCompleted?.Invoke(this, new NormalizationCompletedEventArgs(
                    normalizedCount,
                    sheet.DataRowCount,
                    sheet.ColumnCount));
            }
            else
            {
                _logger.LogWarning("Clipboard not available", "FileDetailsViewModel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to normalize data: {ex.Message}", ex, "FileDetailsViewModel");
        }
    }

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

            // Generate default output path
            var originalPath = SelectedFile.FilePath;
            var directory = Path.GetDirectoryName(originalPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var baseName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(directory, $"{baseName}_normalized.xlsx");

            // Use file picker to let user choose location
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

            // Generate default output path
            var originalPath = SelectedFile.FilePath;
            var directory = Path.GetDirectoryName(originalPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var baseName = Path.GetFileNameWithoutExtension(originalPath);
            var outputPath = Path.Combine(directory, $"{baseName}_normalized.csv");

            // Use file picker to let user choose location
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

    // Events to communicate with parent ViewModels
    public event EventHandler<TemplateSavedEventArgs>? TemplateSaved;
    public event EventHandler<NormalizationCompletedEventArgs>? NormalizationCompleted;
    public event EventHandler<ExportCompletedEventArgs>? ExportCompleted;
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
        TemplateSaved = null;
        NormalizationCompleted = null;

        // Clear collections
        Properties.Clear();
        ErrorLogs.Clear();
        AvailableTemplates.Clear();
        ValidationIssues.Clear();

        // Release references
        _selectedFile = null;
        _selectedTemplate = null;
        _validationReport = null;

        _disposed = true;
    }
}
