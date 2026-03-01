using SheetAtlas.UI.Avalonia.Managers.Files;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Managers.Comparison;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
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

            if (SearchViewModel != null)
            {
                SearchViewModel.RegionFilterCleared -= OnRegionFilterCleared;

                if (_searchViewModelPropertyChangedHandler != null)
                {
                    SearchViewModel.PropertyChanged -= _searchViewModelPropertyChangedHandler;
                    _searchViewModelPropertyChangedHandler = null;
                }
            }

            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.RemoveFromListRequested -= OnRemoveFromListRequested;
                FileDetailsViewModel.CleanAllDataRequested -= OnCleanAllDataRequested;
                FileDetailsViewModel.RemoveNotificationRequested -= OnRemoveNotificationRequested;
                FileDetailsViewModel.TryAgainRequested -= OnTryAgainRequested;
                FileDetailsViewModel.RegionAdded -= OnRegionAdded;
                FileDetailsViewModel.RegionDeleted -= OnRegionDeleted;
                FileDetailsViewModel.RegionResized -= OnRegionResized;
            }

            if (TemplateManagementViewModel != null)
            {
                TemplateManagementViewModel.SelectedTemplateChanged -= OnSelectedTemplateChanged;
            }

            if (ColumnLinkingViewModel != null)
            {
                ColumnLinkingViewModel.ColumnLinks.CollectionChanged -= OnColumnLinksCollectionChanged;
            }

            if (RegionsSidebarViewModel != null)
            {
                RegionsSidebarViewModel.RegionClearRequested -= OnRegionClearRequested;
                RegionsSidebarViewModel.RenameRegionRequested -= OnRenameRegionRequested;
                RegionsSidebarViewModel.ClearAllRegionsRequested -= OnClearAllRegionsRequested;
                RegionsSidebarViewModel.ClearFileRegionsRequested -= OnClearFileRegionsRequested;
                RegionsSidebarViewModel.EditRegionRequested -= OnEditRegionRequested;
                RegionsSidebarViewModel.PropertyChanged -= OnRegionsSidebarPropertyChanged;
                RegionsSidebarViewModel.ApplyDetectedRegionsRequested -= OnApplyDetectedRegionsRequested;
            }
        }

        private void OnFileLoaded(object? sender, FileLoadedEventArgs e)
        {
            _logger.LogInfo($"File loaded: {e.File.FileName} (HasErrors: {e.HasErrors})", "MainWindowViewModel");

            OnPropertyChanged(nameof(HasLoadedFiles));
            OnPropertyChanged(nameof(StatusText));

            // Refresh regions sidebar (persisted regions are loaded before this event fires)
            RegionsSidebarViewModel?.RefreshFromFiles(LoadedFiles);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

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
            OnPropertyChanged(nameof(StatusText));

            // Refresh regions sidebar (removed file's regions should disappear)
            RegionsSidebarViewModel?.RefreshFromFiles(LoadedFiles);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

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

            // Connect column filter to search (in case ColumnLinkingViewModel was set first)
            ConnectColumnFilterToSearch();

            SearchViewModel.RegionFilterCleared += OnRegionFilterCleared;

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
            FileDetailsViewModel.RegionDeleted += OnRegionDeleted;
            FileDetailsViewModel.RegionResized += OnRegionResized;

            // Connect semantic name provider (in case ColumnLinkingViewModel was set first)
            ConnectSemanticNameProvider();

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
            if (ColumnLinkingViewModel != null)
            {
                var provider = (string fileName) => ColumnLinkingViewModel.GetSemanticNamesForFile(fileName);

                TemplateManagementViewModel?.SetSemanticNameProvider(provider);
                FileDetailsViewModel?.SetSemanticNameProvider(provider);

                // Also connect included columns provider for export filtering
                FileDetailsViewModel?.SetIncludedColumnsProvider(() => ColumnLinkingViewModel.GetIncludedColumnNames());
            }
        }

        private void OnSelectedTemplateChanged(object? sender, SelectedTemplateChangedEventArgs e)
        {
            ColumnLinkingViewModel?.SetHighlightedColumns(e.Template);
        }

        public void SetColumnLinkingViewModel(ColumnLinkingViewModel columnLinkingViewModel)
        {
            ColumnLinkingViewModel = columnLinkingViewModel ?? throw new ArgumentNullException(nameof(columnLinkingViewModel));

            // Subscribe to ColumnLinks changes for badge and status bar updates
            ColumnLinkingViewModel.ColumnLinks.CollectionChanged += OnColumnLinksCollectionChanged;

            // Connect semantic name provider (in case TemplateManagementViewModel was set first)
            ConnectSemanticNameProvider();

            // Connect column filter to search
            ConnectColumnFilterToSearch();
        }

        private void ConnectColumnFilterToSearch()
        {
            if (SearchViewModel != null && ColumnLinkingViewModel != null)
            {
                SearchViewModel.SetIncludedColumnsProvider(() => ColumnLinkingViewModel.GetIncludedColumnNames());
            }
        }

        private void OnColumnLinksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ColumnCount));
            OnPropertyChanged(nameof(StatusText));
        }

        public void SetRegionsSidebarViewModel(RegionsSidebarViewModel regionsSidebarViewModel)
        {
            RegionsSidebarViewModel = regionsSidebarViewModel ?? throw new ArgumentNullException(nameof(regionsSidebarViewModel));

            // Connect FileDetailsViewModel region events to sidebar
            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.RegionAdded += OnRegionAdded;
            }

            // Connect clear, rename and edit from sidebar
            RegionsSidebarViewModel.RegionClearRequested += OnRegionClearRequested;
            RegionsSidebarViewModel.RenameRegionRequested += OnRenameRegionRequested;
            RegionsSidebarViewModel.ClearAllRegionsRequested += OnClearAllRegionsRequested;
            RegionsSidebarViewModel.ClearFileRegionsRequested += OnClearFileRegionsRequested;
            RegionsSidebarViewModel.EditRegionRequested += OnEditRegionRequested;

            // Connect region selection to search filtering
            RegionsSidebarViewModel.PropertyChanged += OnRegionsSidebarPropertyChanged;

            // Connect cross-file detection dependencies
            RegionsSidebarViewModel.SetDetectionDependencies(
                _regionDetectionService,
                () => LoadedFiles);
            RegionsSidebarViewModel.ApplyDetectedRegionsRequested += OnApplyDetectedRegionsRequested;

            // Populate from already-loaded files
            RegionsSidebarViewModel.RefreshFromFiles(LoadedFiles);
        }

        private void OnRegionsSidebarPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegionsSidebarViewModel.SelectedRegion)
                || e.PropertyName == nameof(RegionsSidebarViewModel.SelectedRegionGroup)
                || e.PropertyName == nameof(RegionsSidebarViewModel.IsRegionView))
            {
                ApplyRegionFilter();
            }
        }

        private void ApplyRegionFilter()
        {
            if (SearchViewModel == null || RegionsSidebarViewModel == null) return;

            if (RegionsSidebarViewModel.IsRegionView && RegionsSidebarViewModel.SelectedRegionGroup != null)
            {
                // "By Region" view: cross-file filter by region name
                var group = RegionsSidebarViewModel.SelectedRegionGroup;
                var regions = RegionsSidebarViewModel.CollectRegionsByName(group.RegionName);
                SearchViewModel.SetCrossFileRegionFilter(group.RegionName, regions);
                _logger.LogInfo($"Cross-file region filter set: '{group.RegionName}' across {regions.Count} entries", "MainWindowViewModel");
            }
            else if (!RegionsSidebarViewModel.IsRegionView && RegionsSidebarViewModel.SelectedRegion != null)
            {
                // "By File" view: single-file filter
                var selected = RegionsSidebarViewModel.SelectedRegion;
                SearchViewModel.SetSelectedRegion(selected.FilePath, selected.SheetName, selected.Region);
                _logger.LogInfo($"Region filter set: '{selected.Name}' on sheet '{selected.SheetName}'", "MainWindowViewModel");
            }
            else
            {
                SearchViewModel.ClearSelectedRegion();
                _logger.LogInfo("Region filter cleared", "MainWindowViewModel");
            }

            OnPropertyChanged(nameof(HasMultipleRegionsMessage));
        }

        private void OnRegionFilterCleared(object? sender, EventArgs e)
        {
            if (RegionsSidebarViewModel != null)
                RegionsSidebarViewModel.SelectedRegion = null;

            OnPropertyChanged(nameof(HasMultipleRegionsMessage));
        }

        private void OnRegionResized(object? sender, RegionEventArgs e)
        {
            RegionsSidebarViewModel?.UpdateRegion(e.FilePath, e.SheetName, e.Region);
            _logger.LogInfo($"Region '{e.Region.Name}' resized, sidebar updated", "MainWindowViewModel");
        }

        private void OnRegionDeleted(object? sender, RegionEventArgs e)
        {
            RegionsSidebarViewModel?.RemoveRegion(e.FilePath, e.SheetName, e.Region.Name);
            ClearSearchFilterIfNeeded(e.Region.Name);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

            _logger.LogInfo($"Region '{e.Region.Name}' deleted from canvas, sidebar synced", "MainWindowViewModel");
        }

        private void ClearSearchFilterIfNeeded(string regionName)
        {
            if (SearchViewModel?.ActiveRegionName == regionName)
                SearchViewModel.ClearSelectedRegion();
        }

        private void OnRegionAdded(object? sender, RegionEventArgs e)
        {
            var fileName = Path.GetFileName(e.FilePath);
            RegionsSidebarViewModel?.AddRegion(e.FilePath, fileName, e.SheetName, e.Region);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));
        }

        private void OnEditRegionRequested(object? sender, RegionEventArgs e)
        {
            NavigateToDataRegion(e.FilePath, e.SheetName, e.Region.Name);
        }

        private void OnRegionClearRequested(object? sender, RegionEventArgs e)
        {
            var fileVm = LoadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(e.FilePath, StringComparison.OrdinalIgnoreCase));
            if (fileVm?.File == null) return;

            var sheet = fileVm.File.GetSheet(e.SheetName);
            if (sheet == null) return;

            sheet.RemoveDataRegion(e.Region.Name);
            RegionsSidebarViewModel?.RemoveRegion(e.FilePath, e.SheetName, e.Region.Name);

            if (FileDetailsViewModel?.ActiveRegion?.Name == e.Region.Name)
                FileDetailsViewModel.ActiveRegion = null;

            _ = PersistRegionsForFileAsync(fileVm);
            FileDetailsViewModel?.RefreshRegions();
            ClearSearchFilterIfNeeded(e.Region.Name);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

            _logger.LogInfo($"Region '{e.Region.Name}' cleared from sheet '{e.SheetName}'", "MainWindowViewModel");
        }

        private void OnRenameRegionRequested(object? sender, RenameRegionEventArgs e)
        {
            var fileVm = LoadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(e.FilePath, StringComparison.OrdinalIgnoreCase));
            if (fileVm?.File == null) return;

            var sheet = fileVm.File.GetSheet(e.SheetName);
            if (sheet == null) return;

            var existing = sheet.GetDataRegion(e.Region.Name);
            if (existing == null) return;

            // Reject if the target name already exists
            if (sheet.GetDataRegion(e.NewName) != null)
            {
                _logger.LogWarning(
                    $"Region rename skipped: '{e.NewName}' already exists in sheet '{e.SheetName}'",
                    "MainWindowViewModel");
                return;
            }

            sheet.RemoveDataRegion(e.Region.Name);
            var renamed = existing with { Name = e.NewName };
            sheet.AddDataRegion(renamed);

            // Keep canvas in sync when the renamed region is currently active
            if (FileDetailsViewModel?.ActiveRegion?.Name == e.Region.Name &&
                FileDetailsViewModel.SelectedFile?.FilePath.Equals(e.FilePath, StringComparison.OrdinalIgnoreCase) == true)
            {
                FileDetailsViewModel.ActiveRegion = renamed;
            }

            _ = PersistRegionsForFileAsync(fileVm);
            RegionsSidebarViewModel?.RefreshFromFiles(LoadedFiles);
            FileDetailsViewModel?.RefreshRegions();
            ClearSearchFilterIfNeeded(e.Region.Name);
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

            _logger.LogInfo(
                $"Region '{e.Region.Name}' renamed to '{e.NewName}' in sheet '{e.SheetName}'",
                "MainWindowViewModel");
        }

        private async void OnClearAllRegionsRequested(object? sender, EventArgs e)
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "This will remove all data region definitions from all loaded files.\n\nThe spreadsheet data is not affected.",
                "Clear All Regions");
            if (!confirmed) return;

            foreach (var fileVm in LoadedFiles.ToList())
            {
                if (fileVm.File == null) continue;
                ClearAllRegionsForFile(fileVm);
            }

            RegionsSidebarViewModel?.RefreshFromFiles(LoadedFiles);
            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.ActiveRegion = null;
                FileDetailsViewModel.RefreshRegions();
            }
            SearchViewModel?.ClearSelectedRegion();
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

            _logger.LogInfo("All regions cleared from all files", "MainWindowViewModel");
        }

        private async void OnClearFileRegionsRequested(object? sender, ClearFileRegionsEventArgs e)
        {
            var fileVm = LoadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(e.FilePath, StringComparison.OrdinalIgnoreCase));
            if (fileVm?.File == null) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"This will remove all data region definitions from \"{e.FileName}\".\n\nThe spreadsheet data is not affected.",
                "Clear File Regions");
            if (!confirmed) return;

            ClearAllRegionsForFile(fileVm);
            RegionsSidebarViewModel?.RefreshFromFiles(LoadedFiles);

            if (FileDetailsViewModel != null &&
                FileDetailsViewModel.SelectedFile?.FilePath.Equals(e.FilePath, StringComparison.OrdinalIgnoreCase) == true)
            {
                FileDetailsViewModel.ActiveRegion = null;
                FileDetailsViewModel.RefreshRegions();
            }

            SearchViewModel?.ClearSelectedRegion();
            OnPropertyChanged(nameof(HasMultipleRegionsMessage));

            _logger.LogInfo($"All regions cleared from file '{e.FileName}'", "MainWindowViewModel");
        }

        private void ClearAllRegionsForFile(IFileLoadResultViewModel fileVm)
        {
            if (fileVm.File == null) return;

            foreach (var (_, sheetData) in fileVm.File.Sheets)
            {
                foreach (var regionName in sheetData.DataRegions.Keys.ToList())
                    sheetData.RemoveDataRegion(regionName);
            }

            _ = PersistRegionsForFileAsync(fileVm);
        }

        private void OnApplyDetectedRegionsRequested(object? sender, ApplyDetectedRegionsEventArgs e)
        {
            int applied = 0;

            foreach (var item in e.Selections)
            {
                if (item.Detection.DetectedRegion == null) continue;

                var fileVm = LoadedFiles.FirstOrDefault(f =>
                    f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase));
                if (fileVm?.File == null) continue;

                var sheet = fileVm.File.GetSheet(item.SheetName);
                if (sheet == null) continue;

                // Skip if region with same name already exists
                if (sheet.GetDataRegion(item.Detection.DetectedRegion.Name) != null) continue;

                try
                {
                    sheet.AddDataRegion(item.Detection.DetectedRegion);
                    RegionsSidebarViewModel?.AddRegion(item.FilePath, item.FileName, item.SheetName, item.Detection.DetectedRegion);

                    // Persist for this file
                    _ = PersistRegionsForFileAsync(fileVm);
                    applied++;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning($"Could not apply region to '{item.FileName}/{item.SheetName}': {ex.Message}", "MainWindowViewModel");
                }
            }

            if (applied > 0)
            {
                OnPropertyChanged(nameof(HasMultipleRegionsMessage));
                _logger.LogInfo($"Applied detected regions to {applied} file(s)", "MainWindowViewModel");
            }
        }

        private async Task PersistRegionsForFileAsync(IFileLoadResultViewModel fileVm)
        {
            if (fileVm.File == null) return;

            var data = new DataRegionFile
            {
                LastModified = DateTime.UtcNow,
                Sheets = new Dictionary<string, SheetRegionsDto>()
            };

            foreach (var (sheetName, sheetData) in fileVm.File.Sheets)
            {
                var regions = sheetData.DataRegions;
                if (regions.Count > 0)
                {
                    data.Sheets[sheetName] = new SheetRegionsDto
                    {
                        Regions = new Dictionary<string, DataRegion>(regions)
                    };
                }
            }

            await _dataRegionPersistenceService.SaveAsync(fileVm.FilePath, data);
        }

        /// <summary>
        /// Navigate to a specific data region: selects the file, sheet, activates the region,
        /// and switches to the DataRegions tab.
        /// </summary>
        public void NavigateToDataRegion(string filePath, string sheetName, string regionName)
        {
            var fileVm = LoadedFiles.FirstOrDefault(f =>
                f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (fileVm == null) return;

            SelectedFile = fileVm;

            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.SelectedSheetName = sheetName;
                FileDetailsViewModel.ActivateRegionForEditing(regionName);
            }

            IsDataRegionsTabVisible = true;
            SelectedTabIndex = GetTabIndex("DataRegions");
        }

        public void SetSettingsViewModel(SettingsViewModel settingsViewModel)
        {
            SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
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
                "Welcome" => 0,       // First TabItem in XAML
                "FileDetails" => 1,   // Second TabItem in XAML
                "Search" => 2,        // Third TabItem in XAML
                "Comparison" => 3,    // Fourth TabItem in XAML
                "Templates" => 4,     // Fifth TabItem in XAML
                "DataRegions" => 5,   // Sixth TabItem in XAML
                "Settings" => 6,      // Seventh TabItem in XAML
                _ => -1               // Invalid tab name
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
                "Welcome" => new[] { "FileDetails", "Search", "Comparison", "Templates", "DataRegions", "Settings" },
                "FileDetails" => new[] { "Search", "Comparison", "Templates", "DataRegions", "Settings", "Welcome" },
                "Search" => new[] { "FileDetails", "Comparison", "Templates", "DataRegions", "Settings", "Welcome" },
                "Comparison" => new[] { "Search", "FileDetails", "Templates", "DataRegions", "Settings", "Welcome" },
                "Templates" => new[] { "Search", "FileDetails", "Comparison", "DataRegions", "Settings", "Welcome" },
                "DataRegions" => new[] { "FileDetails", "Search", "Comparison", "Templates", "Settings", "Welcome" },
                "Settings" => new[] { "Search", "FileDetails", "Comparison", "Templates", "DataRegions", "Welcome" },
                _ => Array.Empty<string>()
            };

            foreach (var tabName in tabPriorities)
            {
                bool isVisible = tabName switch
                {
                    "Welcome" => IsWelcomeTabVisible,
                    "FileDetails" => IsFileDetailsTabVisible,
                    "Search" => IsSearchTabVisible,
                    "Comparison" => IsComparisonTabVisible,
                    "Templates" => IsTemplatesTabVisible,
                    "DataRegions" => IsDataRegionsTabVisible,
                    "Settings" => IsSettingsTabVisible,
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
