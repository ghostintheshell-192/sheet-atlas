---
type: bug
priority: medium
status: open
discovered: 2026-01-25
related: []
related_decision: null
report: null
---

# Comparison Export Ignores Column Filtering

## Problem
When creating a comparison and applying column filters (hiding some columns to simplify the view), data export does not respect the applied filtering. The export includes all columns, even those intentionally hidden by the user.

## Analysis
Column filtering is an **intentional** modification of the comparison view by the user. When the user exports data, they expect the export to reflect exactly what they see on screen, including only visible columns.

**Current behavior:**
- User applies column filter → hides columns A, B, C
- User exports comparison → export contains ALL columns (including A, B, C)

**Expected behavior:**
- User applies column filter → hides columns A, B, C
- User exports comparison → export contains ONLY visible columns

**Impact:**
- Confusing for the user
- Export contains data that the user explicitly chose to exclude
- Inefficient workflow (user must manually remove columns from export)

## Possible Solutions
- **Option A: Export always respects filtering** - Export includes only columns currently visible in UI
  - Pro: Intuitive behavior, respects user intent
  - Pro: WYSIWYG (What You See Is What You Get)
  - Con: None

- **Option B: Checkbox "Include only visible columns"** - Add an option in export dialog
  - Pro: Flexibility for users who want complete export
  - Con: Additional UI complexity
  - Con: Default might still confuse users

## Recommended Approach
**Option A** - Export always respects filtering.

If the user wants to export all columns, they can simply remove filters before export. The WYSIWYG principle is more important than flexibility in this case.

## Notes
- Reported by user during v0.5.1 testing
- This behavior applies to both Excel and CSV export
- Column filtering is a UI feature in `ColumnsSidebarView` / `ColumnLinkingViewModel`

## Related Documentation
- **Code Locations**:
  - Export: `src/SheetAtlas.Infrastructure/External/Writers/ComparisonExportService.cs`
  - Column filtering: `src/SheetAtlas.UI.Avalonia/ViewModels/ColumnLinkingViewModel.cs`
  - Row comparison: `src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs`
