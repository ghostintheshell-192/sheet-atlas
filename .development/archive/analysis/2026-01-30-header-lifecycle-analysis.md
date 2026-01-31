# Header Lifecycle Analysis - SheetAtlas
**Date**: 2026-01-30
**Context**: Pre-refactoring architectural evaluation
**Scope**: Complete header creation, storage, transformation, and usage flow

---

## Executive Summary

This document provides a comprehensive analysis of how headers are managed throughout the SheetAtlas codebase, from initial extraction from Excel files to final display and export.

**Key findings**:
- **Overall architecture**: Sound separation of concerns (extraction → storage → usage)
- **Critical issue**: Header semantic name resolution is fragmented across three sources
- **Duplication found**: GroupHeadersBySemanticName() implemented twice (Export + UI)
- **Performance concern**: O(n) header lookup without caching in ExcelRow
- **Memory overhead**: Headers copied into every ExcelRow instead of referenced

**Verdict**: Current design is MOSTLY SOUND, but needs consolidation in header resolution and grouping logic.

---

## 1. HEADER CREATION: Where & How Headers Enter the System

### 1.1 Initial Extraction Points

Headers are extracted during the file reading phase by format-specific readers:

| Reader | Source | Process |
|--------|--------|---------|
| **OpenXmlFileReader** | First row of XLSX/XLSM | `ProcessHeaderRow()` → reads cells from first Row element, parsed via cell references, stored in `Dictionary<int, string>` |
| **CsvFileReader** | CSV header record | CsvHelper auto-reads first record as headers (configurable via `HasHeaderRow` option), stored in `IDictionary<string, object>` |
| **XlsFileReader** | First row of XLS | Similar to XLSX, row-based extraction |

**Key file locations:**
- `/data/repos/sheet-atlas/src/SheetAtlas.Infrastructure/External/Readers/OpenXmlFileReader.cs` (lines 323-347)
- `/data/repos/sheet-atlas/src/SheetAtlas.Infrastructure/External/Readers/CsvFileReader.cs` (lines 156-199)

### 1.2 Storage Format During Extraction

```
OpenXml:     Dictionary<int, string>       // Column index → header name
CSV:         IDictionary<string, object>  // Field name → value
Result:      Dictionary<int, string>      // Normalized to index-based
```

### 1.3 Normalization & Uniqueness

Headers are normalized in `CreateColumnNamesArray()` (OpenXmlFileReader:349-385):

```csharp
// For each column position:
// 1. Use provided header value OR generate "Column_{i}"
// 2. Ensure uniqueness (if duplicate, append _2, _3, etc.)
// 3. Result: string[] with unique names
```

**Critical behavior:**
- Empty headers → `"Column_{columnIndex}"`
- Duplicates → `"{originalName}_{count}"`
- Case preserved from source file
- Trimmed of whitespace

---

## 2. HEADER STORAGE: Domain Entity Structure

### 2.1 Primary Storage: SASheetData.ColumnNames

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.Core/Domain/Entities/SASheetData.cs`

```csharp
public class SASheetData
{
    /// String array, one per column
    public string[] ColumnNames { get; private set; }

    /// Number of header rows (supports multi-row headers)
    public int HeaderRowCount { get; private set; } = 1;

    /// Immutable - set only in constructor
    public SASheetData(string sheetName, string[] columnNames, ...)
    {
        ColumnNames = columnNames ?? throw new ArgumentNullException();
    }
}
```

**Key characteristics:**
- **Immutable after construction** (only set in constructor)
- **0-based indexing** aligned with cell array layout
- **Direct positional access** via column index
- **Memory efficient** - single string array, not per-row duplication
- **Multi-row header ready** - `HeaderRowCount` can be > 1 (currently always 1)

### 2.2 Secondary Storage: ExcelRow.ColumnHeaders

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.Core/Domain/Entities/RowComparison.cs`

When rows are extracted for comparison:
```csharp
public class ExcelRow
{
    public IReadOnlyList<string> ColumnHeaders { get; }

    public ExcelRow(ExcelFile sourceFile, string sheetName, int rowIndex,
                   IReadOnlyList<object?> cells,
                   IReadOnlyList<string> columnHeaders)  // COPIED HERE
    {
        ColumnHeaders = columnHeaders;
    }
}
```

**Issue:** Headers are copied from SASheetData into each ExcelRow
- Redundant storage (n rows × column header size)
- BUT: Enables independent row comparison without sheet reference
- Trade-off: Allows ExcelRow to be used standalone

---

## 3. HEADER ACCESS PATTERNS: Where Headers Are Used

### 3.1 Direct Column Name Lookup

| Location | Pattern | Purpose |
|----------|---------|---------|
| SearchService.cs:100 | `sheet.ColumnNames[colIndex]` | Get header for search result context |
| RowComparisonService.cs:94 | `sheet.ColumnNames` | Extract all headers for comparison |
| ColumnLinkingService.cs:128 | `sheet.ColumnNames` | Extract columns for linking |
| ExcelRow:230-253 | `ColumnHeaders.ToList().IndexOf(headerName)` | Case-insensitive header lookup |

### 3.2 Header Retrieval Methods

**RowComparisonService.GetColumnHeaders()** (line 85-95):
```csharp
public IReadOnlyList<string> GetColumnHeaders(ExcelFile file, string sheetName)
{
    var sheet = file.GetSheet(sheetName);
    return sheet.ColumnNames;  // Direct pass-through
}
```

**ExcelRow.GetCellAsStringByHeader()** (line 230-253):
```csharp
// Exact match first
var headerIndex = ColumnHeaders.ToList().IndexOf(headerName);
if (headerIndex >= 0) return GetCellAsString(headerIndex);

// Fall-through: case-insensitive match
var normalized = headerName.Trim().ToLowerInvariant();
for (int i = 0; i < ColumnHeaders.Count; i++)
{
    if (ColumnHeaders[i].Trim().ToLowerInvariant() == normalized)
        return GetCellAsString(i);
}
return string.Empty;
```

**Issue:** Two-pass header lookup (case-sensitive first, then case-insensitive)
- O(n) complexity per call
- Called repeatedly during row comparison
- Could benefit from cache

---

## 4. HEADER TRANSFORMATION: Semantic Name Mapping

### 4.1 Column Linking Service

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.Core/Application/Services/ColumnLinkingService.cs`

**Flow:**
```
ExcelFile.Sheets
    ↓
ExtractColumnsFromFiles() [line 115-147]
    ↓ Per sheet, extract ColumnNames[]
    ↓ Create ColumnInfo(Name, DetectedType, SourceFile, SourceSheet)
    ↓
CreateInitialGroups() [line 55-99]
    ↓ Group by lowercase name
    ↓ Determine dominant type
    ↓ Create ColumnLink with SemanticName = first column's original name
```

**ColumnLink structure:**
```csharp
public sealed record ColumnLink
{
    string SemanticName;              // User-assigned (e.g., "Revenue")
    IReadOnlyList<LinkedColumn> LinkedColumns;  // Columns in this group
    DataType DominantType;            // Most common type
    bool IsAutoGrouped;               // true = automatic, false = user-edited
}

public sealed record LinkedColumn
{
    string Name;                      // Original header name
    DataType DetectedType;
    string? SourceFile;
    string? SourceSheet;
}
```

**Key behavior:**
- SemanticName initially = first column's original name (case-preserved)
- Can be edited by user via ColumnLinkingViewModel
- Maps: "Rev 2016", "Rev 2017" → SemanticName: "Revenue" (or whatever user sets)

### 4.2 Finding Semantic Names

**ColumnLinkingService.FindMatchingLink()** (line 101-113):
```csharp
public ColumnLink? FindMatchingLink(string columnName, IEnumerable<ColumnLink> links)
{
    foreach (var link in links)
    {
        if (link.Matches(columnName))  // Checks SemanticName OR any LinkedColumn.Name
            return link;
    }
    return null;
}
```

**ColumnLink.Matches()** (ColumnLink.cs:123-142):
```csharp
public bool Matches(string columnName)
{
    // Match semantic name
    if (SemanticName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        return true;

    // Match any linked column name
    foreach (var linked in LinkedColumns)
    {
        if (linked.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

### 4.3 Template-Based Semantic Mapping

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.Core/Domain/ValueObjects/ExpectedColumn.cs`

Templates define expected columns with optional semantic names:
```csharp
public sealed record ExpectedColumn
{
    string Name;                      // Original header name to match
    string? SemanticName;             // User-defined semantic name (e.g., "Revenue")
    IReadOnlyList<string>? AlternativeNames;  // "Rev", "Total Revenue", etc.
}

// Usage:
ExpectedColumn.Text("Date")
    .WithSemanticName("Transaction Date")
    .WithAlternatives("Txn Date", "Date Processed")
```

---

## 5. HEADER GROUPING & MERGING

### 5.1 ComparisonExportService.GroupHeadersBySemanticName()

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.Infrastructure/External/Writers/ComparisonExportService.cs` (lines 388-416)

This is the **core grouping logic** used in exports:

```csharp
private static List<HeaderGroup> GroupHeadersBySemanticName(
    IReadOnlyList<string> allHeaders,
    IEnumerable<string>? includedColumns,
    IReadOnlyDictionary<string, string>? semanticNames)
{
    var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var originalHeader in allHeaders)
    {
        // 1. Filter by includedColumns if provided
        if (includedSet != null && !includedSet.Contains(originalHeader))
            continue;

        // 2. Get display name (semantic name if available, else original)
        var displayName = GetDisplayName(originalHeader, semanticNames);

        // 3. Group by display name
        if (!groups.TryGetValue(displayName, out var originalHeaders))
        {
            originalHeaders = new List<string>();
            groups[displayName] = originalHeaders;
        }
        originalHeaders.Add(originalHeader);
    }

    return groups.Select(g => new HeaderGroup(g.Key, g.Value)).ToList();
}

private sealed record HeaderGroup(string DisplayName, IReadOnlyList<string> OriginalHeaders);
```

**Key points:**
- Maps: `["Price", "Cost", "Value"]` → GroupedBy: `"Amount"` (if semantic name is "Amount")
- Merges multiple original columns into one export column
- Selects first value from any of the original headers when exporting

### 5.2 RowComparisonViewModel.RefreshColumns()

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs` (lines 287-377)

Implements the **UI-level header grouping**:

```csharp
private void RefreshColumns()
{
    var allHeaders = Comparison.GetAllColumnHeaders();

    // Group headers by semantic name
    var headerGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var rawHeader in allHeaders)
    {
        // 1. Skip if not in included set
        if (includedColumnsSet != null && !includedColumnsSet.Contains(rawHeader))
            continue;

        // 2. Get display header (semantic name if resolver provided)
        var displayHeader = rawHeader;
        if (_semanticNameResolver != null)
        {
            var semanticName = _semanticNameResolver(rawHeader);
            if (!string.IsNullOrEmpty(semanticName))
                displayHeader = semanticName;
        }

        // 3. Group by display header
        if (!headerGroups.TryGetValue(displayHeader, out var rawHeaders))
        {
            rawHeaders = new List<string>();
            headerGroups[displayHeader] = rawHeaders;
        }
        rawHeaders.Add(rawHeader);
    }

    // Create column view models (one per semantic/display name)
    foreach (var group in headerGroups)
    {
        var columnViewModel = new RowComparisonColumnViewModel(
            group.Key,           // Display header
            group.Value,         // Raw headers
            columnIndex,
            Comparison.Rows);
        Columns.Add(columnViewModel);
    }
}
```

**Parallel implementation to ExportService:**
- Same grouping logic, different context (UI vs file export)
- SemanticNameResolver injected at construction time
- Can filter by includedColumns

### 5.3 RowComparisonColumnViewModel Cell Access

**File:** `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs` (lines 429-469)

When displaying merged columns:
```csharp
private static string GetCellValueFromAnyHeader(ExcelRow row, IReadOnlyList<string> headers)
{
    // Try each raw header until one has a value
    foreach (var header in headers)
    {
        var value = row.GetCellAsStringByHeader(header);
        if (!string.IsNullOrEmpty(value))
            return value;
    }
    return string.Empty;
}
```

**Behavior:** First non-empty value wins (priority order = order in list)

---

## 6. COMPLETE HEADER LIFECYCLE FLOW DIAGRAM

```
┌─────────────────────────────────────────────────────────────────┐
│                    FILE READING PHASE                           │
├─────────────────────────────────────────────────────────────────┤
│ OpenXmlFileReader.ReadAsync()                                   │
│ ├─ ProcessHeaderRow()                                           │
│ │  └─ Dictionary<int, string> headerColumns                     │
│ └─ CreateColumnNamesArray()                                     │
│    ├─ Ensures uniqueness (add _2, _3 for duplicates)           │
│    └─ string[] columnNames (ready for SASheetData)              │
│                                                                  │
│ Result: SASheetData                                             │
│ ├─ ColumnNames: string[]                                        │
│ ├─ HeaderRowCount: 1                                            │
│ └─ Cells: flat SACellData[] array                               │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│               COLUMN LINKING PHASE (Optional)                   │
├─────────────────────────────────────────────────────────────────┤
│ ColumnLinkingService.ExtractColumnsFromFiles()                  │
│ ├─ For each file/sheet: extract ColumnNames[]                  │
│ └─ Create ColumnInfo(Name, DetectedType, File, Sheet)           │
│                                                                  │
│ ColumnLinkingService.CreateInitialGroups()                      │
│ ├─ Group by lowercase name                                      │
│ ├─ Determine dominant type                                      │
│ └─ Create ColumnLink[]                                          │
│    └─ SemanticName = first column's original name               │
│                                                                  │
│ User edits: ColumnLink.WithSemanticName("Revenue")              │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│              ROW COMPARISON PHASE                               │
├─────────────────────────────────────────────────────────────────┤
│ RowComparisonService.CreateRowComparison()                      │
│ ├─ For each SearchResult:                                       │
│ │  └─ ExtractRowFromSearchResult()                              │
│ │     ├─ Get sheet from file                                    │
│ │     ├─ Extract row data (cells)                               │
│ │     └─ COPY ColumnHeaders from sheet.ColumnNames              │
│ │                                                                │
│ └─ Create RowComparison                                         │
│    ├─ ExcelRow[] rows (with ColumnHeaders copied)               │
│    └─ GetAllColumnHeaders() → union of all row headers          │
│                                                                  │
│ RowComparison.AnalyzeStructuralIssues()                         │
│ └─ Detect header mismatches across rows/files                   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│             UI DISPLAY / EXPORT PHASE                           │
├─────────────────────────────────────────────────────────────────┤
│ TWO PARALLEL PATHS:                                             │
│                                                                  │
│ PATH 1: RowComparisonViewModel.RefreshColumns()                 │
│ ├─ Get all headers from RowComparison                           │
│ ├─ Apply semantic name resolver (if available)                  │
│ ├─ Group by display name                                        │
│ └─ Create RowComparisonColumnViewModel for each group           │
│                                                                  │
│ PATH 2: ComparisonExportService.ExportToExcelAsync()            │
│ ├─ GroupHeadersBySemanticName()                                 │
│ │  ├─ Apply semantic names dictionary                           │
│ │  └─ Group by display name                                     │
│ └─ Write to Excel with grouped headers                          │
│                                                                  │
│ Cell Value Resolution (merged columns):                         │
│ └─ GetCellValueFromAnyHeader(row, rawHeaders[])                 │
│    └─ Try each original header, return first non-empty value    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 7. CURRENT SOURCES OF TRUTH

| Component | Is Source of Truth? | Notes |
|-----------|-------------------|-------|
| SASheetData.ColumnNames | ✅ PRIMARY | Original extracted headers, immutable |
| ExcelRow.ColumnHeaders | ⚠️ COPY | Duplicated from SASheetData for row portability |
| RowComparison.GetAllColumnHeaders() | ⚠️ DERIVED | Union of all row headers |
| ColumnLink.SemanticName | ✅ SEMANTIC | User-assigned mapping (optional) |
| ExpectedColumn.SemanticName | ✅ TEMPLATE | Template-defined mapping (optional) |
| ComparisonExportService parameter | ⚠️ INJECTED | Dictionary passed at export time |
| RowComparisonViewModel._semanticNameResolver | ⚠️ INJECTED | Function passed at construction |

---

## 8. DESIGN ISSUES & INCONSISTENCIES

### 8.1 CRITICAL ISSUES

**Issue 1: Duplicate Header Storage**
- **Problem:** Headers copied into every ExcelRow
  ```csharp
  // In RowComparisonService.ExtractRowFromSearchResult()
  var columnHeaders = GetColumnHeaders(searchResult.SourceFile, searchResult.SheetName);
  return new ExcelRow(..., cells, columnHeaders);  // COPIED HERE
  ```
- **Impact:** Memory overhead for large row comparisons (n rows × header size)
- **Severity:** MEDIUM (usually headers are small, but scales badly)
- **Root cause:** Design decision to make ExcelRow independent of SASheetData

**Issue 2: Header Semantic Name Lookup is Fragmented**
- **Problem:** Three different ways semantic names are resolved:
  1. ColumnLink.SemanticName (from ColumnLinkingService)
  2. ExpectedColumn.SemanticName (from templates)
  3. RowComparisonViewModel._semanticNameResolver (injected function)
- **Impact:**
  - No unified semantic name resolution
  - Can create conflicting mappings
  - Hard to trace which resolver was used
- **Severity:** HIGH (architectural inconsistency)

**Issue 3: Header Lookup Performance**
- **Problem:** ExcelRow.GetCellAsStringByHeader() does O(n) scan twice (case-sensitive, then case-insensitive)
  ```csharp
  var headerIndex = ColumnHeaders.ToList().IndexOf(headerName);  // O(n) conversion
  if (headerIndex >= 0) return GetCellAsString(headerIndex);

  // Fall-through O(n) scan
  for (int i = 0; i < ColumnHeaders.Count; i++)
      if (normalized == normalized_header) return value;
  ```
- **Impact:** Inefficient for wide rows (100+ columns)
- **Severity:** MEDIUM (appears in tight loops, but usually acceptable size)

**Issue 4: Dual Grouping Logic**
- **Problem:** GroupHeadersBySemanticName() implemented twice:
  1. ComparisonExportService (for exports)
  2. RowComparisonViewModel.RefreshColumns() (for UI)
- **Impact:**
  - Difficult to maintain consistency
  - Bug fix in one place doesn't propagate
  - Different semantics possible
- **Severity:** HIGH (violation of DRY principle)

### 8.2 MODERATE ISSUES

**Issue 5: Case Sensitivity Inconsistency**
- Headers stored case-preserving (as in source file)
- Lookups often case-insensitive
- Some groupings case-sensitive, others case-insensitive
- Example: ColumnLinkingService groups by `name.ToLowerInvariant()` but then uses first column's original name (case-preserved) as SemanticName

**Issue 6: Multi-row Header Support Limited**
- Architecture supports `HeaderRowCount > 1`
- But in practice, always 1
- Headers from multiple rows would be concatenated with newline (documented in SASheetData)
- Never tested or validated

**Issue 7: No Header Metadata Structure**
- Headers are flat strings
- No metadata about column type, format, width, etc. in the header itself
- Type metadata stored separately in ColumnMetadata via ColumnIndex
- Could make header resolution more reliable

---

## 9. ARCHITECTURAL ASSESSMENT

### 9.1 Is Current Design Sound?

**Verdict:** MOSTLY SOUND, with concerning fragmentation in header resolution

**Strengths:**
✅ Clean separation: extraction (readers) → storage (SASheetData) → usage (comparison/export)
✅ Immutable headers after creation (good for thread safety)
✅ Supports multi-row headers architecturally (even if not used)
✅ Semantic mapping is optional and pluggable
✅ Type metadata tracked separately via ColumnMetadata

**Weaknesses:**
❌ Header semantic resolution is fragmented across three sources
❌ Grouping logic duplicated in two places
❌ Header lookup performance O(n) without caching
❌ ExcelRow redundantly copies headers instead of referencing SASheetData
❌ No unified header resolution interface

### 9.2 Refactoring Recommendations (Ordered by Priority)

**Recommendation 1: Create IHeaderResolver Interface** (HIGH PRIORITY)

```csharp
public interface IHeaderResolver
{
    /// Get semantic name for a header (or null if no mapping)
    string? ResolveSemanticName(string originalHeader);

    /// Get all names that match this header (original + semantic + aliases)
    IEnumerable<string> GetAllNames(string originalHeader);
}

// Implementations:
// - ColumnLinkingHeaderResolver (uses ColumnLink.Matches)
// - TemplateHeaderResolver (uses ExpectedColumn.SemanticName)
// - CompositeHeaderResolver (tries multiple resolvers)
// - NullHeaderResolver (identity mapping)
```

**Recommendation 2: Extract HeaderGrouper Service** (HIGH PRIORITY)

```csharp
public interface IHeaderGrouper
{
    /// Group headers by semantic name
    List<HeaderGroup> GroupBySemantic(
        IReadOnlyList<string> headers,
        IHeaderResolver resolver,
        IEnumerable<string>? includedColumns = null);
}

// Single implementation used by both Export and UI
```

**Recommendation 3: Add Header Name Cache to ExcelRow** (MEDIUM PRIORITY)

```csharp
public class ExcelRow
{
    private Dictionary<string, int>? _headerIndex;  // Lazy-loaded cache

    public int FindHeaderIndex(string headerName)
    {
        _headerIndex ??= BuildHeaderIndex();

        // Try exact match
        if (_headerIndex.TryGetValue(headerName, out int idx))
            return idx;

        // Try case-insensitive
        var key = headerName.Trim().ToLowerInvariant();
        foreach (var kvp in _headerIndex)
        {
            if (kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return -1;
    }
}
```

**Recommendation 4: Store Header Metadata** (LOW PRIORITY)

```csharp
public record HeaderInfo
{
    public string Name { get; init; }
    public int ColumnIndex { get; init; }
    public DataType? DetectedType { get; init; }
    public string? SemanticName { get; init; }
    public bool IsHidden { get; init; }
}
```

**Recommendation 5: Eliminate Header Copy in ExcelRow** (LOW PRIORITY)

```csharp
// Replace IReadOnlyList<string> ColumnHeaders with reference:
public IReadOnlyList<string> ColumnHeaders =>
    SourceFile.GetSheet(SheetName)?.ColumnNames ?? Array.Empty<string>();
```

This breaks row independence but saves memory and keeps headers in one place.

---

## 10. RECOMMENDED REFACTORING APPROACH

### Phase 1: Consolidate Grouping Logic (Current Branch)

1. ✅ Extract `IHeaderGroupingService` with `GroupHeaders()` method
2. ✅ Implement `HeaderGroupingService`
3. ✅ Update `ComparisonExportService` to use service
4. ✅ Update `RowComparisonViewModel` to use service
5. ✅ Write comprehensive unit tests

**Benefit:** Eliminates duplication, single source of truth for grouping

### Phase 2: Unify Semantic Name Resolution (Future)

1. Create `IHeaderResolver` interface
2. Implement resolvers for each source (ColumnLink, Template, Dictionary)
3. Create `CompositeHeaderResolver` that chains resolvers
4. Refactor consumers to use IHeaderResolver

**Benefit:** Single, clear semantic name resolution path

### Phase 3: Performance Optimizations (Future)

1. Add header name cache to ExcelRow
2. Profile and measure impact
3. Consider eliminating header copy if beneficial

**Benefit:** Improved performance for wide datasets

---

## Conclusion

The SheetAtlas header management system is **well-architected overall**, with clear separation of concerns and good domain modeling. The primary issues are:

1. **Duplication** of grouping logic (addressable in current refactoring)
2. **Fragmentation** of semantic name resolution (needs architectural work)
3. **Performance** concerns with header lookup (optimization opportunity)

The current refactoring plan (consolidating GroupHeadersBySemanticName) is **sound and addresses the most pressing issue**. Future work should focus on unifying semantic name resolution through a common interface.

---

**Analysis performed by**: Claude Code (Explore agent)
**Analysis date**: 2026-01-30
**Next review**: After refactoring completion
