using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

        var themeManager = _host.Services.GetRequiredService<IThemeManager>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
            var searchViewModel = _host.Services.GetRequiredService<SearchViewModel>();
            var fileDetailsViewModel = _host.Services.GetRequiredService<FileDetailsViewModel>();
            var treeSearchResultsViewModel = _host.Services.GetRequiredService<TreeSearchResultsViewModel>();

            mainViewModel.SetSearchViewModel(searchViewModel);
            mainViewModel.SetFileDetailsViewModel(fileDetailsViewModel);
            mainViewModel.SetTreeSearchResultsViewModel(treeSearchResultsViewModel);

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
                services.AddSingleton<ISearchService, SearchService>();
                services.AddSingleton<IRowComparisonService, RowComparisonService>();
                services.AddSingleton<IExceptionHandler, ExceptionHandler>();
                services.AddSingleton<IFileLogService, FileLogService>();

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
                services.AddSingleton<IRowComparisonCoordinator, RowComparisonCoordinator>();
                services.AddSingleton<ITabNavigationCoordinator, TabNavigationCoordinator>();
                services.AddSingleton<IFileDetailsCoordinator, FileDetailsCoordinator>();

                // Register ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<SearchViewModel>();
                services.AddSingleton<FileDetailsViewModel>();
                services.AddSingleton<TreeSearchResultsViewModel>();

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

}
