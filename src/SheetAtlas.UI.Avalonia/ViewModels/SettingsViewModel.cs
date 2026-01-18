using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Commands;
using SheetAtlas.UI.Avalonia.Managers;
using SheetAtlas.UI.Avalonia.Services;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Settings tab.
/// Manages user preferences with Save/Reset/Cancel operations.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IFilePickerService _filePickerService;
    private readonly IThemeManager _themeManager;

    // Working copy of settings (edited by user, not saved until Save clicked)
    private UserSettings _workingSettings;

    // Selected values for dropdowns
    private ThemePreference _selectedTheme;
    private int _selectedHeaderRowCount;
    private ExportFormat _selectedExportFormat;
    private NamingPattern _selectedNamingPattern;
    private string _outputFolder = string.Empty;

    // UI state
    private bool _hasUnsavedChanges;

    public SettingsViewModel(
        ISettingsService settingsService,
        ILogService logService,
        IFilePickerService filePickerService,
        IThemeManager themeManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        // Load current settings
        _workingSettings = _settingsService.Current;
        LoadSettingsIntoProperties();

        // Initialize commands
        SaveCommand = new RelayCommand(SaveAsync, CanSave, _logService);
        ResetCommand = new RelayCommand(ResetAsync, () => true, _logService);
        CancelCommand = new RelayCommand(CancelAsync, () => true, _logService);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolderAsync, () => true, _logService);
    }

    #region Properties

    /// <summary>
    /// Available theme options for dropdown
    /// </summary>
    public IEnumerable<ThemePreference> AvailableThemes =>
        Enum.GetValues<ThemePreference>();

    /// <summary>
    /// Available header row count options (1-10)
    /// </summary>
    public IEnumerable<int> AvailableHeaderRowCounts =>
        Enumerable.Range(1, 10);

    /// <summary>
    /// Available export format options
    /// </summary>
    public IEnumerable<ExportFormat> AvailableExportFormats =>
        Enum.GetValues<ExportFormat>();

    /// <summary>
    /// Available naming pattern options
    /// </summary>
    public IEnumerable<NamingPattern> AvailableNamingPatterns =>
        Enum.GetValues<NamingPattern>();

    public ThemePreference SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetField(ref _selectedTheme, value))
            {
                MarkAsModified();
            }
        }
    }

    public int SelectedHeaderRowCount
    {
        get => _selectedHeaderRowCount;
        set
        {
            if (SetField(ref _selectedHeaderRowCount, value))
            {
                MarkAsModified();
            }
        }
    }

    public ExportFormat SelectedExportFormat
    {
        get => _selectedExportFormat;
        set
        {
            if (SetField(ref _selectedExportFormat, value))
            {
                MarkAsModified();
            }
        }
    }

    public NamingPattern SelectedNamingPattern
    {
        get => _selectedNamingPattern;
        set
        {
            if (SetField(ref _selectedNamingPattern, value))
            {
                MarkAsModified();
            }
        }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (SetField(ref _outputFolder, value))
            {
                MarkAsModified();
            }
        }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => SetField(ref _hasUnsavedChanges, value);
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }

    #endregion

    #region Command Methods

    private bool CanSave() => HasUnsavedChanges;

    private async Task SaveAsync()
    {
        try
        {
            // Build new settings from UI values
            var newSettings = new UserSettings
            {
                Appearance = new AppearanceSettings
                {
                    Theme = SelectedTheme
                },
                DataProcessing = new DataProcessingSettings
                {
                    DefaultHeaderRowCount = SelectedHeaderRowCount,
                    DefaultExportFormat = SelectedExportFormat,
                    NormalizedFileNaming = SelectedNamingPattern
                },
                FileLocations = new FileLocationSettings
                {
                    OutputFolder = OutputFolder
                }
            };

            // Save to disk
            await _settingsService.SaveAsync(newSettings);

            // Apply theme immediately
            ApplyThemeFromPreference(SelectedTheme);

            // Update working copy
            _workingSettings = newSettings;
            HasUnsavedChanges = false;

            _logService.LogInfo("Settings saved successfully", "SettingsViewModel");

            // Notify command states
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to save settings", ex, "SettingsViewModel");
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            await _settingsService.ResetToDefaultsAsync();

            // Reload defaults into UI
            _workingSettings = _settingsService.Current;
            LoadSettingsIntoProperties();
            HasUnsavedChanges = false;

            _logService.LogInfo("Settings reset to defaults", "SettingsViewModel");

            // Notify command states
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to reset settings", ex, "SettingsViewModel");
        }
    }

    private Task CancelAsync()
    {
        // Discard changes and reload from service
        _workingSettings = _settingsService.Current;
        LoadSettingsIntoProperties();
        HasUnsavedChanges = false;

        _logService.LogInfo("Settings changes cancelled", "SettingsViewModel");

        // Notify command states
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();

        return Task.CompletedTask;
    }

    private async Task BrowseOutputFolderAsync()
    {
        try
        {
            var result = await _filePickerService.SelectFolderAsync("Select Output Folder");

            if (result != null)
            {
                OutputFolder = result;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to browse output folder", ex, "SettingsViewModel");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Loads current settings from service into UI properties
    /// </summary>
    private void LoadSettingsIntoProperties()
    {
        _selectedTheme = _workingSettings.Appearance.Theme;
        _selectedHeaderRowCount = _workingSettings.DataProcessing.DefaultHeaderRowCount;
        _selectedExportFormat = _workingSettings.DataProcessing.DefaultExportFormat;
        _selectedNamingPattern = _workingSettings.DataProcessing.NormalizedFileNaming;
        _outputFolder = _workingSettings.FileLocations.OutputFolder;

        // Notify all properties changed
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedHeaderRowCount));
        OnPropertyChanged(nameof(SelectedExportFormat));
        OnPropertyChanged(nameof(SelectedNamingPattern));
        OnPropertyChanged(nameof(OutputFolder));
    }

    /// <summary>
    /// Marks settings as modified (unsaved changes exist)
    /// </summary>
    private void MarkAsModified()
    {
        HasUnsavedChanges = true;
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Applies theme based on user preference (Light/Dark/System)
    /// </summary>
    private void ApplyThemeFromPreference(ThemePreference preference)
    {
        Theme theme = preference switch
        {
            ThemePreference.Light => Theme.Light,
            ThemePreference.Dark => Theme.Dark,
            ThemePreference.System => DetectSystemTheme(),
            _ => Theme.Light
        };

        _themeManager.SetTheme(theme);
        _logService.LogInfo($"Applied theme: {preference} → {theme}", "SettingsViewModel");
    }

    /// <summary>
    /// Detects the current system theme preference using PlatformSettings.
    /// Falls back to Light if detection fails.
    /// </summary>
    private static Theme DetectSystemTheme()
    {
        try
        {
            // Try PlatformSettings first (more reliable)
            var platformSettings = Application.Current?.PlatformSettings;
            if (platformSettings != null)
            {
                var colorValues = platformSettings.GetColorValues();
                if (colorValues.ThemeVariant == global::Avalonia.Platform.PlatformThemeVariant.Dark)
                    return Theme.Dark;
            }

            // Fallback to ActualThemeVariant
            if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
                return Theme.Dark;
        }
        catch
        {
            // Ignore detection errors
        }

        return Theme.Light;
    }

    #endregion
}
