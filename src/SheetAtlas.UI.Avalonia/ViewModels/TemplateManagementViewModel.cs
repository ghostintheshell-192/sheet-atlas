using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Templates tab. Manages the template library and template operations.
/// Supports single file (create/validate) and multi-file (batch validate/apply) operations.
/// </summary>
public class TemplateManagementViewModel : ViewModelBase, IDisposable
{
    private readonly ILogService _logger;
    private readonly ITemplateValidationService _templateValidationService;
    private readonly ITemplateRepository _templateRepository;
    private readonly IFilePickerService _filePickerService;

    private TemplateSummary? _selectedTemplateSummary;
    private ExcelTemplate? _selectedTemplateDetails;
    private bool _isLoadingTemplates;
    private bool _isValidating;
    private bool _disposed;

    // Provider for semantic names from column linking
    private Func<string, IReadOnlyDictionary<string, string>>? _semanticNameProvider;

    // Selected files from main window (can be single or multiple)
    private IReadOnlyList<IFileLoadResultViewModel> _selectedFiles = Array.Empty<IFileLoadResultViewModel>();

    public ObservableCollection<TemplateSummary> TemplateLibrary { get; } = new();
    public ObservableCollection<FileValidationResultViewModel> BatchValidationResults { get; } = new();

    /// <summary>
    /// Selected template in the library list.
    /// </summary>
    public TemplateSummary? SelectedTemplateSummary
    {
        get => _selectedTemplateSummary;
        set
        {
            if (SetField(ref _selectedTemplateSummary, value))
            {
                _ = LoadTemplateDetailsAsync();
                ClearValidationResult();
                NotifyCommandsCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Full template details (loaded when a template is selected).
    /// </summary>
    public ExcelTemplate? SelectedTemplateDetails
    {
        get => _selectedTemplateDetails;
        private set
        {
            if (SetField(ref _selectedTemplateDetails, value))
            {
                OnPropertyChanged(nameof(HasSelectedTemplate));
                OnPropertyChanged(nameof(TemplateColumnCount));
                OnPropertyChanged(nameof(TemplateRequiredColumnCount));
                OnPropertyChanged(nameof(TemplateDescription));
                // Notify command states after template details are loaded
                NotifyCommandsCanExecuteChanged();
                // Notify listeners (e.g., ColumnLinkingViewModel) for highlighting
                SelectedTemplateChanged?.Invoke(this, new SelectedTemplateChangedEventArgs(value));
            }
        }
    }

    public bool IsLoadingTemplates
    {
        get => _isLoadingTemplates;
        private set => SetField(ref _isLoadingTemplates, value);
    }

    public bool IsValidating
    {
        get => _isValidating;
        private set
        {
            if (SetField(ref _isValidating, value))
            {
                NotifyCommandsCanExecuteChanged();
            }
        }
    }

    // Template details for display
    public bool HasSelectedTemplate => SelectedTemplateDetails != null;
    public int TemplateColumnCount => SelectedTemplateDetails?.Columns.Count ?? 0;
    public int TemplateRequiredColumnCount => SelectedTemplateDetails?.Columns.Count(c => c.IsRequired) ?? 0;
    public string TemplateDescription => SelectedTemplateDetails?.Description ?? "No description";

    // File selection info
    public int SelectedFileCount => _selectedFiles.Count;
    public bool HasSelectedFiles => _selectedFiles.Count > 0;
    public bool HasSingleFileSelected => _selectedFiles.Count == 1;
    public bool HasMultipleFilesSelected => _selectedFiles.Count > 1;
    public string SelectedFilesText => _selectedFiles.Count switch
    {
        0 => "No files selected",
        1 => _selectedFiles[0].FileName,
        _ => $"{_selectedFiles.Count} files selected"
    };

    // Batch validation state (aggregated from all file results)
    public bool HasValidationResult => BatchValidationResults.Count > 0;
    public bool HasValidationIssues => BatchValidationResults.Any(r => r.HasIssues);

    public int TotalFilesValidated => BatchValidationResults.Count;
    public int FilesWithErrors => BatchValidationResults.Count(r => r.Status == ValidationStatus.Invalid || r.Status == ValidationStatus.Failed);
    public int FilesWithWarnings => BatchValidationResults.Count(r => r.Status == ValidationStatus.ValidWithWarnings);
    public int FilesValid => BatchValidationResults.Count(r => r.Status == ValidationStatus.Valid);

    public string ValidationStatusIcon
    {
        get
        {
            if (!HasValidationResult) return "";
            if (FilesWithErrors > 0) return "\u274C";
            if (FilesWithWarnings > 0) return "\u26A0";
            return "\u2705";
        }
    }

    public string ValidationStatusText
    {
        get
        {
            if (!HasValidationResult) return "";
            if (FilesWithErrors > 0) return $"{FilesWithErrors} file(s) with errors";
            if (FilesWithWarnings > 0) return $"{FilesWithWarnings} file(s) with warnings";
            return $"{FilesValid} file(s) valid";
        }
    }

    public string ValidationResultStatus
    {
        get
        {
            if (!HasValidationResult) return "";
            if (FilesWithErrors > 0) return "INVALID";
            if (FilesWithWarnings > 0) return "VALID";
            return "VALID";
        }
    }

    public IBrush ValidationResultBackground
    {
        get
        {
            if (!HasValidationResult) return new SolidColorBrush(Colors.Transparent);
            if (FilesWithErrors > 0) return new SolidColorBrush(Color.Parse("#EF4444"));
            if (FilesWithWarnings > 0) return new SolidColorBrush(Color.Parse("#F59E0B"));
            return new SolidColorBrush(Color.Parse("#22C55E"));
        }
    }

    public string ValidationSummary
    {
        get
        {
            if (!HasValidationResult) return "";
            var totalErrors = BatchValidationResults.Sum(r => r.ErrorCount);
            var totalWarnings = BatchValidationResults.Sum(r => r.WarningCount);
            return $"{TotalFilesValidated} file(s) validated: {totalErrors} error(s), {totalWarnings} warning(s)";
        }
    }

    // Can execute conditions
    public bool CanCreateTemplate => HasSingleFileSelected && _selectedFiles[0].File != null && !IsValidating;
    public bool CanValidate => HasSelectedTemplate && HasSelectedFiles && !IsValidating;
    public bool CanDeleteTemplate => HasSelectedTemplate && !IsValidating;
    public bool CanImportTemplate => !IsValidating;
    public bool CanUpdateTemplate => HasSelectedTemplate && HasSingleFileSelected && !IsValidating;

    // Commands
    public ICommand CreateTemplateCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand ImportTemplateCommand { get; }
    public ICommand RefreshLibraryCommand { get; }
    public ICommand UpdateTemplateCommand { get; }

    public TemplateManagementViewModel(
        ILogService logger,
        ITemplateValidationService templateValidationService,
        ITemplateRepository templateRepository,
        IFilePickerService filePickerService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateValidationService = templateValidationService ?? throw new ArgumentNullException(nameof(templateValidationService));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));

        CreateTemplateCommand = new RelayCommand(ExecuteCreateTemplateAsync);
        ValidateCommand = new RelayCommand(ExecuteValidateAsync);
        DeleteTemplateCommand = new RelayCommand(ExecuteDeleteTemplateAsync);
        ImportTemplateCommand = new RelayCommand(ExecuteImportTemplateAsync);
        RefreshLibraryCommand = new RelayCommand(async () => await LoadTemplateLibraryAsync());
        UpdateTemplateCommand = new RelayCommand(ExecuteUpdateTemplateAsync);

        // Load templates on startup
        _ = LoadTemplateLibraryAsync();
    }

    /// <summary>
    /// Update the selected files from MainWindowViewModel.
    /// </summary>
    public void SetSelectedFiles(IReadOnlyList<IFileLoadResultViewModel> files)
    {
        _selectedFiles = files ?? Array.Empty<IFileLoadResultViewModel>();

        OnPropertyChanged(nameof(SelectedFileCount));
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(HasSingleFileSelected));
        OnPropertyChanged(nameof(HasMultipleFilesSelected));
        OnPropertyChanged(nameof(SelectedFilesText));
        OnPropertyChanged(nameof(CanCreateTemplate));
        OnPropertyChanged(nameof(CanValidate));

        // Clear validation when file selection changes
        ClearValidationResult();
    }

    /// <summary>
    /// Set the provider function for semantic names from column linking.
    /// The function takes a file name and returns a dictionary of column name -> semantic name.
    /// </summary>
    public void SetSemanticNameProvider(Func<string, IReadOnlyDictionary<string, string>> provider)
    {
        _semanticNameProvider = provider;
    }

    private async Task LoadTemplateLibraryAsync()
    {
        if (IsLoadingTemplates) return;

        IsLoadingTemplates = true;

        try
        {
            var templates = await _templateRepository.ListTemplatesAsync();

            TemplateLibrary.Clear();
            foreach (var template in templates)
            {
                TemplateLibrary.Add(template);
            }

            _logger.LogInfo($"Loaded {TemplateLibrary.Count} templates into library", "TemplateManagementViewModel");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load template library", ex, "TemplateManagementViewModel");
        }
        finally
        {
            IsLoadingTemplates = false;
        }
    }

    private async Task LoadTemplateDetailsAsync()
    {
        if (_selectedTemplateSummary == null)
        {
            SelectedTemplateDetails = null;
            return;
        }

        try
        {
            SelectedTemplateDetails = await _templateRepository.LoadTemplateAsync(_selectedTemplateSummary.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load template details: {_selectedTemplateSummary.Name}", ex, "TemplateManagementViewModel");
            SelectedTemplateDetails = null;
        }
    }

    private async Task ExecuteCreateTemplateAsync()
    {
        if (!CanCreateTemplate) return;

        var file = _selectedFiles[0];
        if (file.File == null) return;

        try
        {
            // Generate template name from file name
            var templateName = Path.GetFileNameWithoutExtension(file.FileName);

            // Check if template already exists
            if (_templateRepository.TemplateExists(templateName))
            {
                // Add timestamp suffix to avoid collision
                templateName = $"{templateName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            _logger.LogInfo($"Creating template '{templateName}' from file '{file.FileName}'", "TemplateManagementViewModel");

            var template = await _templateValidationService.CreateTemplateFromFileAsync(
                file.File,
                templateName);

            // Apply semantic names from column linking if available
            ApplySemanticNamesToTemplate(template, file.FileName);

            await _templateRepository.SaveTemplateAsync(template);

            // Refresh library
            await LoadTemplateLibraryAsync();

            // Select the newly created template
            SelectedTemplateSummary = TemplateLibrary.FirstOrDefault(t => t.Name == templateName);

            _logger.LogInfo($"Template '{templateName}' created successfully with {template.Columns.Count} columns", "TemplateManagementViewModel");

            TemplateCreated?.Invoke(this, new TemplateEventArgs(template));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to create template from file: {file.FileName}", ex, "TemplateManagementViewModel");
        }
    }

    private async Task ExecuteValidateAsync()
    {
        if (!CanValidate || SelectedTemplateDetails == null) return;

        IsValidating = true;
        ClearValidationResult();

        try
        {
            _logger.LogInfo($"Validating {_selectedFiles.Count} file(s) against template '{SelectedTemplateDetails.Name}'", "TemplateManagementViewModel");

            // Validate all selected files
            foreach (var file in _selectedFiles)
            {
                if (file.File == null) continue;

                try
                {
                    var report = await _templateValidationService.ValidateAsync(
                        file.File,
                        SelectedTemplateDetails);

                    var resultViewModel = new FileValidationResultViewModel(file.FileName, report);
                    BatchValidationResults.Add(resultViewModel);

                    _logger.LogInfo($"Validated '{file.FileName}': {report.Status} ({report.TotalErrorCount} errors, {report.TotalWarningCount} warnings)", "TemplateManagementViewModel");
                }
                catch (Exception ex)
                {
                    // Create a failed result for this file
                    var failedResult = new FileValidationResultViewModel(file.FileName, ex.Message);
                    BatchValidationResults.Add(failedResult);

                    _logger.LogError($"Validation failed for '{file.FileName}'", ex, "TemplateManagementViewModel");
                }
            }

            NotifyValidationPropertiesChanged();

            _logger.LogInfo($"Batch validation completed: {TotalFilesValidated} files, {FilesValid} valid, {FilesWithWarnings} with warnings, {FilesWithErrors} with errors", "TemplateManagementViewModel");

            // Fire event for each result (or aggregate event if needed)
            foreach (var result in BatchValidationResults.Where(r => r.Report != null))
            {
                ValidationCompleted?.Invoke(this, new ValidationCompletedEventArgs(result.Report!));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Batch validation failed", ex, "TemplateManagementViewModel");
        }
        finally
        {
            IsValidating = false;
        }
    }

    private async Task ExecuteDeleteTemplateAsync()
    {
        if (!CanDeleteTemplate || _selectedTemplateSummary == null) return;

        var templateName = _selectedTemplateSummary.Name;

        try
        {
            _logger.LogInfo($"Deleting template: {templateName}", "TemplateManagementViewModel");

            var deleted = await _templateRepository.DeleteTemplateAsync(templateName);

            if (deleted)
            {
                _logger.LogInfo($"Template '{templateName}' deleted successfully", "TemplateManagementViewModel");

                SelectedTemplateSummary = null;
                await LoadTemplateLibraryAsync();

                TemplateDeleted?.Invoke(this, new TemplateEventArgs(templateName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to delete template: {templateName}", ex, "TemplateManagementViewModel");
        }
    }

    private async Task ExecuteImportTemplateAsync()
    {
        if (!CanImportTemplate) return;

        try
        {
            var files = await _filePickerService.OpenFilesAsync(
                "Import Template",
                new[] { "*.json" });

            var filePath = files?.FirstOrDefault();
            if (string.IsNullOrEmpty(filePath)) return;

            _logger.LogInfo($"Importing template from: {filePath}", "TemplateManagementViewModel");

            var template = await _templateRepository.ImportTemplateAsync(filePath, overwrite: true);

            await LoadTemplateLibraryAsync();

            // Select the imported template
            SelectedTemplateSummary = TemplateLibrary.FirstOrDefault(t => t.Name == template.Name);

            _logger.LogInfo($"Template '{template.Name}' imported successfully", "TemplateManagementViewModel");

            TemplateImported?.Invoke(this, new TemplateEventArgs(template));
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to import template", ex, "TemplateManagementViewModel");
        }
    }

    private async Task ExecuteUpdateTemplateAsync()
    {
        if (!CanUpdateTemplate || SelectedTemplateDetails == null) return;

        var file = _selectedFiles[0];
        if (file.File == null) return;

        try
        {
            var template = SelectedTemplateDetails;
            var templateName = template.Name;

            _logger.LogInfo($"Updating template '{templateName}' with semantic names from '{file.FileName}'", "TemplateManagementViewModel");

            // Apply semantic names from column linking
            ApplySemanticNamesToTemplate(template, file.FileName);

            // Save the updated template (overwrite existing)
            await _templateRepository.SaveTemplateAsync(template, overwrite: true);

            // Refresh library to show updated template
            await LoadTemplateLibraryAsync();

            // Re-select the template
            SelectedTemplateSummary = TemplateLibrary.FirstOrDefault(t => t.Name == templateName);

            _logger.LogInfo($"Template '{templateName}' updated successfully", "TemplateManagementViewModel");

            TemplateUpdated?.Invoke(this, new TemplateEventArgs(template));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update template: {SelectedTemplateDetails?.Name}", ex, "TemplateManagementViewModel");
        }
    }

    private void ClearValidationResult()
    {
        BatchValidationResults.Clear();
        NotifyValidationPropertiesChanged();
    }

    private void NotifyValidationPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasValidationResult));
        OnPropertyChanged(nameof(HasValidationIssues));
        OnPropertyChanged(nameof(TotalFilesValidated));
        OnPropertyChanged(nameof(FilesWithErrors));
        OnPropertyChanged(nameof(FilesWithWarnings));
        OnPropertyChanged(nameof(FilesValid));
        OnPropertyChanged(nameof(ValidationStatusIcon));
        OnPropertyChanged(nameof(ValidationStatusText));
        OnPropertyChanged(nameof(ValidationResultStatus));
        OnPropertyChanged(nameof(ValidationResultBackground));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    private void NotifyCommandsCanExecuteChanged()
    {
        OnPropertyChanged(nameof(CanCreateTemplate));
        OnPropertyChanged(nameof(CanValidate));
        OnPropertyChanged(nameof(CanDeleteTemplate));
        OnPropertyChanged(nameof(CanImportTemplate));
        OnPropertyChanged(nameof(CanUpdateTemplate));
    }

    /// <summary>
    /// Apply semantic names from column linking to the template's columns.
    /// </summary>
    private void ApplySemanticNamesToTemplate(ExcelTemplate template, string fileName)
    {
        if (_semanticNameProvider == null) return;

        var semanticNames = _semanticNameProvider(fileName);
        if (semanticNames.Count == 0) return;

        // Update columns with semantic names
        for (int i = 0; i < template.Columns.Count; i++)
        {
            var column = template.Columns[i];
            if (semanticNames.TryGetValue(column.Name, out var semanticName))
            {
                // Replace with updated column (ExpectedColumn is a record)
                template.Columns[i] = column.WithSemanticName(semanticName);
                _logger.LogInfo($"Applied semantic name '{semanticName}' to column '{column.Name}'", "TemplateManagementViewModel");
            }
        }
    }

    // Events
    public event EventHandler<TemplateEventArgs>? TemplateCreated;
    public event EventHandler<TemplateEventArgs>? TemplateUpdated;
    public event EventHandler<TemplateEventArgs>? TemplateDeleted;
    public event EventHandler<TemplateEventArgs>? TemplateImported;
    public event EventHandler<ValidationCompletedEventArgs>? ValidationCompleted;
    public event EventHandler<SelectedTemplateChangedEventArgs>? SelectedTemplateChanged;

    public void Dispose()
    {
        if (_disposed) return;

        TemplateCreated = null;
        TemplateUpdated = null;
        TemplateDeleted = null;
        TemplateImported = null;
        ValidationCompleted = null;
        SelectedTemplateChanged = null;

        TemplateLibrary.Clear();
        BatchValidationResults.Clear();

        _selectedTemplateSummary = null;
        _selectedTemplateDetails = null;

        _disposed = true;
    }
}

/// <summary>
/// Represents validation result for a single file in batch validation.
/// </summary>
public class FileValidationResultViewModel : ViewModelBase
{
    public string FileName { get; }
    public ValidationReport? Report { get; }
    public string? ErrorMessage { get; }

    public ValidationStatus Status => Report?.Status ?? ValidationStatus.Failed;
    public int ErrorCount => Report?.TotalErrorCount ?? (ErrorMessage != null ? 1 : 0);
    public int WarningCount => Report?.TotalWarningCount ?? 0;
    public bool HasIssues => ErrorCount > 0 || WarningCount > 0;
    public bool IsValid => Status == ValidationStatus.Valid;
    public bool IsValidWithWarnings => Status == ValidationStatus.ValidWithWarnings;
    public bool IsInvalid => Status == ValidationStatus.Invalid || Status == ValidationStatus.Failed;

    public string StatusIcon => Status switch
    {
        ValidationStatus.Valid => "\u2705",
        ValidationStatus.ValidWithWarnings => "\u26A0",
        ValidationStatus.Invalid => "\u274C",
        ValidationStatus.Failed => "\u26D4",
        _ => ""
    };

    public string StatusText => Status switch
    {
        ValidationStatus.Valid => "Valid",
        ValidationStatus.ValidWithWarnings => $"{WarningCount} warning(s)",
        ValidationStatus.Invalid => $"{ErrorCount} error(s)",
        ValidationStatus.Failed => ErrorMessage ?? "Failed",
        _ => ""
    };

    public IBrush StatusBackground => Status switch
    {
        ValidationStatus.Valid => new SolidColorBrush(Color.Parse("#22C55E")),
        ValidationStatus.ValidWithWarnings => new SolidColorBrush(Color.Parse("#F59E0B")),
        ValidationStatus.Invalid => new SolidColorBrush(Color.Parse("#EF4444")),
        ValidationStatus.Failed => new SolidColorBrush(Color.Parse("#6B7280")),
        _ => new SolidColorBrush(Colors.Transparent)
    };

    public IReadOnlyList<ValidationIssueViewModel> Issues { get; }

    // For expanding/collapsing issues in UI
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public FileValidationResultViewModel(string fileName, ValidationReport report)
    {
        FileName = fileName;
        Report = report;
        Issues = report.AllIssues
            .OrderByDescending(i => i.Severity)
            .Select(i => new ValidationIssueViewModel(i))
            .ToList();
    }

    public FileValidationResultViewModel(string fileName, string errorMessage)
    {
        FileName = fileName;
        ErrorMessage = errorMessage;
        Issues = Array.Empty<ValidationIssueViewModel>();
    }
}

// Event args classes
public class TemplateEventArgs : EventArgs
{
    public string TemplateName { get; }
    public ExcelTemplate? Template { get; }

    public TemplateEventArgs(ExcelTemplate template)
    {
        Template = template;
        TemplateName = template.Name;
    }

    public TemplateEventArgs(string templateName)
    {
        TemplateName = templateName;
    }
}

public class ValidationCompletedEventArgs : EventArgs
{
    public ValidationReport Report { get; }

    public ValidationCompletedEventArgs(ValidationReport report)
    {
        Report = report;
    }
}

public class SelectedTemplateChangedEventArgs : EventArgs
{
    public ExcelTemplate? Template { get; }

    public SelectedTemplateChangedEventArgs(ExcelTemplate? template)
    {
        Template = template;
    }
}
