using FluentAssertions;
using Moq;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly SettingsService _settingsService;
    private readonly string _testSettingsPath;

    public SettingsServiceTests()
    {
        _mockLogService = new Mock<ILogService>();
        _settingsService = new SettingsService(_mockLogService.Object);
        _testSettingsPath = _settingsService.GetSettingsFilePath();
    }

    public void Dispose()
    {
        // Cleanup: Delete test settings file after each test
        if (File.Exists(_testSettingsPath))
        {
            File.Delete(_testSettingsPath);
        }
    }

    #region Load Tests

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        // Arrange - Ensure file doesn't exist
        if (File.Exists(_testSettingsPath))
        {
            File.Delete(_testSettingsPath);
        }

        // Act
        var settings = await _settingsService.LoadAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Appearance.Theme.Should().Be(ThemePreference.System);
        settings.DataProcessing.DefaultHeaderRowCount.Should().Be(1);
        settings.DataProcessing.DefaultExportFormat.Should().Be(ExportFormat.Excel);
        settings.DataProcessing.NormalizedFileNaming.Should().Be(NamingPattern.DatePrefix);
        settings.FileLocations.OutputFolder.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenFileExists_LoadsFromFile()
    {
        // Arrange - Save custom settings first
        var customSettings = new UserSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemePreference.Dark },
            DataProcessing = new DataProcessingSettings
            {
                DefaultHeaderRowCount = 3,
                DefaultExportFormat = ExportFormat.CSV,
                NormalizedFileNaming = NamingPattern.DateSuffix
            },
            FileLocations = new FileLocationSettings { OutputFolder = "/custom/path" }
        };
        await _settingsService.SaveAsync(customSettings);

        // Act - Load settings
        var loadedSettings = await _settingsService.LoadAsync();

        // Assert
        loadedSettings.Appearance.Theme.Should().Be(ThemePreference.Dark);
        loadedSettings.DataProcessing.DefaultHeaderRowCount.Should().Be(3);
        loadedSettings.DataProcessing.DefaultExportFormat.Should().Be(ExportFormat.CSV);
        loadedSettings.DataProcessing.NormalizedFileNaming.Should().Be(NamingPattern.DateSuffix);
        loadedSettings.FileLocations.OutputFolder.Should().Be("/custom/path");
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupted_ReturnsDefaults()
    {
        // Arrange - Write invalid JSON
        var directory = Path.GetDirectoryName(_testSettingsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(_testSettingsPath, "{ invalid json }");

        // Act
        var settings = await _settingsService.LoadAsync();

        // Assert - Should fall back to defaults
        settings.Should().NotBeNull();
        settings.Appearance.Theme.Should().Be(ThemePreference.System);
    }

    #endregion

    #region Save Tests

    [Fact]
    public async Task SaveAsync_CreatesFile_WithCorrectContent()
    {
        // Arrange
        var settings = new UserSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemePreference.Light },
            DataProcessing = new DataProcessingSettings
            {
                DefaultHeaderRowCount = 2,
                DefaultExportFormat = ExportFormat.Excel,
                NormalizedFileNaming = NamingPattern.DateTimePrefix
            },
            FileLocations = new FileLocationSettings { OutputFolder = "/test/output" }
        };

        // Act
        await _settingsService.SaveAsync(settings);

        // Assert - File should exist
        File.Exists(_testSettingsPath).Should().BeTrue();

        // Assert - Content should be readable JSON (camelCase)
        var json = await File.ReadAllTextAsync(_testSettingsPath);
        json.Should().Contain("\"theme\": \"Light\"");
        json.Should().Contain("\"defaultHeaderRowCount\": 2");
        json.Should().Contain("\"defaultExportFormat\": \"Excel\"");
    }

    [Fact]
    public async Task SaveAsync_UpdatesCurrent_Property()
    {
        // Arrange
        var settings = new UserSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemePreference.Dark }
        };

        // Act
        await _settingsService.SaveAsync(settings);

        // Assert
        _settingsService.Current.Appearance.Theme.Should().Be(ThemePreference.Dark);
    }

    [Fact]
    public async Task SaveAsync_ValidatesSettings_AndFixesInvalidValues()
    {
        // Arrange - Invalid settings
        var invalidSettings = new UserSettings
        {
            DataProcessing = new DataProcessingSettings
            {
                DefaultHeaderRowCount = 0, // Invalid: must be 1-10
            },
            FileLocations = new FileLocationSettings
            {
                OutputFolder = "" // Invalid: cannot be empty
            }
        };

        // Act
        await _settingsService.SaveAsync(invalidSettings);

        // Assert - Should be fixed
        _settingsService.Current.DataProcessing.DefaultHeaderRowCount.Should().Be(1); // Fixed to min
        _settingsService.Current.FileLocations.OutputFolder.Should().NotBeEmpty(); // Fixed to default
    }

    #endregion

    #region Reset Tests

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresDefaults()
    {
        // Arrange - Save custom settings
        var customSettings = new UserSettings
        {
            Appearance = new AppearanceSettings { Theme = ThemePreference.Dark },
            DataProcessing = new DataProcessingSettings { DefaultHeaderRowCount = 5 }
        };
        await _settingsService.SaveAsync(customSettings);
        _settingsService.Current.Appearance.Theme.Should().Be(ThemePreference.Dark);

        // Act
        await _settingsService.ResetToDefaultsAsync();

        // Assert - Current should be defaults
        _settingsService.Current.Appearance.Theme.Should().Be(ThemePreference.System);
        _settingsService.Current.DataProcessing.DefaultHeaderRowCount.Should().Be(1);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(0, 1)]  // Below min → fixed to 1
    [InlineData(1, 1)]  // Min valid
    [InlineData(5, 5)]  // Mid valid
    [InlineData(10, 10)] // Max valid
    [InlineData(11, 1)] // Above max → reset to default (1)
    [InlineData(100, 1)] // Way above max → reset to default (1)
    public async Task SaveAsync_ValidatesHeaderRowCount(int inputCount, int expectedCount)
    {
        // Arrange
        var settings = new UserSettings
        {
            DataProcessing = new DataProcessingSettings
            {
                DefaultHeaderRowCount = inputCount
            }
        };

        // Act
        await _settingsService.SaveAsync(settings);

        // Assert
        _settingsService.Current.DataProcessing.DefaultHeaderRowCount.Should().Be(expectedCount);
    }

    [Fact]
    public async Task SaveAsync_ValidatesOutputFolder_FixesEmptyPath()
    {
        // Arrange
        var settings = new UserSettings
        {
            FileLocations = new FileLocationSettings
            {
                OutputFolder = "" // Invalid
            }
        };

        // Act
        await _settingsService.SaveAsync(settings);

        // Assert - Should be fixed to default path
        _settingsService.Current.FileLocations.OutputFolder.Should().NotBeEmpty();
        _settingsService.Current.FileLocations.OutputFolder.Should().Contain("SheetAtlas");
    }

    [Fact]
    public async Task SaveAsync_NormalizesRelativePaths_ToAbsolutePaths()
    {
        // Arrange - Relative path
        var settings = new UserSettings
        {
            FileLocations = new FileLocationSettings
            {
                OutputFolder = "./relative/path"
            }
        };

        // Act
        await _settingsService.SaveAsync(settings);

        // Assert - Should be converted to absolute path
        _settingsService.Current.FileLocations.OutputFolder.Should().NotStartWith("./");
        Path.IsPathRooted(_settingsService.Current.FileLocations.OutputFolder).Should().BeTrue();
    }

    #endregion

    #region GetSettingsFilePath Tests

    [Fact]
    public void GetSettingsFilePath_ReturnsValidPath()
    {
        // Act
        var path = _settingsService.GetSettingsFilePath();

        // Assert
        path.Should().NotBeNullOrEmpty();
        path.Should().EndWith("settings.json");
        path.Should().Contain("SheetAtlas");
    }

    #endregion
}
