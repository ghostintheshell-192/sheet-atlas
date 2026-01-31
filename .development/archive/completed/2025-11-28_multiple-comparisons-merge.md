---
type: bug
priority: high
status: resolved
discovered: 2025-11-27
resolved: 2025-11-28
branch: feature/comparison-history-stack
commits: [bda7518, 2623c60]
related: ["Row Comparison Sync Bug"]
---

# Multiple Comparisons Merge Into Single Table

## Problem

When creating multiple row comparisons, all selected rows from ALL search history items were combined into a single comparison table instead of keeping each comparison separate.

**Reproduction:**
1. Load file → Search "term A" → Select 2 rows → Compare
2. Search "term B" → Select 2 different rows → Compare
3. **Bug**: Comparison table shows 4 rows (all selections from both searches merged)
4. Expected: Each comparison should be independent

## Analysis

### Root Cause
`TreeSearchResultsViewModel.RefreshSelectionCache()` collected selected items from the entire `SearchHistory` collection, not just the current search context.

**Code Location:**
```csharp
// TreeSearchResultsViewModel.cs - RefreshSelectionCache()
foreach (var searchItem in SearchHistory)  // ← Iterates ALL searches
{
    foreach (var fileGroup in searchItem.FileResultGroups)
    // ... collects ALL selected cells from ALL searches
}
```

## Solution Implemented

**Stack Collapsible UI (Option B)**

Implemented comparison history similar to search history:

1. **RowComparisonHistory** collection in MainWindowViewModel
2. Each comparison is a separate `RowComparisonViewModel` instance
3. UI shows collapsible stack of comparisons (like search results)
4. Each comparison preserves its context (search term, file, timestamp)

### Implementation Details

- Created reusable `CollapsibleSection` control in `/Controls/`
- Added `IsExpanded` property to `RowComparisonViewModel`
- Modified `MainWindow.axaml` to use `ItemsControl` over `RowComparisons` collection
- Each comparison displays in its own collapsible section with close button
- Consistent styling with SearchView (same colors, padding, behavior)
- Added `ClearSelection()` call after comparison creation to prevent accumulation

### Files Modified
- `src/SheetAtlas.UI.Avalonia/Controls/CollapsibleSection.axaml[.cs]` (new)
- `src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs`
- `src/SheetAtlas.UI.Avalonia/Views/MainWindow.axaml`
- `src/SheetAtlas.UI.Avalonia/Views/RowComparisonView.axaml`
- `src/SheetAtlas.UI.Avalonia/ViewModels/TreeSearchResultsViewModel.cs`

## Alternatives Considered

- **Option A**: Fix selection cache logic → Rejected: Doesn't solve UX issue of losing comparison history
- **Option B**: Stack collapsible UI → **Selected**: Consistent with search history pattern, preserves context
- **Option C**: Modal comparisons → Rejected: Poor UX, blocks workflow

## Trade-offs

**Pros:**
- Consistent UX pattern (matches search history)
- Preserves comparison history for review
- Independent comparisons don't interfere
- Reusable CollapsibleSection control created

**Cons:**
- Slightly more complex UI code
- Memory usage for multiple comparisons (acceptable)

## Testing

**Manual testing:**
- Create multiple comparisons from different searches ✅
- Expand/collapse individual comparisons ✅
- Close individual comparisons ✅
- Tab switching with multiple comparisons open ✅
- Clear selection after comparison creation ✅

**User feedback:**
- "Funziona benissimo! bravo Claude, bel lavoro!" (Valentina, 2025-11-28)

## Impact

- Bug fixed completely
- Improved UX with comparison history
- Created reusable control for future use
- Also used CollapsibleSection to refactor SearchView (demonstrated reusability)

## Future Considerations

- May partially resolve "Row Comparison Sync Bug" (documented separately)
- CollapsibleSection could be used in other views
- Comparison persistence (JSON) would build on this foundation
