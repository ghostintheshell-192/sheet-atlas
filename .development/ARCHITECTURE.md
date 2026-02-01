# Architecture Reference

Quick reference for navigating the SheetAtlas codebase.
For diagrams and detailed explanations, see [docs/project/ARCHITECTURE.md](../docs/project/ARCHITECTURE.md).

## Layer Overview

| Layer | Project | Purpose |
|-------|---------|---------|
| **UI** | SheetAtlas.UI.Avalonia | Avalonia views, ViewModels, Managers |
| **Core** | SheetAtlas.Core | Business logic, domain entities, services |
| **Infrastructure** | SheetAtlas.Infrastructure | File readers/writers, external integrations |
| **Logging** | SheetAtlas.Logging | Cross-cutting logging abstraction |

**Dependency rule**: UI → Core ← Infrastructure. Core has no knowledge of UI or Infrastructure.

## Key Decisions

- [ADR-001: Error Handling Philosophy](reference/decisions/001-error-handling-philosophy.md)
- [ADR-002: Row Indexing Semantics](reference/decisions/002-row-indexing-semantics.md)
- [ADR-003: Technology Stack](reference/decisions/003-technology-stack.md)
- [ADR-004: Foundation Layer First](reference/decisions/004-foundation-layer-first.md)
- [ADR-005: Security First](reference/decisions/005-security-first.md)
- [ADR-006: Git Workflow](reference/decisions/006-git-workflow.md)
- [ADR-007: Unified Data Flow For Export](reference/decisions/007-unified-data-flow-for-export.md)
- [ADR-008: Facade Pattern For Dependency Injection](reference/decisions/008-facade-pattern-for-dependency-injection.md)

## Project Tree

> Auto-generated from `/// <summary>` comments in source files.
> Run `.development/scripts/generate-architecture.sh` to update.


### SheetAtlas.Core/Application/DTOs
- `ColumnAnalysisResult.cs` — Result of analyzing column characteristics. Extends ColumnMetadata record with detected information.
- `ColumnValidationResult.cs` — Validation result for a single column. Combines expected column definition with actual column analysis.
- `CsvReaderOptions.cs` — Configuration options for CSV file reading
- `ErrorSummary.cs` — Pre-calculated aggregations for UI performance
- `ExportResult.cs` — Result of an export operation. Follows Result pattern - check IsSuccess before using output path.
- `FileInfoDto.cs` — Information about the Excel file being logged
- `FileLoadResult.cs`
- `FileLogEntry.cs` — Root object for structured file log JSON Represents a single load attempt for an Excel file
- `LoadAttemptInfo.cs` — Information about the file load attempt
- `MergeComplexityAnalysis.cs` — Analysis of merged cell complexity in sheet. Used to recommend strategy and warn user.
- `MergeWarning.cs` — Warning generated during merged cell resolution.
- `NormalizationResult.cs` — Result of normalizing single cell value. Preserves original + cleaned value + quality issues. Follows Result pattern (no exceptions for business errors).
- `UserSettings.cs` — User preferences with persistent storage. All properties have sensible defaults - app works without configuration.
- `ValidationIssue.cs` — Represents a single validation issue found during template validation. Includes location, severity, and contextual information.
- `ValidationReport.cs` — Complete validation report for an Excel file against a template. Contains all validation results, issues, and summary statistics.

### SheetAtlas.Core/Application/Interfaces
- `ICellReferenceParser.cs`
- `ICellValueReader.cs` — Service responsible for reading and parsing cell values from Excel worksheets. Handles different cell data types (shared strings, booleans, numbers, dates). Preserves type information by returning SAC... ⚠️
- `IColumnAnalysisService.cs` — Analyzes column characteristics: data type, confidence, generates ColumnMetadata. Enhances existing ColumnMetadata record with detected type and quality metrics.
- `IComparisonExportService.cs` — Service for exporting row comparison results to various formats. Includes metadata (search terms, files, timestamps) for context.
- `ICurrencyDetector.cs` — Extracts currency information from Excel number format strings. Used during file load to enhance ColumnMetadata with currency awareness.
- `IDataNormalizationService.cs` — Normalizes cell values: dates, numbers, text, booleans. Populates OriginalValue and CleanedValue in CellMetadata. Core to search accuracy (+40% improvement).
- `IExcelWriterService.cs` — Service for exporting enriched sheet data to various formats. Uses CleanedValue from cell metadata to write typed cells.
- `IExceptionHandler.cs` — Centralized exception handling service. Converts exceptions to user-friendly error messages and logs technical details.
- `IFileFormatReader.cs` — Reader for specific Excel file formats
- `IFileLogService.cs` — Service for managing structured file logging Provides read/write operations for Excel file error logs in JSON format
- `IHeaderGroupingService.cs` — Groups headers by semantic name, merging columns that map to the same name.
- `IHeaderResolver.cs` — Resolves semantic names for column headers. Provides unified interface for different resolution sources (ColumnLink, Template, Dictionary).
- `IMergedCellResolver.cs` — Resolves merged cells using configurable strategies. Handles horizontal/vertical merges, warns on complex patterns.
- `IMergedRangeExtractor.cs` — Generic interface for extracting merged cell range information from various file formats. Each format (OpenXML, ODF, etc.) provides its own context type.
- `IRowComparisonService.cs`
- `ISettingsService.cs` — Service for managing user preferences with persistent storage. Settings are stored as JSON in the user's application data folder.
- `ISheetAnalysisOrchestrator.cs` — Orchestrates the analysis and enrichment pipeline for sheet data. Coordinates foundation services to analyze columns, resolve merged cells, and populate metadata.
- `ITemplateRepository.cs` — Repository for managing Excel templates (CRUD operations). Templates are stored as JSON files in a user-configurable location.
- `ITemplateValidationService.cs` — Service for validating Excel files against templates and creating templates from files. Core service for the Template Management feature.

### SheetAtlas.Core/Application/Json
- `AppJsonContext.cs` — JSON serialization context for source-generated serializers. Required for PublishTrimmed=true support (AOT and trimming). All types used with JsonSerializer must be registered here.

### SheetAtlas.Core/Application/Services
- `CellReferenceParser.cs`
- `CellValueReader.cs` — Reads and parses cell values from Excel worksheets with type preservation. Handles different cell data types: shared strings, booleans, numbers, dates. Returns CellValue struct with native types (doub... ⚠️
- `ColumnLinkingService.cs` — Input for column linking: column info from a loaded file.
- `ExcelErrorJsonConverter.cs` — Custom JSON converter for ExcelError Handles serialization of Exception property by extracting only serializable info
- `ExceptionHandler.cs` — Centralized exception handling implementation. Converts technical exceptions to user-friendly messages and logs details.
- `FileLogService.cs` — Manages structured logging of Excel file load attempts to JSON files Each Excel file gets its own folder with chronological JSON logs
- `HeaderGroupingService.cs` — Groups headers by semantic name, merging columns that map to the same name. Consolidates header grouping logic previously duplicated across ComparisonExportService and RowComparisonViewModel.
- `RowComparisonService.cs`
- `SearchService.cs` — Service for searching within Excel files across sheets and cells.
- `SettingsService.cs` — Manages user preferences with persistent JSON storage. Settings are stored in %AppData%/SheetAtlas/settings.json.
- `SheetAnalysisOrchestrator.cs` — Orchestrates the analysis and enrichment pipeline for sheet data. Coordinates foundation services: merged cell resolution, column analysis, currency detection, data normalization.

### SheetAtlas.Core/Application/Services/Foundation
- `ColumnAnalysisService.cs` — Analyzes column characteristics: data type, confidence, generates ColumnMetadata. Implements IColumnAnalysisService interface.
- `CurrencyDetector.cs` — Extracts currency information from Excel number format strings. Implements ICurrencyDetector interface.
- `DataNormalizationService.cs` — Normalizes cell values: dates, numbers, text, booleans. Implements IDataNormalizationService interface.
- `MergedCellResolver.cs` — Resolves merged cells using configurable strategies. Implements IMergedCellResolver interface.
- `TemplateRepository.cs` — File-based repository for managing Excel templates. Stores templates as JSON files in a configurable directory.
- `TemplateValidationService.cs` — Service for validating Excel files against templates and creating templates from files. Uses IColumnAnalysisService for type detection and validation.

### SheetAtlas.Core/Application/Services/HeaderResolvers
- `DictionaryHeaderResolver.cs` — Resolves semantic names from a dictionary. Used by export services that receive semantic name mappings as parameters.
- `FunctionHeaderResolver.cs` — Resolves semantic names using a function delegate. Used by ViewModels with injected resolver function.
- `NullHeaderResolver.cs` — No-op resolver that returns null for all headers. Used when no semantic name mapping is needed (identity mapping).

### SheetAtlas.Core/Application/Utilities
- `NumberFormatHelper.cs` — Utility class for detecting number format patterns in Excel number format strings. Used by ColumnAnalysisService and DataNormalizationService for type detection.

### SheetAtlas.Core/Configuration
- `AppSettings.cs` — Application-wide configuration settings

### SheetAtlas.Core/Domain/Entities
- `ExcelFile.cs` — Represents a loaded Excel file with its sheets, load status, and any errors encountered. Implements IDisposable to properly release memory used by sheet data.
- `ExcelTemplate.cs` — Represents an Excel file template that defines expected structure and validation rules. Used to validate incoming files against a known-good template. Supports JSON serialization for persistence and s... ⚠️
- `RowComparison.cs` — Represents a complete row from an Excel sheet for comparison purposes
- `RowComparisonWarning.cs` — Represents a warning about column structure inconsistencies during row comparison
- `SASheetData.cs` — Efficient sheet storage with flat array architecture. Uses single contiguous SACellData[] instead of List of arrays. Benefits: zero fragmentation, cache-friendly, GC can release memory properly. Memor... ⚠️
- `SearchResult.cs` — Configuration options for search operations.

### SheetAtlas.Core/Domain/Exceptions
- `ComparisonException.cs` — Thrown when file comparison operations fail due to incompatible files. Represents business rule violations specific to comparison logic.
- `SheetAtlasException.cs` — Base exception for all Excel Viewer domain exceptions. Represents business rule violations and domain-specific errors.

### SheetAtlas.Core/Domain/ValueObjects
- `CellAnomaly.cs` — Represents an anomaly detected in a cell during column analysis. Used by ColumnAnalysisService to report data quality issues with context.
- `ColumnLink.cs` — Links multiple column names to a single semantic concept. Used for grouping semantically equivalent columns across files.
- `CurrencyInfo.cs` — Immutable currency information extracted from Excel number format. Used for currency-aware comparison and normalization.
- `DataRegion.cs` — Defines a data region within an Excel sheet. Supports both auto-detection and manual user selection (future UI).
- `DataType.cs`
- `DateSystem.cs`
- `ExcelError.cs` — Represents an error, warning, or informational message encountered during Excel file processing. Immutable value object with factory methods for different error types.
- `ExpectedColumn.cs` — Defines an expected column in an Excel template. Captures column requirements: name, position, type, and validation rules. Immutable value object with factory methods for common patterns.
- `ExportCellValue.cs`
- `LinkedColumn.cs` — Represents a column from a specific source file/sheet. Used to track the origin of columns in a ColumnLink.
- `MergeStrategy.cs`
- `RuleType.cs`
- `SACellData.cs` — Optional cell metadata for validation, data cleaning, formulas, and styles. Created on-demand (~5-10% of cells), not allocated for clean simple cells.
- `SACellValue.cs`
- `StringPool.cs` — String interning pool for deduplicating repeated string values. Reduces memory footprint when many cells contain identical strings (categories, enums, etc.). Thread-safe for concurrent reads, requires... ⚠️
- `ValidationRule.cs` — A validation rule that can be applied to a column in an Excel template. Immutable value object with factory methods for common rules.

### SheetAtlas.Core/Shared/Helpers
- `FilePathHelper.cs`
- `RowIndexConverter.cs` — Row indexing converter for SheetAtlas.  TWO INDEXING SYSTEMS: - Excel: 1-based (Row 1 = first row visible in Excel, typically header) - Absolute: 0-based (Row 0 = first row in sheet, SASheetData uses ... ⚠️

### SheetAtlas.Infrastructure/External
- `ExcelReaderService.cs` — Service for loading Excel files using format-specific readers

### SheetAtlas.Infrastructure/External/Readers
- `CsvFileReader.cs` — Reader for CSV (Comma-Separated Values) files
- `FileReaderContext.cs` — Facade that groups common dependencies for all file format readers. Reduces constructor parameter count and centralizes shared service access.
- `NumberFormatInferenceService.cs` — Service for inferring Excel NumberFormat from CSV text values. Handles percentages, scientific notation, and decimal precision detection.
- `OpenXmlFileReader.cs` — Reader for OpenXML Excel formats (.xlsx, .xlsm, .xltx, .xltm)
- `OpenXmlMergedRangeExtractor.cs` — Extracts merged cell range information from OpenXML (.xlsx) worksheets. Implements generic IMergedRangeExtractor for WorksheetPart context.
- `XlsFileReader.cs` — Reader for legacy Excel binary formats (.xls, .xlt)

### SheetAtlas.Infrastructure/External/Writers
- `ComparisonExportService.cs` — Service for exporting row comparison results to Excel and CSV formats. Excel exports include metadata sheet with search terms, files, and timestamp.
- `ExcelWriterService.cs` — Service for exporting enriched sheet data to Excel and CSV formats. Uses CleanedValue from cell metadata for proper type preservation. Preserves number formats (currency, percentage, dates) from sourc... ⚠️

### SheetAtlas.Logging/Models
- `LogAction.cs` — Represents an action that can be performed on a notification
- `LogMessage.cs` — Represents a user notification (error, warning, info)
- `LogSeverity.cs`

### SheetAtlas.Logging/Services
- `ILogService.cs` — Service for managing application log messages
- `LogService.cs` — In-memory and file-based implementation of log service Manages log message storage, file persistence, and events
- `LogServiceExtensions.cs` — Extension methods for ILogService to simplify logging calls

### SheetAtlas.UI.Avalonia
- `App.axaml.cs`
- `Program.cs`

### SheetAtlas.UI.Avalonia/Commands
- `RelayCommand.cs` — RelayCommand with built-in error handling to prevent unhandled exceptions from crashing the app. Provides a global safety net for all command executions.

### SheetAtlas.UI.Avalonia/Controls
- `CollapsibleSection.axaml.cs` — A reusable collapsible section control with customizable header and content. Provides consistent styling across SearchView and ComparisonView.
- `MultiSidebar.axaml.cs` — A VSCode-style multi-sidebar control with icon bar and collapsible panels.

### SheetAtlas.UI.Avalonia/Converters
- `BoolToTextConverter.cs`
- `CollectionNotEmptyConverter.cs` — Converts a collection to a boolean indicating if it's not empty
- `ComparisonTypeToBackgroundConverter.cs` — Converts ComparisonType or CellComparisonResult to appropriate background brush for visual distinction Supports gradient coloring based on frequency intensity for different values
- `EnumEqualsConverter.cs`
- `GreaterThanZeroConverter.cs` — Converts a nullable int to bool (true if value > 0). Used for badge visibility on sidebar icons.
- `LogSeverityToColorConverter.cs`

### SheetAtlas.UI.Avalonia/Managers/Comparison
- `IRowComparisonCoordinator.cs` — Manages the lifecycle of row comparison ViewModels. Handles creation, selection, and removal of row comparisons.
- `RowComparisonCoordinator.cs` — Coordinates the lifecycle of row comparison ViewModels. Manages creation, selection, and removal of comparisons.

### SheetAtlas.UI.Avalonia/Managers/FileDetails
- `FileDetailsCoordinator.cs` — Coordinates file detail operations such as file removal, retry, and cleanup. Orchestrates interactions between FilesManager, SearchViewModels, and ComparisonCoordinator.
- `IFileDetailsCoordinator.cs` — Coordinates file detail operations such as file removal, retry, and cleanup. Handles the orchestration between FilesManager, SearchViewModels, and ComparisonCoordinator when file-related actions are r... ⚠️

### SheetAtlas.UI.Avalonia/Managers/Files
- `ILoadedFilesManager.cs` — Manages the collection of loaded Excel files and their lifecycle. Handles loading, removal, and retry operations for failed loads.
- `LoadedFilesManager.cs` — Manages the collection of loaded Excel files and their lifecycle. Handles loading, removal, and retry operations for failed loads.

### SheetAtlas.UI.Avalonia/Managers/Navigation
- `ITabNavigationCoordinator.cs` — Coordinates tab visibility and navigation in the main window. Handles showing, hiding, and switching between different tabs (FileDetails, Search, Comparison).
- `TabNavigationCoordinator.cs` — Coordinates tab visibility and navigation in the main window. Manages showing, hiding, and switching between different tabs.

### SheetAtlas.UI.Avalonia/Managers/Search
- `ISearchResultsManager.cs` — Manager for search operations and results
- `SearchResultsManager.cs`

### SheetAtlas.UI.Avalonia/Managers/Selection
- `ISelectionManager.cs` — Manager for selection and visibility operations
- `SelectionManager.cs`

### SheetAtlas.UI.Avalonia/Managers/Theme
- `IThemeManager.cs`
- `ThemeManager.cs`

### SheetAtlas.UI.Avalonia/Models
- `CellComparisonResult.cs` — Represents the result of comparing a cell value with other values in the same column
- `ComparisonType.cs`
- `FileDetailAction.cs`
- `FileDetailProperty.cs`
- `IToggleable.cs` — Interface for items that can be toggled (expanded/collapsed or selected/deselected)
- `SidebarItem.cs` — Represents a sidebar item in the MultiSidebar control. Each item has an icon, tooltip, content template, and open/close state.

### SheetAtlas.UI.Avalonia/Models/Search
- `CellOccurrenceImpl.cs`
- `FileOccurrenceImpl.cs`
- `GroupedSearchResultImpl.cs`
- `ICellOccurrence.cs` — Represents a cell occurrence in search results
- `IFileOccurrence.cs` — Represents a file occurrence in search results
- `IGroupedSearchResult.cs` — Represents a group of search results with the same value
- `ISearchResultFactory.cs` — Factory for creating search result models
- `ISheetOccurrence.cs` — Represents a sheet occurrence in search results
- `SearchResultFactory.cs`
- `SheetOccurrenceImpl.cs`

### SheetAtlas.UI.Avalonia/Services
- `AvaloniaDialogService.cs`
- `AvaloniaFilePickerService.cs`
- `ErrorNotificationService.cs` — UI-layer service for displaying errors to users. Bridges exception handling with dialog presentation.
- `IActivityLogService.cs` — Service for logging application activities and operations. Maintains a timeline of events that can be displayed to the user.
- `IDialogService.cs`
- `IFilePickerService.cs`

### SheetAtlas.UI.Avalonia/ViewModels
- `ColumnLinkingViewModel.cs` — ViewModel item for displaying a ColumnLink in the sidebar.
- `ErrorLogRowViewModel.cs` — ViewModel for a single row in the error log table (flat list)
- `FileActionEventArgs.cs` — Event arguments for file-related actions requested from FileDetailsViewModel. Used for actions like Remove, Clean, Retry, etc.
- `FileDetailsViewModel.cs` — ViewModel for file details display. Shows basic file information, notifications/errors, and export functionality. Template management has been moved to TemplateManagementViewModel.
- `FileLoadResultViewModel.cs`
- `FileResultGroup.cs`
- `IFileLoadResultViewModel.cs`
- `MainWindowViewModel.Commands.cs`
- `MainWindowViewModel.cs`
- `MainWindowViewModel.EventHandlers.cs`
- `MainWindowViewModel.FileOperations.cs`
- `MainWindowViewModel.HelpCommands.cs`
- `RowComparisonViewModel.cs` — Builds a mapping from original column names to their semantic names for export.
- `SearchHistoryItem.cs`
- `SearchResultItem.cs`
- `SearchViewModel.cs`
- `SettingsViewModel.cs` — ViewModel for the Settings tab. Manages user preferences with Save/Reset/Cancel operations.
- `SheetResultGroup.cs`
- `TemplateManagementViewModel.cs` — ViewModel for the Templates tab. Manages the template library and template operations. Supports single file (create/validate) and multi-file (batch validate/apply) operations.
- `TreeSearchResultsViewModel.cs`
- `ValidationIssueViewModel.cs` — ViewModel for displaying validation issues in the UI.
- `ViewModelBase.cs`

### SheetAtlas.UI.Avalonia/Views
- `ColumnsSidebarView.axaml.cs`
- `FileDetailsView.axaml.cs`
- `FileLoadResultView.axaml.cs`
- `FilesSidebarView.axaml.cs`
- `MainWindow.axaml.cs`
- `RowComparisonView.axaml.cs`
- `SettingsView.axaml.cs`
- `TemplateManagementView.axaml.cs`
- `TreeSearchResultsView.axaml.cs`

---

*Auto-generated from source code comments by `.development/scripts/generate-architecture.sh`*
