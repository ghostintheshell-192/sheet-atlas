# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 2025-10-24

### Added

- Comprehensive website deployment with automated testing

### Documentation

- Update website for release v0.3.1

### Fixed

- Remove incorrect data from website structured metadata
- Critical fixes for website deployment workflow

### Miscellaneous

- Add website data configuration file

### Refactored

- Remove redundant alpha status message

## [0.3.0] - 2025-10-23

### Added

- Implement honest SEO improvements focused on Windows availability
- Create website template with version placeholders
- Implement unified release workflow
- Unified release pipeline with website automation
- Extract SheetAtlas.Logging as reusable module
- Integrate LogService into error notification flow
- Simplified extension methods for LogService
- Implement native file logging with daily rotation
- Add pre-push hook for automated unit testing
- Add structured JSON file logging system
- Tabular error log view with full history tracking
- Implement collapsible sidebar with vertical toggle bar
- Implement dynamic tab system with progressive disclosure
- Comprehensive UI/UX improvements
- Implement IDisposable pattern on MainWindowViewModel
- Implement parallel file loading with configurable concurrency

### Docs

- Clean up redundant comments and add XML documentation

### Documentation

- Update CHANGELOG to reflect corrected versioning (0.x for alpha)
- Correct README.md to reflect honest platform availability
- Add release process documentation and update CLAUDE.md
- Major SEO improvements for website discoverability
- Revise privacy messaging in features page

### Fixed

- Merge build-release workflow fix from main
- Remove hardcoded version numbers from website, add alpha status notice
- Update website download links to use specific v0.2.0 tag
- Correct error messages for corrupted xlsx files
- Translate all Italian error messages to English
- Catch specific regex exceptions in SearchService instead of all exceptions
- Update test to expect Info level for empty sheets
- Remove redundant .NET project detection in pre-push hook
- Fix tab selection bugs and refactor navigation logic
- Resolve memory leak in UnloadAllFiles by reusing single-file cleanup logic
- Prevent memory leak from inline PropertyChanged subscription
- Eliminate fire-and-forget inconsistency and retry UI flicker
- Correct alpha banner version substitution and platform availability text
- Correct sitemap and alpha banner generation
- Improve website deployment and navbar alignment

### Miscellaneous

- Remove legacy ExcelViewer references and directories
- Remove duplicate release workflows
- Remove unused using directives across codebase

### Perf

- Precompute column comparison data to eliminate redundant calculations
- Eliminate duplicate GetCellAsStringByHeader calls
- Implement flat cache for RefreshCellColors to eliminate nested iteration

### Performance

- Optimize PropertyChanged notifications in TreeSearchResultsViewModel
- Implement flat cache in SearchHistoryItem to reduce O(n³) to O(n)

### Refactor

- Replace toast notifications with inline treeview error display
- Simplify property propagation in OnTabNavigatorPropertyChanged
- Simplify property propagation in OnComparisonCoordinatorPropertyChanged
- Extract SelectedFile setter logic to helper methods
- Unify duplicate switch logic in LoadedFilesManager
- Eliminate message duplication and redundant null checks
- Standardize event delegates and fix memory leak

### Refactored

- Consolidate severity levels using LogSeverity
- Clean up nomenclature in Logging module
- Migrate all UI layer logging to ILogService
- Migrate Infrastructure layer logging to ILogService
- Migrate Core layer logging to ILogService
- Migrate Services/Commands logging to ILogService
- Standardize error handling pattern across file readers
- Simplify null validation in RowComparisonService
- Implement IDisposable pattern to fix memory leaks
- Consolidate button styles to centralized Buttons.axaml
- Extract menu styles and replace hardcoded colors with theme resources
- Add SecondaryButton style and eliminate last hardcoded colors
- Remove deprecated ShowSearchResultsCommand
- Extract TabNavigationCoordinator from MainWindowViewModel
- Extract FileDetails operations to FileDetailsCoordinator
- Simplify UnloadAllFilesAsync with self-documenting helper methods
- Split MainWindowViewModel into partial classes for better organization

### Test

- Add comprehensive regex tests for SearchService

### Testing

- Migrate test mocks from ILogger to ILogService
- Update assertions for English error messages
- Add comprehensive unit tests for structured file logging system

### Ci

- Add workflow_dispatch trigger to build-release workflow
- Integrate Windows installer build into main release workflow

### Release

- Unified release pipeline v1.0

## [0.2.0] - 2025-10-12

### Added

- Add prominent download section to GitHub Page
- Reorganize project structure with branch protections and GitHub Actions
- Add GitHub issue and PR templates
- Add Windows installer infrastructure with Inno Setup
- Implement custom SASheetData for memory optimization
- Add final memory optimizations with TrimExcess
- Add responsive hamburger menu for mobile navigation

### Documentation

- Update website branding from ExcelViewer to SheetAtlas
- Update website downloads - Windows installer ready, Linux/macOS coming soon

### Fixed

- Remove explicit wizard image files from Inno Setup script
- Reorder Pascal functions to declare before use
- Correct project descriptions and update all references from excel-viewer to sheet-atlas
- Implement memory leak cleanup infrastructure (partial)
- Handle empty Excel sheets correctly in OpenXmlFileReader
- Preserve UI state when removing files via Clean All Data
- RowComparison UI cleanup on close (#8, #9)
- Auto-switch to Search Results tab when search completes
- RowComparison cells now adapt to dark mode theme (#7)
- Menu items cleanup and implementation (#1-6)
- Update website version numbers to v1.1.0
- Update project path from ExcelViewer to SheetAtlas in build-release workflow

### Miscellaneous

- Update CHANGELOG for v1.1.0
- Merge develop for v1.0.0 release
- Update GitHub Page for v1.0.0 release

### Performance

- Remove unnecessary ItemArray.ToList() memory copy
- Remove fake async methods from RowComparisonService
- Add string interning for duplicate cell values

### Refactored

- Rename project from ExcelViewer to SheetAtlas
- Fix all nullability warnings for cleaner build

## [0.1.0] - 2025-10-08

### Added

- Implement David Fowl standards for project structure
- Complete UI redesign with professional theme system and Notepad++ style search results
- Add granular selection controls for search results
- Create GitHub Pages website with app-themed design
- Add quality external links to improve SEO
- Add quality external links to improve SEO
- Improve website SEO with advanced meta tags
- Implement centralized exception handling framework
- Add error handling safety nets and activity logging
- Add XlsFileReader for legacy Excel file support
- Add CsvFileReader with auto-delimiter detection
- Update file picker to support all spreadsheet formats
- Add automated changelog generation workflow
- Add automated multi-platform build workflow

### Documentation

- Update Git workflow documentation with develop branch strategy
- Reorganize and clean up documentation structure
- Separate business strategy from public documentation
- Add comprehensive README.md
- Add error handling philosophy to CLAUDE.md
- Add Google Search Console verification meta tag
- Update README for v1.0.0 release

### Fixed

- Resolve comparison view crash and improve styling
- Remove download options from README - project not ready for release
- Add website images to version control
- Synchronize column widths between headers and data cells
- Align row comparison header width with column content
- Use single-line command for PowerShell compatibility

### Miscellaneous

- Remove accidentally committed sample files
- Remove obsolete UI documentation files
- Update CHANGELOG for v1.0.0

### Refactored

- Reorganize project structure following David Fowl standards
- Restructure Core layer following Clean Architecture principles
- Implement David Fowl architecture with separated Infrastructure layer
- Move website files to docs/ directory for GitHub Pages
- Split large ViewModels and improve code organization
- Extract ICellValueReader to eliminate code duplication
- Remove redundant error handling and apply "Never Throw" philosophy
- Extract OpenXML logic to Strategy Pattern for multi-format support

### Testing

- Verify pre-commit hook
- Add integration test infrastructure with real Excel files
- Update test constructors after ExcelReaderService refactoring

### Ci

- Add multi-platform CI workflow with automated testing

### Merge

- Align feature/ui-improvements with develop
- Integrate UI improvements into develop

### Resolve

- Merge conflict in index.html keeping enhanced meta tags

<!-- generated by git-cliff -->
