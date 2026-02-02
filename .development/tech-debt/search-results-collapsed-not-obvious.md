---
type: feature
priority: medium
status: open
discovered: 2026-01-25
related: []
related_decision: null
report: null
---

# Search Results Tree Not Obviously Expandable

## Problem

When performing a search, results are displayed in a TreeView with:

- File → Sheet → Results found

The last row (the one with actual found occurrences) appears **collapsed** by default, but it's not visually evident that:

1. The row contains children (the occurrences)
2. You need to double-click (or expand) to see the results
3. It's possible to select individual occurrences

**UX Problem:**
Users don't immediately understand that they need to expand the last row to see and select search results.

## Analysis

**Current behavior:**

```
📄 File.xlsx
  📊 Sheet1
    🔍 3 results found   ← Looks like an informational row, not an expandable node
```

**Expected behavior:**

```
📄 File.xlsx
  📊 Sheet1
    ▶ 🔍 3 results found   ← Arrow indicates it's expandable
```

**Root cause:**
Missing a visual indicator (expander/chevron) showing that the node is expandable, similar to the one used for grouped columns in the rest of the application.

**Impact:**

- Confusion for new users
- Difficult discovery of result selection functionality
- UX inconsistent with the rest of the app (other TreeViews have visible expanders)

## Possible Solutions

- **Option A: Show expander/chevron like parent nodes** - Add standard ▶/▼ icon
  - Pro: Consistency with other TreeViews in the app
  - Pro: Familiar pattern for users
  - Pro: Minimal and standard solution
  - Con: None

- **Option B: Auto-expand results** - Always show results expanded
  - Pro: Zero clicks to see results
  - Con: With many files/sheets, the view becomes huge and hard to navigate
  - Con: Performance issues with hundreds of results

- **Option C: Add tooltip "Double-click to expand"**
  - Pro: Explicit guidance for the user
  - Con: Doesn't solve the visual problem
  - Con: Requires hover to discover functionality

## Recommended Approach

**Option A** - Show standard expander/chevron.

It's the simplest solution and consistent with the rest of the application. Users will immediately recognize the pattern.

Optionally, can be combined with **Option C** for first-time users.

## Notes

- Reported by user during v0.5.1 testing
- The problem specifically concerns `TreeSearchResultsView`
- The same pattern is used correctly in `ColumnsSidebarView` (grouped columns show the expander)

## Related Documentation

- **Code Locations**:
  - View: `src/SheetAtlas.UI.Avalonia/Views/TreeSearchResultsView.axaml`
  - ViewModel: `src/SheetAtlas.UI.Avalonia/ViewModels/TreeSearchResultsViewModel.cs`
  - Style TreeView: `src/SheetAtlas.UI.Avalonia/Styles/TreeView.axaml`
- **Reference**:
  - For examples of TreeViews with visible expanders, see `ColumnsSidebarView.axaml`

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
