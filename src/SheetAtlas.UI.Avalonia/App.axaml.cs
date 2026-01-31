using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using SheetAtlas.UI.Avalonia.Views;
using SheetAtlas.UI.Avalonia.ViewModels;
using SheetAtlas.UI.Avalonia.Services;
using SheetAtlas.UI.Avalonia.Managers.Search;
using SheetAtlas.UI.Avalonia.Managers.Selection;
using SheetAtlas.UI.Avalonia.Managers.Files;
using SheetAtlas.UI.Avalonia.Managers.Comparison;
using SheetAtlas.UI.Avalonia.Managers.Navigation;
using SheetAtlas.UI.Avalonia.Managers.FileDetails;
using SheetAtlas.UI.Avalonia.Models.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Infrastructure.External;
using SheetAtlas.Infrastructure.External.Readers;
using SheetAtlas.Infrastructure.External.Writers;
using SheetAtlas.UI.Avalonia.Managers;
using SheetAtlas.Logging.Services;
using SheetAtlas.Core.Configuration;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;

namespace SheetAtlas.UI.Avalonia;

public partial class App : Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = CreateHostBuilder().Build();

        _ = _host.Services.GetRequiredService<ILogService>();

        // Load user settings synchronously at startup (use Task.Run to avoid deadlock)
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        Task.Run(() => settingsService.LoadAsync()).GetAwaiter().GetResult();

        // Apply theme from loaded settings
        var themeManager = _host.Services.GetRequiredService<IThemeManager>();
        var themePreference = settingsService.Current.Appearance.Theme;

        if (themePreference == Core.Application.DTOs.ThemePreference.System)
        {
            // For System theme, delay detection until platform is fully initialized
            Dispatcher.UIThread.Post(() =>
            {
                var detectedTheme = DetectSystemTheme();
                themeManager.SetTheme(detectedTheme);
            }, DispatcherPriority.Loaded);
        }
        else
        {
            // For explicit Light/Dark, apply immediately
            var theme = themePreference == Core.Application.DTOs.ThemePreference.Dark
                ? Theme.Dark
                : Theme.Light;
            themeManager.SetTheme(theme);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
            var searchViewModel = _host.Services.GetRequiredService<SearchViewModel>();
            var fileDetailsViewModel = _host.Services.GetRequiredService<FileDetailsViewModel>();
            var treeSearchResultsViewModel = _host.Services.GetRequiredService<TreeSearchResultsViewModel>();
            var templateManagementViewModel = _host.Services.GetRequiredService<TemplateManagementViewModel>();
            var columnLinkingViewModel = _host.Services.GetRequiredService<ColumnLinkingViewModel>();
            var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();

            mainViewModel.SetSearchViewModel(searchViewModel);
            mainViewModel.SetFileDetailsViewModel(fileDetailsViewModel);
            mainViewModel.SetTreeSearchResultsViewModel(treeSearchResultsViewModel);
            mainViewModel.SetTemplateManagementViewModel(templateManagementViewModel);
            mainViewModel.SetColumnLinkingViewModel(columnLinkingViewModel);
            mainViewModel.SetSettingsViewModel(settingsViewModel);

            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) => _host?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register configuration
                services.Configure<AppSettings>(context.Configuration.GetSection("AppSettings"));

                // Register Core services
                services.AddSingleton<ICellReferenceParser, CellReferenceParser>();
                services.AddSingleton<ICellValueReader, CellValueReader>();

                // Register MergedRangeExtractor (generic interface for OpenXML)
                services.AddSingleton<IMergedRangeExtractor<WorksheetPart>, OpenXmlMergedRangeExtractor>();

                // Register Foundation Layer services
                services.AddSingleton<ICurrencyDetector, SheetAtlas.Core.Application.Services.Foundation.CurrencyDetector>();
                services.AddSingleton<IDataNormalizationService, SheetAtlas.Core.Application.Services.Foundation.DataNormalizationService>();
                services.AddSingleton<IColumnAnalysisService, SheetAtlas.Core.Application.Services.Foundation.ColumnAnalysisService>();
                services.AddSingleton<IMergedCellResolver, SheetAtlas.Core.Application.Services.Foundation.MergedCellResolver>();
                services.AddSingleton<ITemplateValidationService, SheetAtlas.Core.Application.Services.Foundation.TemplateValidationService>();
                services.AddSingleton<ITemplateRepository, SheetAtlas.Core.Application.Services.Foundation.TemplateRepository>();
                services.AddSingleton<IHeaderGroupingService, SheetAtlas.Core.Application.Services.HeaderGroupingService>();
                services.AddSingleton<SheetAtlas.Infrastructure.External.Readers.INumberFormatInferenceService, SheetAtlas.Infrastructure.External.Readers.NumberFormatInferenceService>();

                services.AddSingleton<ISheetAnalysisOrchestrator>(sp =>
                {
                    var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;

                    var strategyStr = settings.FoundationLayer.MergedCells.DefaultStrategy;
                    var strategy = Enum.TryParse<SheetAtlas.Core.Domain.ValueObjects.MergeStrategy>(strategyStr, out var parsed)
                        ? parsed
                        : SheetAtlas.Core.Domain.ValueObjects.MergeStrategy.ExpandValue;

                    double warnThreshold = settings.FoundationLayer.MergedCells.WarnThresholdPercentage / 100.0;

                    return new SheetAnalysisOrchestrator(
                        sp.GetRequiredService<IMergedCellResolver>(),
                        sp.GetRequiredService<IColumnAnalysisService>(),
                        sp.GetRequiredService<IDataNormalizationService>(),
                        sp.GetRequiredService<ILogService>(),
                        strategy,
                        warnThreshold);
                });

                // Register file format readers (must be before ExcelReaderService)
                services.AddSingleton<IFileFormatReader, OpenXmlFileReader>();
                services.AddSingleton<IFileFormatReader, XlsFileReader>();
                services.AddSingleton<IFileFormatReader, CsvFileReader>();

                services.AddSingleton<IExcelReaderService, ExcelReaderService>();
                services.AddSingleton<IExcelWriterService, ExcelWriterService>();
                services.AddSingleton<IComparisonExportService, ComparisonExportService>();
                services.AddSingleton<ISearchService, SearchService>();
                services.AddSingleton<IRowComparisonService, RowComparisonService>();
                services.AddSingleton<IColumnLinkingService, ColumnLinkingService>();
                services.AddSingleton<IExceptionHandler, ExceptionHandler>();
                services.AddSingleton<IFileLogService, FileLogService>();
                services.AddSingleton<ISettingsService, SettingsService>();

                // Register Avalonia-specific services
                services.AddSingleton<IDialogService, AvaloniaDialogService>();
                services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
                services.AddSingleton<IErrorNotificationService, ErrorNotificationService>();
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<IActivityLogService, ActivityLogService>();


                // Register Managers and Factories
                services.AddSingleton<ISearchResultFactory, SheetAtlas.UI.Avalonia.Models.Search.SearchResultFactory>();
                services.AddSingleton<ISearchResultsManager, SheetAtlas.UI.Avalonia.Managers.Search.SearchResultsManager>();
                services.AddSingleton<ISelectionManager, SheetAtlas.UI.Avalonia.Managers.Selection.SelectionManager>();
                services.AddSingleton<IThemeManager, ThemeManager>();
                services.AddSingleton<ILoadedFilesManager, LoadedFilesManager>();
                services.AddSingleton<IRowComparisonCoordinator>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogService>();
                    var themeManager = sp.GetRequiredService<IThemeManager>();
                    var exportService = sp.GetRequiredService<IComparisonExportService>();
                    var filePickerService = sp.GetRequiredService<IFilePickerService>();
                    var settingsService = sp.GetRequiredService<ISettingsService>();
                    var headerGroupingService = sp.GetRequiredService<IHeaderGroupingService>();
                    var columnLinkingViewModel = sp.GetRequiredService<ColumnLinkingViewModel>();
                    return new RowComparisonCoordinator(
                        logger, logger, headerGroupingService, themeManager,
                        exportService, filePickerService, settingsService,
                        columnLinkingViewModel);
                });
                services.AddSingleton<ITabNavigationCoordinator, TabNavigationCoordinator>();
                services.AddSingleton<IFileDetailsCoordinator, FileDetailsCoordinator>();

                // Register ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<SearchViewModel>();
                services.AddSingleton<FileDetailsViewModel>();
                services.AddSingleton<TreeSearchResultsViewModel>();
                services.AddSingleton<TemplateManagementViewModel>();
                services.AddSingleton<ColumnLinkingViewModel>(sp =>
                {
                    var columnLinkingService = sp.GetRequiredService<IColumnLinkingService>();
                    var filesManager = sp.GetRequiredService<ILoadedFilesManager>();
                    return new ColumnLinkingViewModel(
                        columnLinkingService,
                        () => filesManager.LoadedFiles
                            .Where(f => f.File != null)
                            .Select(f => f.File!),
                        filesManager);
                });
                services.AddSingleton<SettingsViewModel>();

                // Register Views
                services.AddSingleton<MainWindow>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
    }

    /// <summary>
    /// Detects the system theme preference using PlatformSettings.
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
                if (colorValues.ThemeVariant == PlatformThemeVariant.Dark)
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
}
