# Column Linking

**Status**: implemented
**Release**: v0.4.0 (released 2025-12-16)
**Priority**: must-have
**Depends on**: foundation-layer.md
**Enables**: template-validation.md (edit headers), column-grouping.md

## Summary

Foundation service for linking semantically equivalent columns across files. Multiple column names map to one semantic concept. Used by both Template Management (persistent) and Column Grouping (runtime).

## Problem Statement

The same conceptual column can have different names across files:

- "Rev 2016", "Rev 2017", "Revenue" → all mean "Revenue"
- "Amount (€)" in File A vs "Amount" (number) in File B → same name, different meaning

Matching by name alone is ambiguous. Users need explicit control over which columns are semantically equivalent.

## User Stories

- As a user, I want columns with the same name and type to be automatically grouped
- As a user, I want to manually adjust groupings when the automatic matching is wrong
- As a user, I want to assign a semantic name to a group of columns
- As a user, I want my column links to be saved in templates for reuse

## Design Principles

1. **Auto-grouping as suggestion**: System groups by name + type, user confirms/adjusts
2. **Explicit control**: User has final say on what's linked
3. **Origin tracking**: Know which file/sheet each column comes from
4. **Reusable foundation**: Same mechanism for templates and runtime grouping

## Requirements

### Functional

#### Data Structure

```csharp
public sealed record LinkedColumn
{
    public string Name { get; init; }
    public string? SourceFile { get; init; }
    public string? SourceSheet { get; init; }
    public DataType DetectedType { get; init; }
    public string SourceDisplay { get; }  // "FileName - SheetName" or "FileName"

    public static LinkedColumn Create(string name, DataType type,
        string sourceFile, string? sourceSheet = null);
}

public sealed record ColumnLink
{
    public string SemanticName { get; init; }
    public IReadOnlyList<LinkedColumn> LinkedColumns { get; init; }
    public DataType DominantType { get; init; }
    public bool IsAutoGrouped { get; init; }  // false after manual edit

    public int ColumnCount { get; }
    public int SourceCount { get; }  // Unique sources

    public bool Matches(string columnName);  // Case-insensitive
    public static ColumnLink FromGroup(string semanticName, DataType type,
        params LinkedColumn[] columns);
}
```

#### Auto-Grouping (Initial State)

- [x] Group columns with same name AND same dominant type
- [x] Display as collapsed tree showing source count
- [x] Columns with same name but different types stay separate
- [x] Auto-refresh when files are loaded/unloaded

#### Manual Adjustment

- [x] Drag & drop column onto another to merge groups
- [ ] Drag out single column to remove from group (deferred - use Ungroup + re-merge)
- [x] Menu → "Ungroup" to dissolve entire group into individual columns

#### Semantic Naming

- [x] Menu → "Rename..." on any column/group
- [x] Inline editing with Enter to confirm, Escape to cancel
- [x] SemanticName defaults to first column's original name
- [x] SemanticName included in matching

#### Matching Logic

- [x] Case-insensitive comparison
- [x] Match against SemanticName OR any LinkedColumn.Name
- [x] Origin (file/sheet) not used in matching, only for display

### UI: Column Sidebar (MultiSidebar)

```
COLUMNS                    [Refresh]
─────────────────────────────────────
▶ Amount (Currency)        4    ⋮   ← collapsed, 4 columns
▼ Revenue (Number)         3    ⋮   ← expanded
    ├─ Rev 2016     FileA.xlsx
    ├─ Rev 2017     FileB.xlsx
    └─ Revenue      FileC.xlsx
  Date (Date)              1    ⋮
  Customer ID (Text)       1    ⋮
```

#### Display

- [x] Column name + dominant type below
- [x] Tree structure for groups (collapsible with ▶/▼)
- [x] Source count badge for groups
- [x] Source file shown on children
- [x] Tooltips on truncated names

#### Interactions

- [x] Click ▶/▼ to expand/collapse group
- [x] Drag & drop to merge groups
- [x] ⋮ menu button for actions:
  - [x] Rename... (inline editing)
  - [x] Ungroup (only for groups with 2+ columns)
  - [ ] View source details (deferred)

#### Sidebar Features

- [x] Resizable width (180px - 500px) with drag grip
- [x] Auto-refresh on file load/unload
- [x] Memory cleanup on dispose

### Integration Points

#### Row Comparison View

- [x] Display semantic names in column headers
- [x] Columns with same semantic name merged into single column
- [x] Cell values looked up using all raw headers from group
- [x] Comparison highlighting works correctly with merged columns

#### Template Management

- [ ] Links saved in template JSON (ExpectedColumn uses ColumnLink)
- [ ] When editing template, sidebar shows template columns
- [ ] User can adjust links, changes persist to template

#### Column Grouping (Runtime)

- [x] Links created at runtime for current session
- [x] Affects comparison (grouped columns treated as equivalent)
- [ ] Option to "Save to template" (promote runtime link to persistent)

### Non-Functional

- [x] Performance: grouping is instant even with many columns
- [x] UX: auto-grouping reduces work, manual adjustment is easy

## Implementation Status

### Completed (v0.4.0-dev)

| Component | Status | Notes |
|-----------|--------|-------|
| `LinkedColumn` value object | ✅ Done | Core/Domain/ValueObjects |
| `ColumnLink` value object | ✅ Done | Core/Domain/ValueObjects |
| `IColumnLinkingService` | ✅ Done | CreateInitialGroups, FindMatchingLink, MergeGroups, Ungroup |
| `ColumnLinkingViewModel` | ✅ Done | With MergeColumns, UngroupColumn methods |
| `ColumnsSidebarView` | ✅ Done | Expandable tree, drag & drop, context menu |
| MultiSidebar resize | ✅ Done | Drag grip on right edge |
| Comparison integration | ✅ Done | Semantic names displayed, columns merged |

### Pending

| Component | Status | Notes |
|-----------|--------|-------|
| Template JSON persistence | ⏳ Pending | Save/load ColumnLinks in template |
| "Save to template" action | ⏳ Pending | Promote runtime links to template |
| View source details | ⏳ Pending | Low priority |

## Technical Notes

### Service Interface

```csharp
public interface IColumnLinkingService
{
    IReadOnlyList<ColumnLink> CreateInitialGroups(IEnumerable<ColumnInfo> columns);
    ColumnLink? FindMatchingLink(string columnName, IEnumerable<ColumnLink> links);
    IEnumerable<ColumnInfo> ExtractColumnsFromFiles(IEnumerable<ExcelFile> files);
    ColumnLink MergeGroups(ColumnLink target, ColumnLink source);
    IReadOnlyList<ColumnLink> Ungroup(ColumnLink link);
}
```

### Comparison View Integration

The `RowComparisonViewModel` receives a semantic name resolver function:

```csharp
Func<string, string?>? semanticNameResolver = rawHeader =>
{
    foreach (var link in currentLinks)
    {
        if (link.Matches(rawHeader))
            return link.SemanticName;
    }
    return null;
};
```

Headers are grouped by semantic name before creating columns:

- Raw headers resolving to same semantic name → merged into one column
- Cell values looked up using all raw headers from the group

### JSON Serialization (for templates - pending)

```json
{
  "columns": [
    {
      "semanticName": "Revenue",
      "linkedColumns": [
        { "name": "Rev 2016", "sourceFile": "Budget2016.xlsx" },
        { "name": "Rev 2017", "sourceFile": "Budget2017.xlsx" }
      ],
      "expectedType": "Currency",
      "isRequired": true
    }
  ]
}
```

## Open Questions

- [x] ~~Should auto-grouping consider column position as well as name + type?~~ → No, only name + type
- [x] ~~How to handle columns from files added later?~~ → Auto-refresh via event subscription
- [x] ~~Visual distinction between auto-grouped and manually-grouped?~~ → `IsAutoGrouped` flag available, not currently displayed

## Acceptance Criteria

- [x] Columns with same name + type are auto-grouped on load
- [x] User can drag columns to merge groups
- [x] User can ungroup via context menu
- [x] User can assign semantic name via inline editing
- [ ] Links are saved in template JSON
- [x] Matching works against semantic name and all linked names
- [x] Source file/sheet is tracked and displayable
- [x] Semantic names appear in comparison view
- [x] Columns with same semantic name are merged in comparison

## Related Commits

- `dfce0b2` - MultiSidebar VSCode-style control
- `95c7aa9` - Column Linking foundation (service, value objects, sidebar view)
- `66cbf0d` - UI polish, drag & drop, context menu, comparison integration
- `88ad16c` - Informative log for Unknown type in sparse columns
