using System.Text.Json;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Json;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Manages user preferences with persistent JSON storage.
    /// Settings are stored in %AppData%/SheetAtlas/settings.json.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ILogService _logService;
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;
        private UserSettings _currentSettings;

        public SettingsService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Setup settings directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _settingsDirectory = Path.Combine(appDataPath, "SheetAtlas");
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

            // Initialize with defaults
            _currentSettings = UserSettings.CreateDefault();

            // Ensure settings directory exists
            try
            {
                Directory.CreateDirectory(_settingsDirectory);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to create settings directory: {_settingsDirectory}", ex.Message);
            }
        }

        public UserSettings Current => _currentSettings;

        public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logService.LogInfo($"Settings file not found, using defaults: {_settingsFilePath}", "SettingsService");
                    _currentSettings = UserSettings.CreateDefault();
                    return _currentSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserSettings);

                if (settings == null)
                {
                    _logService.LogWarning("Failed to deserialize settings, using defaults", "SettingsService");
                    _currentSettings = UserSettings.CreateDefault();
                    return _currentSettings;
                }

                // Validate and fix settings if needed
                settings = ValidateAndFixSettings(settings);

                _currentSettings = settings;
                _logService.LogInfo($"Settings loaded successfully from: {_settingsFilePath}", "SettingsService");
                return _currentSettings;
            }
            catch (JsonException ex)
            {
                _logService.LogError($"Invalid JSON in settings file, using defaults: {_settingsFilePath}", ex, "SettingsService");
                _currentSettings = UserSettings.CreateDefault();
                return _currentSettings;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to load settings, using defaults: {_settingsFilePath}", ex, "SettingsService");
                _currentSettings = UserSettings.CreateDefault();
                return _currentSettings;
            }
        }

        public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_settingsDirectory);

                // Validate settings before saving
                settings = ValidateAndFixSettings(settings);

                // Serialize to JSON
                var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.UserSettings);

                // Write to file
                await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);

                // Update current settings
                _currentSettings = settings;

                _logService.LogInfo($"Settings saved successfully to: {_settingsFilePath}", "SettingsService");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to save settings to: {_settingsFilePath}", ex, "SettingsService");
                throw;
            }
        }

        public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
        {
            _logService.LogInfo("Resetting settings to defaults", "SettingsService");
            var defaults = UserSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken);
        }

        public string GetSettingsFilePath() => _settingsFilePath;

        /// <summary>
        /// Validates and fixes invalid settings values.
        /// Ensures all settings have valid values even if file is corrupted.
        /// </summary>
        private UserSettings ValidateAndFixSettings(UserSettings settings)
        {
            // Validate header row count (must be 1-10)
            var headerRowCount = settings.DataProcessing.DefaultHeaderRowCount;
            if (headerRowCount < 1 || headerRowCount > 10)
            {
                _logService.LogWarning($"Invalid header row count {headerRowCount}, resetting to 1", "SettingsService");
                settings = settings with
                {
                    DataProcessing = settings.DataProcessing with { DefaultHeaderRowCount = 1 }
                };
            }

            // Validate output folder (must not be empty)
            if (string.IsNullOrWhiteSpace(settings.FileLocations.OutputFolder))
            {
                _logService.LogWarning("Empty output folder, resetting to default", "SettingsService");
                var defaultSettings = UserSettings.CreateDefault();
                settings = settings with
                {
                    FileLocations = settings.FileLocations with
                    {
                        OutputFolder = defaultSettings.FileLocations.OutputFolder
                    }
                };
            }

            // Ensure output folder path is valid
            try
            {
                var fullPath = Path.GetFullPath(settings.FileLocations.OutputFolder);
                if (fullPath != settings.FileLocations.OutputFolder)
                {
                    settings = settings with
                    {
                        FileLocations = settings.FileLocations with { OutputFolder = fullPath }
                    };
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Invalid output folder path, resetting to default: {settings.FileLocations.OutputFolder}",
                    ex.Message);
                var defaultSettings = UserSettings.CreateDefault();
                settings = settings with
                {
                    FileLocations = settings.FileLocations with
                    {
                        OutputFolder = defaultSettings.FileLocations.OutputFolder
                    }
                };
            }

            return settings;
        }
    }
}
