# SheetAtlas Codebase: Refactoring and Abstraction Analysis
**Date**: 2026-01-30
**Context**: Pre-release v0.5.3 architectural review
**Scope**: Recent features analysis (export with semantic names, column grouping, column filtering)

---

## Executive Summary

Analysis of the SheetAtlas codebase identified **9 major refactoring opportunities** across 3 priority levels. The codebase shows solid architecture but recent feature additions (export with semantic names, column grouping) have introduced pattern duplication and abstraction gaps.

**Key findings**:
- **3 HIGH priority** items with immediate impact potential
- **4 MEDIUM priority** architectural improvements
- **2 LOW priority** nice-to-have enhancements
- **Pattern duplication**: Header grouping logic appears 3+ times
- **Service layer**: Some services exceed 600 lines, mixing concerns
- **Missing abstractions**: Export pipeline, cell formatting, enrichment pipeline

**Recommendation**: Address HIGH and selected MEDIUM priority items before v0.5.3 release to improve maintainability and extensibility.

---

## 1. HIGH PRIORITY REFACTORING OPPORTUNITIES

### 1.1 ComparisonExportService: Extract Formatting Helpers into Reusable Layer

**File**: `src/SheetAtlas.Infrastructure/External/Writers/ComparisonExportService.cs` (615 lines)

**Issue**: Contains 12 private static helper methods scattered throughout:
- Cell creation (`CreateTextCell`)
- Column reference calculation (`GetColumnReference`)
- CSV escaping (`EscapeCsvField`)
- Filename generation (`SanitizeForFilename`)
- Header grouping (`GroupHeadersBySemanticName`)

**Problem**:
- **Duplicated logic**: Same formatting logic needed by other exporters (JSON, XML)
- **Not reusable**: Logic is trapped in ComparisonExportService
- **Violated SRP**: Service combines export orchestration + cell formatting + CSV escaping + filename generation

**Proposed Solution**:

Extract into separate, focused classes:

```
SheetAtlas.Core/Application/Services/Export/
├── ExportCellFormatter.cs        (CreateTextCell, CreateTypedCell)
├── ExportFileNameGenerator.cs    (GenerateFilename, SanitizeForFilename)
├── CsvFormattingHelper.cs        (EscapeCsvField)
├── ExcelCellBuilder.cs           (GetColumnReference, GetOrCreateCellFormatIndex)
└── HeaderGroupingService.cs      (HeaderGroup record, GroupHeadersBySemanticName)
```

**Benefits**:
- Other exporters (JSON, Parquet) can reuse `ExportFileNameGenerator`, `ExportCellFormatter`
- Unit testing becomes granular (test CSV escaping independently)
- Reduces ComparisonExportService to ~350 lines, focused on orchestration
- Enables localization of format strings

**Impact**: **High** - enables future export formats with 40% less code duplication

**Effort**: Medium
**Risk**: Low (pure extraction, no behavior change)

---

### 1.2 RowComparisonViewModel: Excessive Service Configuration (SetExportServices antipattern)

**File**: `src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs` (410 lines)

**Issue**: Lines 122-134 implement Service Locator antipattern:

```csharp
public void SetExportServices(
    IComparisonExportService exportService,
    IFilePickerService filePickerService,
    ISettingsService settingsService)
{
    _exportService = exportService;
    _filePickerService = filePickerService;
    _settingsService = settingsService;
    OnPropertyChanged(nameof(CanExport));
}
```

**Problems**:
- VM has 3 private export service fields set AFTER construction
- State is partially initialized in constructor, completed later
- `CanExport` property depends on null checks (nullable fields)
- Makes constructor dependencies unclear
- Tests must call `SetExportServices` before commands work

**Proposed Solution**:

Create dedicated `IRowComparisonExportCoordinator` service:

```csharp
public interface IRowComparisonExportCoordinator
{
    Task<ExportResult> ExportComparisonAsync(
        RowComparison comparison,
        IEnumerable<string>? includedColumns,
        IReadOnlyDictionary<string, string>? semanticNames,
        ExportFormat format,
        CancellationToken ct);
}
```

**Benefits**:
- Constructor is the complete contract (no hidden SetXyz methods)
- `CanExport` is simply `_exportCoordinator != null` (no nullable checks)
- Export logic is testable independently
- Easier to mock in unit tests
- Lines reduced from 410 → 280

**Impact**: **Medium-High** - improves testability and API clarity

**Effort**: Medium
**Risk**: Low (internal refactoring, no external API change)

---

### 1.3 ColumnLinkingViewModel: Dual Responsibility (UI + Business Logic)

**File**: `src/SheetAtlas.UI.Avalonia/ViewModels/ColumnLinkingViewModel.cs` (580 lines)

**Issue**: Mixes multiple responsibilities:
1. Managing ColumnLink UI state (expansion, editing, highlighting)
2. Performing semantic column operations (merge, ungroup, filter)
3. Template matching logic
4. File event subscription/cleanup

**Problem**:
- Lines 238-298 repeat merge/ungroup with similar edit/cleanup code
- Can't test merge logic without Avalonia UI infrastructure
- Business logic trapped in UI layer

**Proposed Solution**:

Create `IColumnLinkingStateManager` (business logic):

```csharp
public interface IColumnLinkingStateManager
{
    IReadOnlyList<ColumnLink> GetCurrentState();
    void MergeColumns(ColumnLink target, ColumnLink source);
    void UngroupColumn(ColumnLink column);
    void ApplyHighlighting(ExcelTemplate? template);
    void SetIncludedColumns(IEnumerable<string> columns);
}
```

ViewModel becomes thin UI adapter.

**Benefits**:
- Business logic testable without Avalonia
- UI updates become predictable (always sync from state)
- Merge/Ungroup logic reusable by other UIs (CLI, batch, API)
- ViewModel drops to ~250 lines
- StateManager is ~200 lines of pure, testable logic

**Impact**: **High** - enables non-UI usage, improves testability

**Effort**: Medium
**Risk**: Medium (requires careful state synchronization)

---

## 2. PATTERN DUPLICATION (Quick Wins)

### 2.1 Header Grouping Logic Appears 3+ Times

**Locations**:
1. `ComparisonExportService.cs` line 388: `GroupHeadersBySemanticName()` (38 lines)
2. `RowComparisonViewModel.cs` line 320: Header grouping (25 lines)
3. `ColumnLinkingViewModel.cs` line 373: `GetIncludedColumns()` (similar pattern)

**Problem**:
All three implementations do the same thing:
- Group columns by semantic name
- Filter by included columns
- Create display names
- Return grouped structure

But logic is duplicated and slightly inconsistent.

**Proposed Solution**:

Create `IHeaderGroupingService`:

```csharp
namespace SheetAtlas.Core.Application.Services
{
    public interface IHeaderGroupingService
    {
        IReadOnlyList<HeaderGroup> GroupHeaders(
            IEnumerable<string> headers,
            IEnumerable<string>? includedColumns = null,
            IReadOnlyDictionary<string, string>? semanticNames = null);
    }
}
```

**Locations to update**:
1. `ComparisonExportService` - inject and use
2. `RowComparisonViewModel` - inject and use
3. `ColumnLinkingViewModel` - use existing semantic name mapping

**Benefits**:
- Single source of truth for grouping logic
- Consistent behavior across features
- 60 lines of code removed from three files
- Easier to extend (add weighted grouping, custom sort, etc.)

**Impact**: **Medium** - improves consistency and maintainability

**Effort**: Low
**Risk**: Low (pure logic extraction)

---

### 2.2 File/Column Filtering Pattern Repeated

**Locations**:
1. `SearchService.cs` lines 85-89: Column filter HashSet creation
2. `ColumnLinkingViewModel.cs` line 407: Same pattern
3. `ComparisonExportService.cs` lines 393-395: Same pattern

**Problem**:
```csharp
// Repeated 3+ times with minor variations
HashSet<string>? includedSet = includedColumns != null
    ? new HashSet<string>(includedColumns, StringComparer.OrdinalIgnoreCase)
    : null;

if (includedSet != null && !includedSet.Contains(item))
    continue;
```

**Proposed Solution**:

Create helper in utilities:

```csharp
namespace SheetAtlas.Core.Application.Utilities
{
    public static class FilteringHelper
    {
        public static HashSet<string>? CreateIncludedSet(
            IEnumerable<string>? items,
            StringComparer? comparer = null);

        public static bool IsIncluded(
            string? item,
            IEnumerable<string>? includedSet,
            StringComparer? comparer = null);
    }
}
```

**Benefits**: Minor code savings but improves consistency

**Impact**: **Low**
**Effort**: Low
**Risk**: Very Low

---

## 3. ABSTRACTION GAPS & MISSING INTERFACES

### 3.1 Export Pipeline Lacks Abstraction (Forces Single Format)

**Current State**:
- `IComparisonExportService` hardcoded to Excel + CSV only
- Styling, formatting embedded in ComparisonExportService
- Adding new format (JSON, Parquet) requires modifying service + copying formatting code

**Proposed Solution**:

Create export pipeline abstraction:

```csharp
namespace SheetAtlas.Core.Application.Interfaces
{
    public interface IComparisonExportFormat
    {
        string FileExtension { get; }
        Task<ExportResult> ExportAsync(
            RowComparison comparison,
            string outputPath,
            ExportOptions options,
            CancellationToken ct);
    }

    public interface IComparisonExportPipeline
    {
        Task<ExportResult> ExportAsync(
            RowComparison comparison,
            string outputFormat,
            string outputPath,
            ExportOptions options,
            CancellationToken ct);
    }
}
```

Implementations:
```
SheetAtlas.Infrastructure/External/Writers/
├── ExcelComparisonExporter.cs   (IComparisonExportFormat)
├── CsvComparisonExporter.cs     (IComparisonExportFormat)
├── JsonComparisonExporter.cs    (IComparisonExportFormat - future)
└── ComparisonExportPipeline.cs  (IComparisonExportPipeline)
```

**Benefits**:
- New formats added without touching existing code
- Each exporter < 200 lines, focused
- Shared formatting layer (NumberFormatHelper, CellFormatter)
- Testable: mock format, test pipeline

**Impact**: **Medium** - improves extensibility

**Effort**: Medium
**Risk**: Medium (architectural change)

---

### 3.2 Cell Formatting Logic Scattered Across Domain and Services

**Current State**:
- `ExcelRow.FormatCellValueForDisplay()` - 97 lines in domain entity
- `NumberFormatHelper.IsDateFormat()` - duplicate logic in utilities
- Excel date conversion scattered in export service + display logic
- No unified formatting contract

**Problem**:
- Format detection happens 3+ times
- Display formatting + export formatting diverge
- Date format conversion duplicated

**Proposed Solution**:

Create `ICellValueFormatter` abstraction:

```csharp
namespace SheetAtlas.Core.Application.Interfaces
{
    public interface ICellValueFormatter
    {
        string FormatForDisplay(ExportCellValue cell, DisplayFormattingOptions? options = null);
        string FormatForExport(ExportCellValue cell, ExportFormattingOptions? options = null);
        CellFormatCategory DetectFormatCategory(string? numberFormat);
    }
}
```

Consolidates:
- `ExcelRow.FormatCellValueForDisplay()` → `ICellValueFormatter.FormatForDisplay()`
- `NumberFormatHelper` logic → `ICellValueFormatter.DetectFormatCategory()`
- Excel date/number conversion → unified in formatter

**Impact**: **Medium** - reduces duplication, improves maintainability

**Effort**: Medium
**Risk**: Medium (touches display + export logic)

---

## 4. DOMAIN MODELING ISSUES

### 4.1 SearchResult Lacks Proper Abstraction for Different Match Types

**File**: `src/SheetAtlas.Core/Domain/Entities/SearchResult.cs`

**Problem**:
```csharp
public class SearchResult
{
    public int Row { get; }          // -1 if filename/sheet match
    public int Column { get; }       // -1 if filename/sheet match
    public string MatchText { get; } // what was matched
    public Dictionary<string, object> Context { get; } // untyped, magic strings
}
```

Three different result types squeezed into one class:
1. **Cell match**: Row >= 0, Column >= 0, MatchText = cell value
2. **Sheet match**: Row = -1, Column = -1, MatchText = sheet name, Context["Type"] = "SheetName"
3. **Filename match**: Row = -1, Column = -1, MatchText = filename, Context["Type"] = "FileName"

**Issues**:
- Callers must check Row/Column values to determine type
- Context is untyped (magic strings, no Intellisense)
- Can't distinguish between empty sheet and no result
- Pattern matching unavailable

**Proposed Solution**:

Replace with sealed record hierarchy:

```csharp
public abstract record SearchResult
{
    public string MatchText { get; init; }
    public ExcelFile SourceFile { get; init; }

    public sealed record CellMatch(
        ExcelFile SourceFile,
        string SheetName,
        int Row,
        int Column,
        string MatchText,
        string ColumnHeader) : SearchResult;

    public sealed record SheetMatch(
        ExcelFile SourceFile,
        string SheetName,
        string MatchText) : SearchResult;

    public sealed record FileMatch(
        ExcelFile SourceFile,
        string MatchText) : SearchResult;
}
```

**Benefits**:
- Type-safe, no magic strings
- Pattern matching in LINQ
- No defensive null checks needed
- Easier to add new result types

**Impact**: **Medium-High** - improves type safety, enables better patterns

**Effort**: Medium
**Risk**: Medium (breaking change, requires updating callers)

---

### 4.2 ExportResult Too Minimal, Hides Export Metadata

**File**: `src/SheetAtlas.Core/Application/DTOs/ExportResult.cs` (77 lines)

**Problem**:
```csharp
public record ExportResult
{
    public bool IsSuccess { get; init; }
    public string? OutputPath { get; init; }
    public int RowsExported { get; init; }
    public int ColumnsExported { get; init; }
    public int NormalizedCellCount { get; init; } // Why? Not exported
    public long FileSizeBytes { get; init; }
    public TimeSpan Duration { get; init; }
}
```

**Issues**:
- `NormalizedCellCount` makes no sense for export result (not exported info)
- Can't distinguish what was merged/grouped during export
- No info about semantic name mappings applied
- Missing metrics: header groups, deduplicated columns

**Proposed Enhancement**:

```csharp
public record ExportResult
{
    public bool IsSuccess { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }

    // Core metrics
    public int RowsExported { get; init; }
    public int ColumnsExported { get; init; }
    public int SemanticGroupsApplied { get; init; }    // NEW
    public long FileSizeBytes { get; init; }
    public TimeSpan Duration { get; init; }

    // Metadata about what was exported
    public ExportMetadata? Metadata { get; init; }
}

public record ExportMetadata
{
    public int OriginalColumnCount { get; init; }
    public int GroupedColumnCount { get; init; }
    public int IncludedColumnCount { get; init; }
    public IReadOnlyList<string> AppliedSemanticNames { get; init; }
    public IReadOnlyList<string> ExcludedColumns { get; init; }
}
```

**Benefits**:
- Rich, typed feedback about export
- Can report "Exported 50 rows, 12 columns (merged from 18 original)"
- Metrics for user feedback and debugging

**Impact**: **Low** - nice-to-have enhancement

**Effort**: Low
**Risk**: Low (additive change)

---

## 5. SERVICE LAYER ISSUES

### 5.1 SheetAnalysisOrchestrator: Single Service vs. Pipeline

**File**: `src/SheetAtlas.Core/Application/Services/SheetAnalysisOrchestrator.cs` (317 lines)

**Problem**:
- Orchestrates 3 foundation services sequentially
- Mixes concerns: merged cell resolution + column analysis + normalization + error handling
- `EnrichAsync` is fire-and-forget, actual work is synchronous (misleading)
- Error accumulation pattern (passing List<ExcelError>) is awkward
- Callback pattern for merge warnings is fragile

**Current Flow**:
```
EnrichAsync(SASheetData rawData, List<ExcelError> errors)
  ├─ ResolveMergedCells (sync)
  │  ├─ _mergedCellResolver.AnalyzeMergeComplexity()
  │  ├─ _mergedCellResolver.ResolveMergedCells()
  │  └─ HandleMergeWarning callback
  │
  └─ EnrichSheetWithColumnAnalysis (sync)
     ├─ _columnAnalysisService.AnalyzeColumn()
     ├─ NormalizeCellValue() per cell
     └─ AddAnomaliesToErrors()
```

**Issues**:
1. Callback pattern fragile: `warning => HandleMergeWarning()` is callback hell waiting
2. Error accumulation awkward: Passing mutable List<> through pipeline
3. No result composition: Can't easily add validation pipeline step
4. Synchronous lying: `async Task` with no actual await

**Proposed Solution**:

Create `IEnrichmentPipeline`:

```csharp
public record EnrichmentResult(
    SASheetData EnrichedData,
    IReadOnlyList<ExcelError> Errors);

public interface IEnrichmentStep
{
    Task<EnrichmentResult> ExecuteAsync(
        SASheetData data,
        EnrichmentResult previous,
        CancellationToken ct);
}

public interface ISheetEnrichmentPipeline
{
    Task<EnrichmentResult> EnrichAsync(SASheetData rawData, CancellationToken ct);
}
```

Implementations:
- `MergedCellResolutionStep`
- `ColumnAnalysisStep`
- `DataValidationStep` (future)
- `SheetEnrichmentPipeline`

**Benefits**:
- Pipeline is composable (add validation, metadata steps easily)
- Each step is independently testable
- Error handling is unified (accumulate in result, not callback)
- Easy to log/debug each stage
- Actual async-ready architecture

**Impact**: **Medium** - improves extensibility and testability

**Effort**: High
**Risk**: Medium (architectural change)

---

## 6. UI/APPLICATION LAYER ISSUES

### 6.1 RowComparisonViewModel Contains Business Logic (Header Grouping, Cell Comparison)

**File**: `src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs`

**Lines 320-377**: Duplicate of ComparisonExportService grouping logic

**Problem**:
- Duplicate column header mapping
- No separation between display logic and business logic
- Business logic trapped in UI layer

**Proposed Solution**:

Extract to `IRowComparisonPresenter`:

```csharp
public interface IRowComparisonPresenter
{
    RowComparisonPresentationModel PrepareForDisplay(
        RowComparison comparison,
        IEnumerable<string>? includedColumns,
        Func<string, string?>? semanticNameResolver);
}

public record RowComparisonPresentationModel(
    IReadOnlyList<ColumnPresentationModel> Columns,
    int RowCount,
    IReadOnlyList<RowComparisonWarning> Warnings);
```

ViewModel becomes UI adapter.

**Benefits**:
- Business logic (header grouping) is testable without Avalonia
- ViewModel becomes pure UI adapter
- Presentation logic reusable by other UIs
- Easier to reason about

**Impact**: **Medium-High** - improves testability and reusability

**Effort**: Medium
**Risk**: Low (internal refactoring)

---

## 7. RISK AREAS FOR RELEASE

### 7.1 Export Path: No Validation Before Writing

**Risk**: `ComparisonExportService.cs` lines 46-68 create file without validating:
- Disk space
- Write permissions
- Path exists
- Disk full during write

**Recommended Mitigation**:
```csharp
private async Task ValidateExportPath(string outputPath)
{
    var dirInfo = new DirectoryInfo(Path.GetDirectoryName(outputPath));
    if (!dirInfo.Exists)
        throw new ArgumentException($"Directory does not exist: {dirInfo.FullName}");

    var driveInfo = new DriveInfo(dirInfo.Root.Name);
    if (driveInfo.AvailableFreeSpace < 100 * 1024 * 1024) // 100 MB
        throw new InvalidOperationException("Insufficient disk space");

    // Try creating a test file to verify write permissions
    try
    {
        var testFile = Path.Combine(dirInfo.FullName, ".sheet-atlas-test");
        File.WriteAllText(testFile, "test");
        File.Delete(testFile);
    }
    catch (UnauthorizedAccessException)
    {
        throw new InvalidOperationException($"No write permission: {outputPath}");
    }
}
```

**Priority**: HIGH - should be addressed before v0.5.3 release

---

### 7.2 Memory Leak Risk: ColumnLinkingViewModel Event Subscriptions

**Risk**: Lines 228-232 subscribe to `_filesManager` events, but disposal (line 561-565) may not always be called

**Recommended Mitigation**:
```csharp
public void Dispose()
{
    if (!_disposed)
    {
        // Always unsubscribe, even if subscriber is null (defensive)
        if (_filesManager != null)
        {
            _filesManager.FileLoaded -= OnFilesChanged;
            _filesManager.FileRemoved -= OnFilesChanged;
        }

        // Clear in try-finally to ensure cleanup
        try
        {
            foreach (var item in ColumnLinks)
            {
                item.PropertyChanged -= OnColumnLinkPropertyChanged;
                item.Cleanup();
            }
        }
        finally
        {
            ColumnLinks.Clear();
            _disposed = true;
        }
    }
}
```

**Priority**: MEDIUM - should be addressed before v0.5.3 release

---

## 8. SUMMARY TABLE: Ranked Refactoring Opportunities

| Priority | Area | Impact | Effort | Risk | Complexity |
|----------|------|--------|--------|------|------------|
| **HIGH** | Extract export formatting helpers | High | Medium | Low | Medium |
| **HIGH** | RowComparisonViewModel: Remove SetExportServices antipattern | Medium-High | Medium | Low | Medium |
| **HIGH** | ColumnLinkingViewModel: Extract StateManager | High | Medium | Medium | High |
| **MEDIUM** | Create HeaderGroupingService (consolidate 3 duplicates) | Medium | Low | Low | Low |
| **MEDIUM** | Create IComparisonExportFormat pipeline | Medium | Medium | Medium | Medium |
| **MEDIUM** | SearchResult: Use sealed record hierarchy | Medium-High | Medium | Medium | Medium |
| **MEDIUM** | SheetAnalysisOrchestrator: Use pipeline pattern | Medium | High | Medium | High |
| **MEDIUM** | RowComparisonViewModel: Extract IRowComparisonPresenter | Medium | Medium | Low | Medium |
| **LOW** | FilteringHelper utility (deduplicate column filtering) | Low | Low | Very Low | Very Low |
| **LOW** | ExportResult enhancement with metadata | Low | Low | Low | Low |

---

## 9. RECOMMENDED APPROACH FOR v0.5.3 RELEASE

### Phase 1: Pre-Release Quick Wins (Low Risk, Immediate Impact)

**Target**: Address before v0.5.3 release

1. ✅ **HeaderGroupingService** (MEDIUM priority, LOW effort/risk)
   - Consolidate 3 duplications
   - Can be done in 1-2 hours
   - Testable easily
   - Non-breaking change

2. ✅ **Export Path Validation** (Risk mitigation)
   - Add disk space/permission checks
   - Prevents user-reported bugs
   - Quick win for stability

3. ✅ **ColumnLinkingViewModel Disposal Fix** (Risk mitigation)
   - Fix potential memory leak
   - Low effort, high safety improvement

### Phase 2: Post-Release Foundation Work (Medium Risk, High Impact)

**Target**: v0.6.0 or v1.0.0

1. **Export Formatting Helpers Extraction**
   - Foundation for new export formats
   - Requires comprehensive testing
   - Breaking changes to internal APIs

2. **RowComparisonViewModel Refactoring**
   - Remove SetExportServices antipattern
   - Create ExportCoordinator
   - Requires UI testing

3. **SearchResult Sealed Records**
   - Breaking change
   - Requires updating all callers
   - Needs migration guide

### Phase 3: Long-Term Architectural Improvements (High Risk, High Impact)

**Target**: v1.0.0+

1. **Export Pipeline Abstraction**
2. **SheetAnalysisOrchestrator Pipeline Pattern**
3. **ColumnLinkingViewModel StateManager Extraction**

---

## 10. NEXT STEPS

### Immediate Actions (Before v0.5.3 Release)

- [ ] Implement `HeaderGroupingService` to consolidate duplication
- [ ] Add export path validation to `ComparisonExportService`
- [ ] Fix `ColumnLinkingViewModel` disposal pattern
- [ ] Test export with large datasets (>1GB, merge cells)
- [ ] Audit all `Task.Run()` calls for proper cancellation token handling
- [ ] Add export progress indication (currently silent for large exports)

### Post-Release Planning

- [ ] Create refactoring backlog in project tracking
- [ ] Prioritize Phase 2 items for v0.6.0 roadmap
- [ ] Document architectural decisions and patterns
- [ ] Set up code metrics tracking (complexity, duplication)

---

## Conclusion

The SheetAtlas codebase demonstrates solid architectural foundations with clear separation of concerns. Recent feature additions have introduced opportunities for consolidation and abstraction that should be addressed to maintain long-term maintainability.

**Key takeaway**: Focus on low-risk, high-impact refactoring before v0.5.3 release (HeaderGroupingService, export validation), then plan medium-risk architectural improvements for subsequent releases.

The identified patterns (header grouping, export formatting, state management) represent natural evolution points as the application scales to support additional features and export formats.

---

**Analysis performed by**: Claude Code (Explore agent)
**Review date**: 2026-01-30
**Next review**: After v0.5.3 release
