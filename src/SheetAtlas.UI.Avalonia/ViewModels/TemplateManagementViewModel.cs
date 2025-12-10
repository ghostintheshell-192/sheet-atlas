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
    private ValidationReport? _validationReport;
    private bool _isLoadingTemplates;
    private bool _isValidating;
    private bool _disposed;

    // Selected files from main window (can be single or multiple)
    private IReadOnlyList<IFileLoadResultViewModel> _selectedFiles = Array.Empty<IFileLoadResultViewModel>();

    public ObservableCollection<TemplateSummary> TemplateLibrary { get; } = new();
    public ObservableCollection<ValidationIssueViewModel> ValidationIssues { get; } = new();

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

    // Validation state
    public bool HasValidationResult => _validationReport != null;
    public bool HasValidationIssues => _validationReport?.AllIssues.Count > 0;

    public string ValidationStatusIcon => _validationReport?.Status switch
    {
        ValidationStatus.Valid => "\u2705",
        ValidationStatus.ValidWithWarnings => "\u26A0",
        ValidationStatus.Invalid => "\u274C",
        ValidationStatus.Failed => "\u26D4",
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
        ValidationStatus.Valid => new SolidColorBrush(Color.Parse("#22C55E")),
        ValidationStatus.ValidWithWarnings => new SolidColorBrush(Color.Parse("#F59E0B")),
        ValidationStatus.Invalid => new SolidColorBrush(Color.Parse("#EF4444")),
        ValidationStatus.Failed => new SolidColorBrush(Color.Parse("#6B7280")),
        _ => new SolidColorBrush(Colors.Transparent)
    };

    public string ValidationSummary => _validationReport?.Summary ?? "";

    // Can execute conditions
    public bool CanCreateTemplate => HasSingleFileSelected && _selectedFiles[0].File != null && !IsValidating;
    public bool CanValidate => HasSelectedTemplate && HasSelectedFiles && !IsValidating;
    public bool CanDeleteTemplate => HasSelectedTemplate && !IsValidating;
    public bool CanImportTemplate => !IsValidating;

    // Commands
    public ICommand CreateTemplateCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand ImportTemplateCommand { get; }
    public ICommand RefreshLibraryCommand { get; }

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
            // For now, validate single file. Multi-file batch validation can be added later.
            var file = _selectedFiles[0];
            if (file.File == null) return;

            _logger.LogInfo($"Validating file '{file.FileName}' against template '{SelectedTemplateDetails.Name}'", "TemplateManagementViewModel");

            _validationReport = await _templateValidationService.ValidateAsync(
                file.File,
                SelectedTemplateDetails);

            // Populate issues for display
            foreach (var issue in _validationReport.AllIssues.OrderByDescending(i => i.Severity))
            {
                ValidationIssues.Add(new ValidationIssueViewModel(issue));
            }

            NotifyValidationPropertiesChanged();

            _logger.LogInfo($"Validation completed: {_validationReport.Status} ({_validationReport.TotalErrorCount} errors, {_validationReport.TotalWarningCount} warnings)", "TemplateManagementViewModel");

            ValidationCompleted?.Invoke(this, new ValidationCompletedEventArgs(_validationReport));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Validation failed", ex, "TemplateManagementViewModel");
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
    }

    private void NotifyCommandsCanExecuteChanged()
    {
        OnPropertyChanged(nameof(CanCreateTemplate));
        OnPropertyChanged(nameof(CanValidate));
        OnPropertyChanged(nameof(CanDeleteTemplate));
        OnPropertyChanged(nameof(CanImportTemplate));
    }

    // Events
    public event EventHandler<TemplateEventArgs>? TemplateCreated;
    public event EventHandler<TemplateEventArgs>? TemplateDeleted;
    public event EventHandler<TemplateEventArgs>? TemplateImported;
    public event EventHandler<ValidationCompletedEventArgs>? ValidationCompleted;

    public void Dispose()
    {
        if (_disposed) return;

        TemplateCreated = null;
        TemplateDeleted = null;
        TemplateImported = null;
        ValidationCompleted = null;

        TemplateLibrary.Clear();
        ValidationIssues.Clear();

        _selectedTemplateSummary = null;
        _selectedTemplateDetails = null;
        _validationReport = null;

        _disposed = true;
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
