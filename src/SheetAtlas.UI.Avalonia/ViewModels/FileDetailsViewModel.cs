using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    private readonly IDataRegionPersistenceService _dataRegionPersistenceService;

    private IFileLoadResultViewModel? _selectedFile;
    private bool _isLoadingHistory;
    private bool _disposed;
    private Func<string, IReadOnlyDictionary<string, string>>? _getSemanticNamesForFile;
    private Func<IEnumerable<string>>? _getIncludedColumns;
    private string? _selectedSheetName;
    private SASheetData? _currentSheetData;
    private IReadOnlyDictionary<string, DataRegion>? _currentRegions;
    private DataRegion? _canvasSelectedRegion;
    private DataRegion? _activeRegion;
    private string _newRegionName = "";
    private string? _regionErrorMessage;
    private bool _isResizeSaved;
    private bool _isEditingRegion;
    private DataRegion? _originalRegionBeforeEdit;
    private CancellationTokenSource? _resizeFeedbackCts;

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

    /// <summary>
    /// Sheet names available in the currently selected file.
    /// </summary>
    public string[] SheetNames =>
        SelectedFile?.File?.Sheets.Keys.ToArray() ?? Array.Empty<string>();

    /// <summary>
    /// Currently selected sheet name in the sheet selector.
    /// </summary>
    public string? SelectedSheetName
    {
        get => _selectedSheetName;
        set
        {
            if (SetField(ref _selectedSheetName, value))
                UpdateCurrentSheet();
        }
    }

    /// <summary>
    /// Sheet data for the currently selected sheet (bound to SheetGridCanvas).
    /// </summary>
    public SASheetData? CurrentSheetData
    {
        get => _currentSheetData;
        private set => SetField(ref _currentSheetData, value);
    }

    /// <summary>
    /// DataRegions for the currently selected sheet (bound to SheetGridCanvas overlay).
    /// </summary>
    public IReadOnlyDictionary<string, DataRegion>? CurrentRegions
    {
        get => _currentRegions;
        private set
        {
            // Always notify: SASheetData.DataRegions returns the same dictionary reference
            // even after Add/Remove, so reference equality check would skip updates.
            _currentRegions = value;
            OnPropertyChanged(nameof(CurrentRegions));
            OnPropertyChanged(nameof(RegionNames));
            OnPropertyChanged(nameof(HasRegions));
        }
    }

    /// <summary>
    /// Whether a sheet with data is available for display.
    /// </summary>
    public bool HasSheetData => CurrentSheetData != null;

    /// <summary>
    /// Region selected by dragging on the canvas (two-way bound).
    /// </summary>
    public DataRegion? CanvasSelectedRegion
    {
        get => _canvasSelectedRegion;
        set
        {
            if (SetField(ref _canvasSelectedRegion, value))
            {
                OnPropertyChanged(nameof(HasCanvasSelection));
                OnPropertyChanged(nameof(SelectionBoundsText));
            }
        }
    }

    /// <summary>
    /// Name for the new region being created.
    /// </summary>
    public string NewRegionName
    {
        get => _newRegionName;
        set => SetField(ref _newRegionName, value);
    }

    /// <summary>
    /// Whether a canvas selection is active.
    /// </summary>
    public bool HasCanvasSelection => CanvasSelectedRegion != null;

    /// <summary>
    /// Error message shown in the creation panel when region creation fails.
    /// </summary>
    public string? RegionErrorMessage
    {
        get => _regionErrorMessage;
        private set
        {
            if (SetField(ref _regionErrorMessage, value))
                OnPropertyChanged(nameof(HasRegionError));
        }
    }

    public bool HasRegionError => !string.IsNullOrEmpty(RegionErrorMessage);

    /// <summary>
    /// Brief feedback shown after resize save. Auto-clears after 2 seconds.
    /// </summary>
    public bool IsResizeSaved
    {
        get => _isResizeSaved;
        private set => SetField(ref _isResizeSaved, value);
    }

    /// <summary>
    /// Whether the user is currently editing (resizing) the active region.
    /// </summary>
    public bool IsEditingRegion
    {
        get => _isEditingRegion;
        private set
        {
            if (SetField(ref _isEditingRegion, value))
                OnPropertyChanged(nameof(IsNotEditingRegion));
        }
    }

    public bool IsNotEditingRegion => !IsEditingRegion;

    /// <summary>
    /// The region activated by clicking its badge on the canvas.
    /// </summary>
    public DataRegion? ActiveRegion
    {
        get => _activeRegion;
        set
        {
            if (SetField(ref _activeRegion, value))
            {
                OnPropertyChanged(nameof(HasActiveRegion));
                OnPropertyChanged(nameof(ActiveRegionInfoText));
            }
        }
    }

    public bool HasActiveRegion => ActiveRegion != null;

    /// <summary>
    /// Region names for the current sheet (used for chip display below the grid).
    /// </summary>
    public string[] RegionNames =>
        CurrentRegions?.Values
            .Select(r => r.Name)
            .ToArray() ?? Array.Empty<string>();

    public bool HasRegions => RegionNames.Length > 0;

    /// <summary>
    /// Human-readable info about the active region.
    /// </summary>
    public string ActiveRegionInfoText
    {
        get
        {
            var region = ActiveRegion;
            if (region == null) return "";

            int startRow = region.HeaderStartRow ?? region.DataStartRow;
            int endRow = region.DataEndRow ?? startRow;
            int startCol = region.StartColumn ?? 0;
            int endCol = region.EndColumn ?? startCol;

            string startCell = $"{GetColumnLetter(startCol)}{startRow + 1}";
            string endCell = $"{GetColumnLetter(endCol)}{endRow + 1}";
            int rows = endRow - startRow + 1;
            int cols = endCol - startCol + 1;

            return $"Region '{region.Name}' — {startCell}:{endCell} ({rows} rows x {cols} cols)";
        }
    }

    /// <summary>
    /// Human-readable description of the current selection bounds.
    /// </summary>
    public string SelectionBoundsText
    {
        get
        {
            var region = CanvasSelectedRegion;
            if (region == null) return "";

            int startRow = region.HeaderStartRow ?? region.DataStartRow;
            int endRow = region.DataEndRow ?? startRow;
            int startCol = region.StartColumn ?? 0;
            int endCol = region.EndColumn ?? startCol;

            string startCell = $"{GetColumnLetter(startCol)}{startRow + 1}";
            string endCell = $"{GetColumnLetter(endCol)}{endRow + 1}";
            int rows = endRow - startRow + 1;
            int cols = endCol - startCol + 1;

            return $"{startCell}:{endCell} ({rows} rows x {cols} cols)";
        }
    }

    /// <summary>
    /// Sets the provider for semantic names used during export.
    /// When set, export will use semantic names for column headers instead of original names.
    /// </summary>
    public void SetSemanticNameProvider(Func<string, IReadOnlyDictionary<string, string>> provider)
    {
        _getSemanticNamesForFile = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Sets the provider for included columns used during export.
    /// When set, export will only include columns returned by this function.
    /// </summary>
    public void SetIncludedColumnsProvider(Func<IEnumerable<string>> provider)
    {
        _getIncludedColumns = provider ?? throw new ArgumentNullException(nameof(provider));
    }

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
    public ICommand CreateRegionCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand DeleteActiveRegionCommand { get; }
    public ICommand ClearActiveRegionCommand { get; }
    public ICommand ActivateRegionCommand { get; }
    public ICommand StartEditRegionCommand { get; }
    public ICommand SaveRegionEditCommand { get; }
    public ICommand CancelRegionEditCommand { get; }

    public FileDetailsViewModel(
        ILogService logger,
        IFileLogService fileLogService,
        IFilePickerService filePickerService,
        IDataNormalizationService dataNormalizationService,
        IExcelWriterService excelWriterService,
        ISettingsService settingsService,
        IDataRegionPersistenceService dataRegionPersistenceService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileLogService = fileLogService ?? throw new ArgumentNullException(nameof(fileLogService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _dataNormalizationService = dataNormalizationService ?? throw new ArgumentNullException(nameof(dataNormalizationService));
        _excelWriterService = excelWriterService ?? throw new ArgumentNullException(nameof(excelWriterService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _dataRegionPersistenceService = dataRegionPersistenceService ?? throw new ArgumentNullException(nameof(dataRegionPersistenceService));

        RemoveFromListCommand = new RelayCommand(() => { ExecuteRemoveFromList(); return Task.CompletedTask; });
        CleanAllDataCommand = new RelayCommand(() => { ExecuteCleanAllData(); return Task.CompletedTask; });
        RemoveNotificationCommand = new RelayCommand(() => { ExecuteRemoveNotification(); return Task.CompletedTask; });
        TryAgainCommand = new RelayCommand(() => { ExecuteTryAgain(); return Task.CompletedTask; });
        ViewErrorLogCommand = new RelayCommand(OpenErrorLogAsync);
        RetryCommand = new RelayCommand(ExecuteRetryAsync);
        ClearCommand = new RelayCommand(ExecuteClearAsync);
        ExportExcelCommand = new RelayCommand(ExecuteExportExcelAsync);
        ExportCsvCommand = new RelayCommand(ExecuteExportCsvAsync);
        CreateRegionCommand = new RelayCommand(ExecuteCreateRegionAsync);
        ClearSelectionCommand = new RelayCommand(() => { ClearCanvasSelection(); return Task.CompletedTask; });
        DeleteActiveRegionCommand = new RelayCommand(ExecuteDeleteActiveRegionAsync);
        ClearActiveRegionCommand = new RelayCommand(() => { ActiveRegion = null; return Task.CompletedTask; });
        ActivateRegionCommand = new RelayCommand<string>(name => ActivateRegionByName(name));
        StartEditRegionCommand = new RelayCommand(() => { StartEditRegion(); return Task.CompletedTask; });
        SaveRegionEditCommand = new RelayCommand(SaveRegionEditAsync);
        CancelRegionEditCommand = new RelayCommand(() => { CancelRegionEdit(); return Task.CompletedTask; });
    }

    private void UpdateDetails()
    {
        Properties.Clear();
        ErrorLogs.Clear();

        OnPropertyChanged(nameof(HasSelectedFile));
        OnPropertyChanged(nameof(SheetNames));

        if (SelectedFile == null)
        {
            SelectedSheetName = null;
            return;
        }

        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSize));

        // Auto-select first sheet
        var sheets = SelectedFile.File?.Sheets;
        SelectedSheetName = sheets?.Keys.FirstOrDefault();

        _ = LoadErrorHistoryAsync();
    }

    private void UpdateCurrentSheet()
    {
        if (IsEditingRegion) CancelRegionEdit();
        ActiveRegion = null;
        ClearCanvasSelection();

        if (SelectedFile?.File == null || _selectedSheetName == null)
        {
            CurrentSheetData = null;
            CurrentRegions = null;
        }
        else
        {
            CurrentSheetData = SelectedFile.File.GetSheet(_selectedSheetName);
            CurrentRegions = CurrentSheetData?.DataRegions;
        }

        OnPropertyChanged(nameof(HasSheetData));
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

    #region Region Creation

    private async Task ExecuteCreateRegionAsync()
    {
        var selection = CanvasSelectedRegion;
        if (selection == null || CurrentSheetData == null || SelectedFile?.File == null)
            return;

        RegionErrorMessage = null;

        var name = string.IsNullOrWhiteSpace(NewRegionName)
            ? $"Region {CurrentSheetData.DataRegions.Count + 1}"
            : NewRegionName.Trim();

        var region = selection with { Name = name };

        try
        {
            CurrentSheetData.AddDataRegion(region);
            _logger.LogInfo($"Region '{name}' created on sheet '{_selectedSheetName}'", "FileDetailsViewModel");

            // Persist
            await PersistRegionsAsync();

            // Refresh bindings
            CurrentRegions = CurrentSheetData.DataRegions;
            ClearCanvasSelection();

            RegionAdded?.Invoke(this, new RegionEventArgs(SelectedFile.FilePath, _selectedSheetName!, region));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Cannot create region: {ex.Message}", "FileDetailsViewModel");
            RegionErrorMessage = ex.Message;
        }
    }

    private async Task ExecuteDeleteActiveRegionAsync()
    {
        var region = ActiveRegion;
        if (region == null || CurrentSheetData == null) return;

        try
        {
            CurrentSheetData.RemoveDataRegion(region.Name);
            _logger.LogInfo($"Region '{region.Name}' deleted from sheet '{_selectedSheetName}'", "FileDetailsViewModel");

            ActiveRegion = null;
            await PersistRegionsAsync();
            CurrentRegions = CurrentSheetData.DataRegions;

            RegionDeleted?.Invoke(this, new RegionEventArgs(SelectedFile!.FilePath, _selectedSheetName!, region));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to delete region: {ex.Message}", ex, "FileDetailsViewModel");
        }
    }

    /// <summary>
    /// Updates a region after bottom-edge resize: removes old, adds new with updated bounds, persists.
    /// </summary>
    public async Task UpdateResizedRegionAsync(DataRegion resizedRegion)
    {
        if (CurrentSheetData == null || SelectedFile?.File == null) return;

        try
        {
            // Clear warning since user manually adjusted bounds
            var updated = resizedRegion with { WarningMessage = null };

            // Remove the old region by name, add the updated one
            CurrentSheetData.RemoveDataRegion(updated.Name);
            CurrentSheetData.AddDataRegion(updated);

            await PersistRegionsAsync();
            CurrentRegions = CurrentSheetData.DataRegions;
            ActiveRegion = updated;

            RegionResized?.Invoke(this, new RegionEventArgs(SelectedFile.FilePath, _selectedSheetName!, updated));
            _logger.LogInfo($"Region '{updated.Name}' resized on sheet '{_selectedSheetName}'", "FileDetailsViewModel");

            ShowResizeSavedFeedback();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update resized region: {ex.Message}", ex, "FileDetailsViewModel");
        }
    }

    public void ActivateRegionByName(string name)
    {
        if (CurrentRegions != null && CurrentRegions.TryGetValue(name, out var region))
            ActiveRegion = (ActiveRegion?.Name == name) ? null : region;
        else
            ActiveRegion = null;

        // Exit edit mode when switching regions
        if (IsEditingRegion)
            CancelRegionEdit();
    }

    /// <summary>
    /// Activate a region and immediately enter edit mode (used by sidebar navigation).
    /// </summary>
    public void ActivateRegionForEditing(string name)
    {
        if (CurrentRegions != null && CurrentRegions.TryGetValue(name, out var region))
        {
            ActiveRegion = region;
            StartEditRegion();
        }
    }

    private void StartEditRegion()
    {
        if (ActiveRegion == null) return;
        _originalRegionBeforeEdit = ActiveRegion;
        IsEditingRegion = true;
    }

    private async Task SaveRegionEditAsync()
    {
        if (ActiveRegion == null || !IsEditingRegion) return;

        await UpdateResizedRegionAsync(ActiveRegion);
        IsEditingRegion = false;
        _originalRegionBeforeEdit = null;
    }

    private void CancelRegionEdit()
    {
        if (_originalRegionBeforeEdit != null)
            ActiveRegion = _originalRegionBeforeEdit;

        IsEditingRegion = false;
        _originalRegionBeforeEdit = null;
    }

    private void ShowResizeSavedFeedback()
    {
        _resizeFeedbackCts?.Cancel();
        _resizeFeedbackCts = new CancellationTokenSource();
        var token = _resizeFeedbackCts.Token;

        IsResizeSaved = true;
        _ = Task.Delay(2000, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                IsResizeSaved = false;
        }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ClearCanvasSelection()
    {
        CanvasSelectedRegion = null;
        NewRegionName = "";
        RegionErrorMessage = null;
    }

    /// <summary>
    /// Refreshes the region display from current sheet data.
    /// Call after regions are loaded asynchronously (e.g., from persistence on file open).
    /// </summary>
    public void RefreshRegions()
    {
        if (CurrentSheetData != null)
            CurrentRegions = CurrentSheetData.DataRegions;
    }

    /// <summary>
    /// Persists current regions to disk and refreshes canvas bindings.
    /// Called by MainWindowViewModel when regions are deleted from sidebar.
    /// </summary>
    public async Task PersistAndRefreshRegionsAsync()
    {
        await PersistRegionsAsync();
        RefreshRegions();
    }

    private async Task PersistRegionsAsync()
    {
        if (SelectedFile?.File == null) return;

        var data = new DataRegionFile
        {
            LastModified = DateTime.UtcNow,
            Sheets = new Dictionary<string, SheetRegionsDto>()
        };

        foreach (var (sheetName, sheetData) in SelectedFile.File.Sheets)
        {
            var regions = sheetData.DataRegions;
            if (regions.Count > 0)
            {
                data.Sheets[sheetName] = new SheetRegionsDto
                {
                    Regions = new Dictionary<string, DataRegion>(regions)
                };
            }
        }

        await _dataRegionPersistenceService.SaveAsync(SelectedFile.FilePath, data);
    }

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

    #endregion

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

            // Get semantic names for this file if available
            var semanticNames = _getSemanticNamesForFile?.Invoke(SelectedFile.FileName);
            // Get included columns if available
            var includedColumns = _getIncludedColumns?.Invoke()?.ToList();
            var options = new ExcelExportOptions
            {
                SemanticNames = semanticNames,
                IncludedColumns = includedColumns
            };

            var result = await _excelWriterService.WriteToExcelAsync(sheet, savedPath, options);

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

            // Get semantic names for this file if available
            var semanticNames = _getSemanticNamesForFile?.Invoke(SelectedFile.FileName);
            // Get included columns if available
            var includedColumns = _getIncludedColumns?.Invoke()?.ToList();
            var options = new CsvExportOptions
            {
                SemanticNames = semanticNames,
                IncludedColumns = includedColumns
            };

            var result = await _excelWriterService.WriteToCsvAsync(sheet, savedPath, options);

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
    public event EventHandler<RegionEventArgs>? RegionAdded;
    public event EventHandler<RegionEventArgs>? RegionDeleted;
    public event EventHandler<RegionEventArgs>? RegionResized;

    public void Dispose()
    {
        if (_disposed) return;

        _resizeFeedbackCts?.Cancel();
        _resizeFeedbackCts?.Dispose();

        RemoveFromListRequested = null;
        CleanAllDataRequested = null;
        RemoveNotificationRequested = null;
        TryAgainRequested = null;
        ExportCompleted = null;
        RegionAdded = null;
        RegionDeleted = null;
        RegionResized = null;

        Properties.Clear();
        ErrorLogs.Clear();

        _selectedFile = null;

        _disposed = true;
    }
}
