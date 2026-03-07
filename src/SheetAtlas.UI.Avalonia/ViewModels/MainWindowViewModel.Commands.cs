using System.Windows.Input;
using SheetAtlas.UI.Avalonia.Commands;

namespace SheetAtlas.UI.Avalonia.ViewModels
{
    public partial class MainWindowViewModel
    {
        public ICommand LoadFileCommand { get; private set; } = null!;
        public ICommand UnloadAllFilesCommand { get; private set; } = null!;
        public ICommand ToggleSidebarCommand { get; private set; } = null!;
        public ICommand ToggleQuickBarCommand { get; private set; } = null!;
        public ICommand ToggleStatusBarCommand { get; private set; } = null!;
        public ICommand ShowFileDetailsTabCommand { get; private set; } = null!;
        public ICommand ShowSearchTabCommand { get; private set; } = null!;
        public ICommand ShowComparisonTabCommand { get; private set; } = null!;
        public ICommand CloseFileDetailsTabCommand { get; private set; } = null!;
        public ICommand CloseSearchTabCommand { get; private set; } = null!;
        public ICommand CloseComparisonTabCommand { get; private set; } = null!;
        public ICommand ShowTemplatesTabCommand { get; private set; } = null!;
        public ICommand CloseTemplatesTabCommand { get; private set; } = null!;
        public ICommand ShowSettingsTabCommand { get; private set; } = null!;
        public ICommand CloseSettingsTabCommand { get; private set; } = null!;
        public ICommand ShowDataRegionsTabCommand { get; private set; } = null!;
        public ICommand CloseDataRegionsTabCommand { get; private set; } = null!;
        public ICommand ShowWelcomeTabCommand { get; private set; } = null!;
        public ICommand CloseWelcomeTabCommand { get; private set; } = null!;
        public ICommand ShowSearchResultsCommand { get; private set; } = null!;
        public ICommand NormalizeExportCommand { get; private set; } = null!;
        public ICommand ShowAboutCommand { get; private set; } = null!;
        public ICommand ShowDocumentationCommand { get; private set; } = null!;
        public ICommand ViewErrorLogCommand { get; private set; } = null!;

        // Delegated commands from SearchViewModel
        public ICommand ShowAllFilesCommand => SearchViewModel?.ShowAllFilesCommand ?? new RelayCommand(() => Task.CompletedTask);

        public void InitializeCommands()
        {
            LoadFileCommand = new RelayCommand(async () => await LoadFileAsync());

            UnloadAllFilesCommand = new RelayCommand(async () => await UnloadAllFilesAsync());

            ToggleSidebarCommand = new RelayCommand(() =>
            {
                IsSidebarExpanded = !IsSidebarExpanded;
                return Task.CompletedTask;
            });

            ToggleQuickBarCommand = new RelayCommand(() =>
            {
                IsQuickBarVisible = !IsQuickBarVisible;
                return Task.CompletedTask;
            });

            ToggleStatusBarCommand = new RelayCommand(() =>
            {
                IsStatusBarVisible = !IsStatusBarVisible;
                return Task.CompletedTask;
            });

            ShowFileDetailsTabCommand = new RelayCommand(() =>
            {
                if (SelectedFile == null && LoadedFiles.Any())
                    SelectedFile = LoadedFiles.First();
                IsFileDetailsTabVisible = true;
                SelectedTabIndex = GetTabIndex("FileDetails");
                return Task.CompletedTask;
            });

            ShowSearchTabCommand = new RelayCommand(() =>
            {
                IsSearchTabVisible = true;
                SelectedTabIndex = GetTabIndex("Search");
                return Task.CompletedTask;
            });

            ShowComparisonTabCommand = new RelayCommand(() =>
            {
                IsComparisonTabVisible = true;
                SelectedTabIndex = GetTabIndex("Comparison");
                return Task.CompletedTask;
            });

            CloseFileDetailsTabCommand = new RelayCommand(() =>
            {
                IsFileDetailsTabVisible = false;
                SelectedFile = null;
                SwitchToNextVisibleTab("FileDetails");
                return Task.CompletedTask;
            });

            CloseSearchTabCommand = new RelayCommand(() =>
            {
                IsSearchTabVisible = false;
                SwitchToNextVisibleTab("Search");
                return Task.CompletedTask;
            });

            CloseComparisonTabCommand = new RelayCommand(() =>
            {
                IsComparisonTabVisible = false;
                SelectedComparison = null;
                SwitchToNextVisibleTab("Comparison");
                return Task.CompletedTask;
            });

            ShowTemplatesTabCommand = new RelayCommand(() =>
            {
                IsTemplatesTabVisible = true;
                SelectedTabIndex = GetTabIndex("Templates");
                return Task.CompletedTask;
            });

            CloseTemplatesTabCommand = new RelayCommand(() =>
            {
                IsTemplatesTabVisible = false;
                SwitchToNextVisibleTab("Templates");
                return Task.CompletedTask;
            });

            ShowSettingsTabCommand = new RelayCommand(() =>
            {
                IsSettingsTabVisible = true;
                SelectedTabIndex = GetTabIndex("Settings");
                return Task.CompletedTask;
            });

            CloseSettingsTabCommand = new RelayCommand(() =>
            {
                IsSettingsTabVisible = false;
                SwitchToNextVisibleTab("Settings");
                return Task.CompletedTask;
            });

            ShowDataRegionsTabCommand = new RelayCommand(() =>
            {
                if (SelectedFile == null && LoadedFiles.Any())
                {
                    SelectedFile = LoadedFiles.First();
                }
                IsDataRegionsTabVisible = true;
                SelectedTabIndex = GetTabIndex("DataRegions");
                return Task.CompletedTask;
            });

            CloseDataRegionsTabCommand = new RelayCommand(() =>
            {
                IsDataRegionsTabVisible = false;
                SwitchToNextVisibleTab("DataRegions");
                return Task.CompletedTask;
            });

            ShowSearchResultsCommand = new RelayCommand(() =>
            {
                IsSearchTabVisible = true;
                SelectedTabIndex = GetTabIndex("Search");
                return Task.CompletedTask;
            });

            ShowWelcomeTabCommand = new RelayCommand(() =>
            {
                IsWelcomeTabVisible = true;
                SelectedTabIndex = GetTabIndex("Welcome");
                return Task.CompletedTask;
            });

            CloseWelcomeTabCommand = new RelayCommand(() =>
            {
                IsWelcomeTabVisible = false;
                SwitchToNextVisibleTab("Welcome");
                return Task.CompletedTask;
            });

            NormalizeExportCommand = new RelayCommand(async () => await ExecuteNormalizeExportAsync());

            ShowAboutCommand = new RelayCommand(async () => await ShowAboutDialogAsync());
            ShowDocumentationCommand = new RelayCommand(async () => await OpenDocumentationAsync());
            ViewErrorLogCommand = new RelayCommand(async () => await OpenErrorLogAsync());
        }
    }

}
