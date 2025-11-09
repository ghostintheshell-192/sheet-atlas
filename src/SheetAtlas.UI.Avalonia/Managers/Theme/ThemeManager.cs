using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Managers
{
    public class ThemeManager : INotifyPropertyChanged, IThemeManager
    {
        private readonly ILogService _logger;
        private Theme _currentTheme = Theme.Light;

        public Theme CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTheme)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThemeButtonText)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThemeButtonTooltip)));
                    ThemeChanged?.Invoke(this, value);
                }
            }
        }

        public string ThemeButtonText => CurrentTheme == Theme.Light ? "🌙" : "☀️";
        public string ThemeButtonTooltip => CurrentTheme == Theme.Light
            ? "Switch to Dark Theme"
            : "Switch to Light Theme";


        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<Theme>? ThemeChanged;

        public ThemeManager(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize with system preference if available
            InitializeTheme();
        }

        public void SetTheme(Theme theme)
        {
            _logger.LogInfo($"Setting theme to {theme}", "ThemeManager");

            try
            {
                ApplyTheme(theme);
                CurrentTheme = theme;
                SaveThemePreference(theme);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to set theme to {theme}", ex, "ThemeManager");
                throw;
            }
        }

        public void ToggleTheme()
        {
            var newTheme = CurrentTheme == Theme.Light ? Theme.Dark : Theme.Light;
            SetTheme(newTheme);
        }

        private void InitializeTheme()
        {
            try
            {
                // Load saved preference or default to Light
                var savedTheme = LoadThemePreference();
                ApplyTheme(savedTheme);
                CurrentTheme = savedTheme;
            }
            catch (Exception)
            {
                _logger.LogWarning("Failed to initialize theme, using Light theme as fallback", "ThemeManager");
                ApplyTheme(Theme.Light);
                CurrentTheme = Theme.Light;
            }
        }

        private void ApplyTheme(Theme theme)
        {
            var application = Application.Current;
            if (application == null)
            {
                _logger.LogWarning("Application.Current is null, cannot apply theme", "ThemeManager");
                return;
            }

            try
            {
                // Set the global theme variant for Avalonia
                var themeVariant = theme == Theme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
                application.RequestedThemeVariant = themeVariant;

                var themeKey = theme == Theme.Light ? "LightTheme" : "DarkTheme";

                // Find the theme resources file
                var themeResourcesDict = application.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.TryGetResource(themeKey, null, out _));

                if (themeResourcesDict != null &&
                    themeResourcesDict.TryGetResource(themeKey, null, out var themeDict) &&
                    themeDict is ResourceDictionary themeDictionary)
                {
                    // Clear existing theme resources and apply new ones
                    var keysToRemove = application.Resources.Keys
                        .Where(key => IsThemeResource(key))
                        .ToList();

                    foreach (var key in keysToRemove)
                    {
                        application.Resources.Remove(key);
                    }

                    // Apply new theme resources
                    foreach (var kvp in themeDictionary)
                    {
                        application.Resources[kvp.Key] = kvp.Value;
                    }

                    _logger.LogInfo($"Applied {theme} theme variant and {themeDictionary.Count} resources", "ThemeManager");
                }
                else
                {
                    _logger.LogError($"Failed to find theme dictionary for {theme}", "ThemeManager");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying theme {theme}", ex, "ThemeManager");
            }
        }

        private static bool IsThemeResource(object key)
        {
            var keyString = key?.ToString();
            if (string.IsNullOrEmpty(keyString))
                return false;

            // Check if it's one of our theme resource keys
            return keyString.Contains("Primary") ||
                   keyString.Contains("Secondary") ||
                   keyString.Contains("Accent") ||
                   keyString.Contains("Background") ||
                   keyString.Contains("Text") ||
                   keyString.Contains("Gray") ||
                   keyString.Contains("Border") ||
                   keyString.Contains("Success") ||
                   keyString.Contains("Warning") ||
                   keyString.Contains("Error") ||
                   keyString.Contains("Info") ||
                   keyString.Contains("Hover") ||
                   keyString.Contains("Selected") ||
                   keyString.Contains("Active") ||
                   keyString.Contains("Focus") ||
                   keyString.Contains("Search") ||
                   keyString.Contains("File") ||
                   keyString.Contains("Sheet") ||
                   keyString.Contains("Highlight") ||
                   keyString.Contains("Comparison") ||
                   keyString.Contains("MenuFlyout") ||
                   keyString.Contains("Overlay") ||
                   keyString.Contains("System") ||
                   keyString.Contains("Button") ||
                   keyString.Contains("TextControl") ||
                   keyString.Contains("ListBox") ||
                   keyString.Contains("ScrollBar") ||
                   keyString.Contains("TreeView") ||
                   keyString.Contains("ComboBox") ||
                   keyString.Contains("ToolTip") ||
                   keyString.Contains("Chrome") ||
                   keyString.Contains("Region") ||
                   keyString.Contains("List") ||
                   keyString.Contains("Highlight") ||
                   keyString.Contains("Alt");
        }

        private static Theme LoadThemePreference()
        {
            // TODO: Load from user settings or config file
            // For now, default to Light theme
            return Theme.Light;
        }

        private void SaveThemePreference(Theme theme)
        {
            // TODO: Save to user settings or config file
            _logger.LogInfo($"Theme preference saved: {theme}", "ThemeManager");
        }
    }
}
