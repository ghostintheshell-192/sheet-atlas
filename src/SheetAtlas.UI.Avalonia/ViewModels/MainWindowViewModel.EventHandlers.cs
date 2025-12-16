using SheetAtlas.UI.Avalonia.Managers.Files;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Managers.Comparison;
using SheetAtlas.Core.Domain.Entities;
using System.Collections.ObjectModel;

namespace SheetAtlas.UI.Avalonia.ViewModels
{
    public partial class MainWindowViewModel
    {
        public ReadOnlyObservableCollection<IFileLoadResultViewModel> LoadedFiles => _filesManager.LoadedFiles;
        public ReadOnlyObservableCollection<RowComparisonViewModel> RowComparisons => _comparisonCoordinator.RowComparisons;
        public bool HasLoadedFiles => LoadedFiles.Count > 0;

        public void SubscribeToEvents()
        {
            _filesManager.FileLoaded += OnFileLoaded;
            _filesManager.FileRemoved += OnFileRemoved;
            _filesManager.FileLoadFailed += OnFileLoadFailed;
            _filesManager.FileReloaded += OnFileReloaded;

            _comparisonCoordinator.SelectionChanged += OnComparisonSelectionChanged;
            _comparisonCoordinator.ComparisonRemoved += OnComparisonRemoved;
            _comparisonCoordinator.PropertyChanged += OnComparisonCoordinatorPropertyChanged;
        }

        private void UnsubscribeFromEvents()
        {
            _filesManager.FileLoaded -= OnFileLoaded;
            _filesManager.FileRemoved -= OnFileRemoved;
            _filesManager.FileLoadFailed -= OnFileLoadFailed;
            _filesManager.FileReloaded -= OnFileReloaded;
            _comparisonCoordinator.SelectionChanged -= OnComparisonSelectionChanged;
            _comparisonCoordinator.ComparisonRemoved -= OnComparisonRemoved;
            _comparisonCoordinator.PropertyChanged -= OnComparisonCoordinatorPropertyChanged;

            if (SearchViewModel != null && _searchViewModelPropertyChangedHandler != null)
            {
                SearchViewModel.PropertyChanged -= _searchViewModelPropertyChangedHandler;
                _searchViewModelPropertyChangedHandler = null;
            }

            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.RemoveFromListRequested -= OnRemoveFromListRequested;
                FileDetailsViewModel.CleanAllDataRequested -= OnCleanAllDataRequested;
                FileDetailsViewModel.RemoveNotificationRequested -= OnRemoveNotificationRequested;
                FileDetailsViewModel.TryAgainRequested -= OnTryAgainRequested;
            }

            if (TemplateManagementViewModel != null)
            {
                TemplateManagementViewModel.SelectedTemplateChanged -= OnSelectedTemplateChanged;
            }
        }

        private void OnFileLoaded(object? sender, FileLoadedEventArgs e)
        {
            _logger.LogInfo($"File loaded: {e.File.FileName} (HasErrors: {e.HasErrors})", "MainWindowViewModel");

            OnPropertyChanged(nameof(HasLoadedFiles));

            if (LoadedFiles.Count == 1)
            {
                IsSidebarExpanded = true;
            }
        }

        private void OnFileReloaded(object? sender, FileReloadedEventArgs e)
        {
            _logger.LogInfo($"File reloaded: {e.NewFile.FileName}", "MainWindowViewModel");

            SelectedFile = e.NewFile;

            IsFileDetailsTabVisible = true;
            SelectedTabIndex = GetTabIndex("FileDetails");
        }

        private void OnFileRemoved(object? sender, FileRemovedEventArgs e)
        {
            _logger.LogInfo($"File removed: {e.File.FileName} (isRetry: {e.IsRetry})", "MainWindowViewModel");

            OnPropertyChanged(nameof(HasLoadedFiles));

            if (!e.IsRetry && SelectedFile == e.File)
            {
                SelectedFile = null;
            }

            if (LoadedFiles.Count == 0 && !e.IsRetry)
            {
                IsSidebarExpanded = false;
            }
        }

        private void OnFileLoadFailed(object? sender, FileLoadFailedEventArgs e)
        {
            _logger.LogError($"File load failed: {e.FilePath}", e.Exception, "MainWindowViewModel");
        }

        private void OnComparisonCoordinatorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IRowComparisonCoordinator.SelectedComparison))
            {
                OnPropertyChanged(nameof(SelectedComparison));
            }
        }

        private void OnComparisonRemoved(object? sender, ComparisonRemovedEventArgs e)
        {
            TreeSearchResultsViewModel?.ClearSelection();

            // Only switch away from Comparison tab if no comparisons remain
            if (RowComparisons.Count == 0)
            {
                IsComparisonTabVisible = false;

                if (IsSearchTabVisible)
                {
                    SelectedTabIndex = GetTabIndex("Search");
                }
                else
                {
                    SelectedTabIndex = -1;
                }
            }
            // If there are still comparisons, stay on the Comparison tab

            _logger.LogInfo($"Comparison removed, {RowComparisons.Count} remaining", "MainWindowViewModel");
        }

        private void OnComparisonSelectionChanged(object? sender, ComparisonSelectionChangedEventArgs e)
        {
            if (e.NewSelection != null)
            {
                IsComparisonTabVisible = true;
                SelectedTabIndex = GetTabIndex("Comparison");
            }
        }

        public void SetSearchViewModel(SearchViewModel searchViewModel)
        {
            SearchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
            SearchViewModel.Initialize(LoadedFiles);
            OnPropertyChanged(nameof(ShowAllFilesCommand));

            if (SearchViewModel != null)
            {
                _searchViewModelPropertyChangedHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(SearchViewModel.SearchResults) && TreeSearchResultsViewModel != null)
                    {
                        var query = SearchViewModel.SearchQuery;
                        var results = SearchViewModel.SearchResults;
                        if (!string.IsNullOrWhiteSpace(query) && results?.Any() == true)
                        {
                            TreeSearchResultsViewModel.AddSearchResults(query, results.ToList());

                            IsSearchTabVisible = true;
                            SelectedTabIndex = GetTabIndex("Search");
                        }
                    }
                };

                SearchViewModel.PropertyChanged += _searchViewModelPropertyChangedHandler;
            }
        }

        public void SetFileDetailsViewModel(FileDetailsViewModel fileDetailsViewModel)
        {
            FileDetailsViewModel = fileDetailsViewModel ?? throw new ArgumentNullException(nameof(fileDetailsViewModel));

            FileDetailsViewModel.RemoveFromListRequested += OnRemoveFromListRequested;
            FileDetailsViewModel.CleanAllDataRequested += OnCleanAllDataRequested;
            FileDetailsViewModel.RemoveNotificationRequested += OnRemoveNotificationRequested;
            FileDetailsViewModel.TryAgainRequested += OnTryAgainRequested;

            FileDetailsViewModel.SelectedFile = SelectedFile;
        }

        public void SetTreeSearchResultsViewModel(TreeSearchResultsViewModel treeSearchResultsViewModel)
        {
            TreeSearchResultsViewModel = treeSearchResultsViewModel ?? throw new ArgumentNullException(nameof(treeSearchResultsViewModel));

            TreeSearchResultsViewModel.RowComparisonCreated += OnRowComparisonCreated;
        }

        public void SetTemplateManagementViewModel(TemplateManagementViewModel templateManagementViewModel)
        {
            TemplateManagementViewModel = templateManagementViewModel ?? throw new ArgumentNullException(nameof(templateManagementViewModel));

            // Connect template selection to column highlighting
            TemplateManagementViewModel.SelectedTemplateChanged += OnSelectedTemplateChanged;

            // Connect semantic name provider from column linking
            // Note: This requires ColumnLinkingViewModel to be set first or we defer the connection
            ConnectSemanticNameProvider();
        }

        private void ConnectSemanticNameProvider()
        {
            if (TemplateManagementViewModel != null && ColumnLinkingViewModel != null)
            {
                TemplateManagementViewModel.SetSemanticNameProvider(
                    fileName => ColumnLinkingViewModel.GetSemanticNamesForFile(fileName));
            }
        }

        private void OnSelectedTemplateChanged(object? sender, SelectedTemplateChangedEventArgs e)
        {
            ColumnLinkingViewModel?.SetHighlightedColumns(e.Template);
        }

        public void SetColumnLinkingViewModel(ColumnLinkingViewModel columnLinkingViewModel)
        {
            ColumnLinkingViewModel = columnLinkingViewModel ?? throw new ArgumentNullException(nameof(columnLinkingViewModel));

            // Connect semantic name provider (in case TemplateManagementViewModel was set first)
            ConnectSemanticNameProvider();
        }

        /// <summary>
        /// Update the list of selected files from the sidebar.
        /// Called by MainWindow code-behind when ListBox selection changes.
        /// </summary>
        public void UpdateSelectedFiles(IReadOnlyList<IFileLoadResultViewModel> selectedFiles)
        {
            // Pass the full list to TemplateManagementViewModel for multi-file operations
            TemplateManagementViewModel?.SetSelectedFiles(selectedFiles);

            // Update SelectedFile to the first selected (for FileDetails compatibility)
            // Note: SelectedFile binding will also update, but this ensures sync
            if (selectedFiles.Count > 0 && SelectedFile != selectedFiles[0])
            {
                // Don't update if already correct - avoids infinite loop with SelectedItem binding
                // The ListBox SelectedItem binding handles single selection
            }
        }

        private void OnRowComparisonCreated(object? sender, RowComparison comparison)
        {
            _comparisonCoordinator.CreateComparison(comparison);
        }

        // Event handlers for FileDetailsViewModel - delegate to FilesManager
        private void OnRemoveFromListRequested(object? sender, FileActionEventArgs e) => _filesManager.RemoveFile(e.File);

        private void OnCleanAllDataRequested(object? sender, FileActionEventArgs e)
        {
            var file = e.File;
            if (file == null)
            {
                _logger.LogWarning("Clean all data requested with null file", "MainWindowViewModel");
                return;
            }

            _logger.LogInfo($"Clean all data requested for: {file.FileName}", "MainWindowViewModel");

            if (SelectedFile == file)
            {
                SelectedFile = null;
            }

            TreeSearchResultsViewModel?.RemoveSearchResultsForFile(file.File!);

            SearchViewModel?.RemoveResultsForFile(file.File!);

            _comparisonCoordinator.RemoveComparisonsForFile(file.File!);

            file.Dispose();

            _filesManager.RemoveFile(file);

            _logger.LogInfo($"Cleaned all data for file: {file.FileName}", "MainWindowViewModel");

            // AGGRESSIVE CLEANUP: Force garbage collection after file removal
            // REASON: DataTable objects (100-500 MB each) end up in Large Object Heap (LOH)
            // ISSUE: .NET GC is lazy for Gen 2/LOH - can wait minutes before collection
            // IMPACT: Without this, memory stays high even after Dispose() until GC decides to run
            Task.Run(() =>
            {
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            });
        }

        private void OnRemoveNotificationRequested(object? sender, FileActionEventArgs e) => _filesManager.RemoveFile(e.File);

        private void OnTryAgainRequested(object? sender, FileActionEventArgs e)
        {
            var file = e.File;
            if (file == null)
            {
                _logger.LogWarning("Try again requested but file is null", "MainWindowViewModel");
                return;
            }

            // Use fire-and-forget pattern with proper error handling
            _ = RetryLoadFileAsync(file);
        }

        /// <summary>
        /// Returns the absolute tab index for a given tab name.
        /// These indices correspond to the TabItem positions in MainWindow.axaml.
        /// IMPORTANT: These are absolute indices in the XAML markup, NOT relative to visible tabs.
        /// Avalonia TabControl uses absolute indices regardless of TabItem visibility.
        /// </summary>
        private static int GetTabIndex(string tabName)
        {
            return tabName switch
            {
                "FileDetails" => 0,  // First TabItem in XAML
                "Search" => 1,       // Second TabItem in XAML
                "Comparison" => 2,   // Third TabItem in XAML
                "Templates" => 3,    // Fourth TabItem in XAML
                _ => -1              // Invalid tab name
            };
        }

        /// <summary>
        /// Switches to the next visible tab after closing the current one.
        /// Uses a priority order to determine which tab to select.
        /// If no tabs are visible, sets SelectedTabIndex to -1 (welcome screen).
        /// </summary>
        /// <param name="closedTabName">The name of the tab being closed (to exclude from selection)</param>
        private void SwitchToNextVisibleTab(string closedTabName)
        {
            // Define priority order for tab selection
            // Each tab type has its preferred fallback sequence
            var tabPriorities = closedTabName switch
            {
                "FileDetails" => new[] { "Search", "Comparison", "Templates" },
                "Search" => new[] { "FileDetails", "Comparison", "Templates" },
                "Comparison" => new[] { "Search", "FileDetails", "Templates" },
                "Templates" => new[] { "Search", "FileDetails", "Comparison" },
                _ => Array.Empty<string>()
            };

            foreach (var tabName in tabPriorities)
            {
                bool isVisible = tabName switch
                {
                    "FileDetails" => IsFileDetailsTabVisible,
                    "Search" => IsSearchTabVisible,
                    "Comparison" => IsComparisonTabVisible,
                    "Templates" => IsTemplatesTabVisible,
                    _ => false
                };

                if (isVisible)
                {
                    SelectedTabIndex = GetTabIndex(tabName);
                    return;
                }
            }

            SelectedTabIndex = -1;
        }
    }
}
