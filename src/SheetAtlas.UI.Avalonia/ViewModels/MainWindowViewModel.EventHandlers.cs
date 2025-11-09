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
            // Subscribe to file manager events
            _filesManager.FileLoaded += OnFileLoaded;
            _filesManager.FileRemoved += OnFileRemoved;
            _filesManager.FileLoadFailed += OnFileLoadFailed;
            _filesManager.FileReloaded += OnFileReloaded;

            // Subscribe to comparison coordinator events
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

            // Unsubscribe from SearchViewModel PropertyChanged to prevent memory leak
            if (SearchViewModel != null && _searchViewModelPropertyChangedHandler != null)
            {
                SearchViewModel.PropertyChanged -= _searchViewModelPropertyChangedHandler;
                _searchViewModelPropertyChangedHandler = null;
            }

            // Unsubscribe from FileDetailsViewModel events to prevent memory leak
            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.RemoveFromListRequested -= OnRemoveFromListRequested;
                FileDetailsViewModel.CleanAllDataRequested -= OnCleanAllDataRequested;
                FileDetailsViewModel.RemoveNotificationRequested -= OnRemoveNotificationRequested;
                FileDetailsViewModel.TryAgainRequested -= OnTryAgainRequested;
            }
        }

        private void OnFileLoaded(object? sender, FileLoadedEventArgs e)
        {
            _logger.LogInfo($"File loaded: {e.File.FileName} (HasErrors: {e.HasErrors})", "MainWindowViewModel");

            // Notify that HasLoadedFiles changed
            OnPropertyChanged(nameof(HasLoadedFiles));

            // Auto-open sidebar when first file is loaded
            if (LoadedFiles.Count == 1)
            {
                IsSidebarExpanded = true;
            }
        }

        private void OnFileReloaded(object? sender, FileReloadedEventArgs e)
        {
            _logger.LogInfo($"File reloaded: {e.NewFile.FileName}", "MainWindowViewModel");

            // Auto-select the reloaded file to show updated details
            SelectedFile = e.NewFile;

            // Show File Details tab to display the updated log
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
            // Propagate PropertyChanged from Coordinator to ViewModel
            if (e.PropertyName == nameof(IRowComparisonCoordinator.SelectedComparison))
            {
                OnPropertyChanged(nameof(SelectedComparison));
            }
        }

        private void OnComparisonRemoved(object? sender, ComparisonRemovedEventArgs e)
        {
            // Clear all selections in TreeSearchResultsViewModel
            TreeSearchResultsViewModel?.ClearSelection();

            // If Search tab is visible, switch to it; otherwise just deselect
            if (IsSearchTabVisible)
            {
                SelectedTabIndex = GetTabIndex("Search");
            }
            else
            {
                SelectedTabIndex = -1;
            }

            _logger.LogInfo("Comparison removed and selections cleared", "MainWindowViewModel");
        }

        private void OnComparisonSelectionChanged(object? sender, ComparisonSelectionChangedEventArgs e)
        {
            // Show/hide comparison tab based on selection
            if (e.NewSelection != null)
            {
                // Comparison created/selected - show and switch to Comparison tab
                IsComparisonTabVisible = true;
                SelectedTabIndex = GetTabIndex("Comparison");
            }
        }

        public void SetSearchViewModel(SearchViewModel searchViewModel)
        {
            SearchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
            SearchViewModel.Initialize(LoadedFiles);
            OnPropertyChanged(nameof(ShowAllFilesCommand));

            // Wire up search results to tree view
            if (SearchViewModel != null)
            {
                // Store handler as field to enable proper cleanup
                _searchViewModelPropertyChangedHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(SearchViewModel.SearchResults) && TreeSearchResultsViewModel != null)
                    {
                        var query = SearchViewModel.SearchQuery;
                        var results = SearchViewModel.SearchResults;
                        if (!string.IsNullOrWhiteSpace(query) && results?.Any() == true)
                        {
                            TreeSearchResultsViewModel.AddSearchResults(query, results.ToList());

                            // Show and switch to Search tab to display results
                            IsSearchTabVisible = true;
                            SelectedTabIndex = GetTabIndex("Search");
                        }
                    }
                };

                // Subscribe to search results changes
                SearchViewModel.PropertyChanged += _searchViewModelPropertyChangedHandler;
            }
        }

        public void SetFileDetailsViewModel(FileDetailsViewModel fileDetailsViewModel)
        {
            FileDetailsViewModel = fileDetailsViewModel ?? throw new ArgumentNullException(nameof(fileDetailsViewModel));

            // Wire up events from FileDetailsViewModel
            FileDetailsViewModel.RemoveFromListRequested += OnRemoveFromListRequested;
            FileDetailsViewModel.CleanAllDataRequested += OnCleanAllDataRequested;
            FileDetailsViewModel.RemoveNotificationRequested += OnRemoveNotificationRequested;
            FileDetailsViewModel.TryAgainRequested += OnTryAgainRequested;

            // Set current selection if any
            FileDetailsViewModel.SelectedFile = SelectedFile;
        }

        public void SetTreeSearchResultsViewModel(TreeSearchResultsViewModel treeSearchResultsViewModel)
        {
            TreeSearchResultsViewModel = treeSearchResultsViewModel ?? throw new ArgumentNullException(nameof(treeSearchResultsViewModel));

            // Wire up row comparison creation
            TreeSearchResultsViewModel.RowComparisonCreated += OnRowComparisonCreated;
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

            // Clear selection if this file is currently selected (prevent memory leak)
            if (SelectedFile == file)
            {
                SelectedFile = null;
            }

            // Remove search results that reference this file (TreeView history)
            TreeSearchResultsViewModel?.RemoveSearchResultsForFile(file.File!);

            // Remove current search results that reference this file (SearchViewModel)
            SearchViewModel?.RemoveResultsForFile(file.File!);

            // Remove row comparisons that reference this file
            _comparisonCoordinator.RemoveComparisonsForFile(file.File!);

            // Dispose ViewModel (which disposes ExcelFile and DataTables, then nulls the reference)
            file.Dispose();

            // Finally, remove the file from the loaded files list
            _filesManager.RemoveFile(file);

            _logger.LogInfo($"Cleaned all data for file: {file.FileName}", "MainWindowViewModel");

            // AGGRESSIVE CLEANUP: Force garbage collection after file removal
            // REASON: DataTable objects (100-500 MB each) end up in Large Object Heap (LOH)
            // ISSUE: .NET GC is lazy for Gen 2/LOH - can wait minutes before collection
            // IMPACT: Without this, memory stays high even after Dispose() until GC decides to run
            // TODO: When DataTable is replaced with lightweight structures, this can be removed
            //       or changed to standard GC.Collect() without aggressive mode
            Task.Run(() =>
            {
                // Enable LOH compaction for this collection cycle
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

                // Force Gen 2 + LOH collection with compaction (blocking in background thread)
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
                "FileDetails" => new[] { "Search", "Comparison" },
                "Search" => new[] { "FileDetails", "Comparison" },
                "Comparison" => new[] { "Search", "FileDetails" },
                _ => Array.Empty<string>()
            };

            // Find first visible tab from priority list
            foreach (var tabName in tabPriorities)
            {
                bool isVisible = tabName switch
                {
                    "FileDetails" => IsFileDetailsTabVisible,
                    "Search" => IsSearchTabVisible,
                    "Comparison" => IsComparisonTabVisible,
                    _ => false
                };

                if (isVisible)
                {
                    SelectedTabIndex = GetTabIndex(tabName);
                    return;
                }
            }

            // No tabs visible - show welcome screen
            SelectedTabIndex = -1;
        }
    }
}
