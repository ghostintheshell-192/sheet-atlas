using FluentAssertions;
using Moq;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Managers;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IFilePickerService> _mockFilePickerService;
    private readonly Mock<IThemeManager> _mockThemeManager;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogService = new Mock<ILogService>();
        _mockFilePickerService = new Mock<IFilePickerService>();
        _mockThemeManager = new Mock<IThemeManager>();

        // Setup default settings
        var defaultSettings = UserSettings.CreateDefault();
        _mockSettingsService.Setup(s => s.Current).Returns(defaultSettings);

        _viewModel = new SettingsViewModel(
            _mockSettingsService.Object,
            _mockLogService.Object,
            _mockFilePickerService.Object,
            _mockThemeManager.Object);
    }

    /// <summary>
    /// Helper to execute async commands (ICommand.Execute returns void but internally runs async)
    /// </summary>
    private static async Task ExecuteCommandAsync(System.Windows.Input.ICommand command, object? parameter = null)
    {
        command.Execute(parameter);
        // Give async operations time to complete
        await Task.Delay(10);
    }

    #region Initialization Tests

    [Fact]
    public void Constructor_LoadsCurrentSettings()
    {
        // Assert - Should load defaults
        _viewModel.SelectedTheme.Should().Be(ThemePreference.System);
        _viewModel.SelectedHeaderRowCount.Should().Be(1);
        _viewModel.SelectedExportFormat.Should().Be(ExportFormat.Excel);
        _viewModel.SelectedNamingPattern.Should().Be(NamingPattern.DatePrefix);
        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void SelectedTheme_WhenChanged_MarksAsModified()
    {
        // Act
        _viewModel.SelectedTheme = ThemePreference.Dark;

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void SelectedHeaderRowCount_WhenChanged_MarksAsModified()
    {
        // Act
        _viewModel.SelectedHeaderRowCount = 3;

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void SelectedExportFormat_WhenChanged_MarksAsModified()
    {
        // Act
        _viewModel.SelectedExportFormat = ExportFormat.CSV;

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void SelectedNamingPattern_WhenChanged_MarksAsModified()
    {
        // Act
        _viewModel.SelectedNamingPattern = NamingPattern.DateSuffix;

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void OutputFolder_WhenChanged_MarksAsModified()
    {
        // Act
        _viewModel.OutputFolder = "/new/path";

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    #endregion

    #region Save Command Tests

    [Fact]
    public async Task SaveCommand_SavesSettingsToService()
    {
        // Arrange
        _viewModel.SelectedTheme = ThemePreference.Dark;
        _viewModel.SelectedHeaderRowCount = 2;
        _viewModel.SelectedExportFormat = ExportFormat.CSV;

        // Act
        await ExecuteCommandAsync(_viewModel.SaveCommand);

        // Assert
        _mockSettingsService.Verify(s => s.SaveAsync(
            It.Is<UserSettings>(settings =>
                settings.Appearance.Theme == ThemePreference.Dark &&
                settings.DataProcessing.DefaultHeaderRowCount == 2 &&
                settings.DataProcessing.DefaultExportFormat == ExportFormat.CSV),
            default), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_AppliesTheme_WhenThemeIsLight()
    {
        // Arrange
        _viewModel.SelectedTheme = ThemePreference.Light;

        // Act
        await ExecuteCommandAsync(_viewModel.SaveCommand);

        // Assert
        _mockThemeManager.Verify(t => t.SetTheme(Theme.Light), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_AppliesTheme_WhenThemeIsDark()
    {
        // Arrange
        _viewModel.SelectedTheme = ThemePreference.Dark;

        // Act
        await ExecuteCommandAsync(_viewModel.SaveCommand);

        // Assert
        _mockThemeManager.Verify(t => t.SetTheme(Theme.Dark), Times.Once);
    }

    [Fact]
    public async Task SaveCommand_ClearsUnsavedChanges()
    {
        // Arrange
        _viewModel.SelectedTheme = ThemePreference.Dark;
        _viewModel.HasUnsavedChanges.Should().BeTrue();

        // Act
        await ExecuteCommandAsync(_viewModel.SaveCommand);

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    #endregion

    #region Reset Command Tests

    [Fact]
    public async Task ResetCommand_ResetsToDefaults_RequiresSave()
    {
        // Arrange
        _viewModel.SelectedTheme = ThemePreference.Dark;
        _viewModel.SelectedHeaderRowCount = 5;

        var defaultSettings = UserSettings.CreateDefault();

        // Act
        await ExecuteCommandAsync(_viewModel.ResetCommand);

        // Assert - Reset loads defaults locally but does NOT save
        // User must click Save to persist the changes
        _viewModel.SelectedTheme.Should().Be(defaultSettings.Appearance.Theme);
        _viewModel.SelectedHeaderRowCount.Should().Be(defaultSettings.DataProcessing.DefaultHeaderRowCount);
        _viewModel.HasUnsavedChanges.Should().BeTrue(); // Requires Save to persist
    }

    #endregion

    #region Cancel Command Tests

    [Fact]
    public async Task CancelCommand_RestoresOriginalValues()
    {
        // Arrange - Start with defaults
        var originalTheme = _viewModel.SelectedTheme;
        var originalHeaderRowCount = _viewModel.SelectedHeaderRowCount;

        // Modify settings
        _viewModel.SelectedTheme = ThemePreference.Dark;
        _viewModel.SelectedHeaderRowCount = 5;
        _viewModel.HasUnsavedChanges.Should().BeTrue();

        // Act - Cancel should restore
        await ExecuteCommandAsync(_viewModel.CancelCommand);

        // Assert
        _viewModel.SelectedTheme.Should().Be(originalTheme);
        _viewModel.SelectedHeaderRowCount.Should().Be(originalHeaderRowCount);
        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    #endregion

    #region Browse Output Folder Tests

    [Fact]
    public async Task BrowseOutputFolderCommand_UpdatesOutputFolder_WhenUserSelectsFolder()
    {
        // Arrange
        var selectedPath = "/test/output/path";
        _mockFilePickerService
            .Setup(f => f.SelectFolderAsync(It.IsAny<string>()))
            .ReturnsAsync(selectedPath);

        // Act
        await ExecuteCommandAsync(_viewModel.BrowseOutputFolderCommand);

        // Assert
        _viewModel.OutputFolder.Should().Be(selectedPath);
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseOutputFolderCommand_DoesNotUpdateOutputFolder_WhenUserCancels()
    {
        // Arrange
        var originalPath = _viewModel.OutputFolder;
        _mockFilePickerService
            .Setup(f => f.SelectFolderAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        await ExecuteCommandAsync(_viewModel.BrowseOutputFolderCommand);

        // Assert
        _viewModel.OutputFolder.Should().Be(originalPath);
    }

    #endregion

    #region Available Options Tests

    [Fact]
    public void AvailableThemes_ContainsAllThemeOptions()
    {
        // Assert
        _viewModel.AvailableThemes.Should().Contain(ThemePreference.System);
        _viewModel.AvailableThemes.Should().Contain(ThemePreference.Light);
        _viewModel.AvailableThemes.Should().Contain(ThemePreference.Dark);
    }

    [Fact]
    public void AvailableHeaderRowCounts_Contains1To10()
    {
        // Assert
        _viewModel.AvailableHeaderRowCounts.Should().HaveCount(10);
        _viewModel.AvailableHeaderRowCounts.Should().ContainInOrder(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Fact]
    public void AvailableExportFormats_ContainsAllFormats()
    {
        // Assert
        _viewModel.AvailableExportFormats.Should().Contain(ExportFormat.Excel);
        _viewModel.AvailableExportFormats.Should().Contain(ExportFormat.CSV);
    }

    [Fact]
    public void AvailableNamingPatterns_ContainsAllPatterns()
    {
        // Assert
        _viewModel.AvailableNamingPatterns.Should().Contain(NamingPattern.DatePrefix);
        _viewModel.AvailableNamingPatterns.Should().Contain(NamingPattern.DateSuffix);
        _viewModel.AvailableNamingPatterns.Should().Contain(NamingPattern.DateTimePrefix);
    }

    #endregion
}
