---
type: bug
priority: medium
status: open
discovered: 2025-10-19
related: []
---

# Row Comparison Sync Bug

## Problem

Bidirectional synchronization issue between Search tab and RowComparison tab.

**Reproduction:**

1. Select cells in Search tab
2. Create row comparison → RowComparison tab shows selected cells
3. Click "Clear Selection" in Search tab
4. **Bug**: RowComparison tab still shows old selection (doesn't update)

**Current behavior that works:**

- Closing RowComparison tab with X → Search selections are cleared ✅

## Analysis

### Root Cause

`RowComparisonViewModel` is a static snapshot created at comparison time. It doesn't subscribe to selection changes from `TreeSearchResultsViewModel`.

### Related Work

- Code review report: `.personal/archive/analysis/2025-10-19_report_code-reviewer.md`
- Refactoring branch: `refactor/implement-idisposable-pattern` (if exists)

## Possible Solutions

- **Option A**: Keep as snapshot (add UX note that it's historical)
  - Pro: Simple, matches historical data pattern
  - Con: Might confuse users

- **Option B**: Implement bidirectional sync (complex, requires event subscriptions)
  - Pro: Always in sync
  - Con: Complex implementation, memory overhead

- **Option C**: Auto-close comparison when selection cleared (quick fix, intuitive)
  - Pro: Simple, intuitive
  - Con: Might be unexpected behavior

## Recommended Approach

Wait for event-driven architecture refactoring (Priority C in refactoring plan). This will likely resolve the issue at the architectural level.

**Status**: Deferred to event-driven refactoring

## Notes

- Not critical - has workaround (close and recreate comparison)
- Will be addressed as part of larger architectural improvement
- Low user impact

## Status Verification (2025-11-28)

**Verified by code-reviewer agent**: Bug is **CONFIRMED PRESENT**.

**Analysis**:

- TreeSearchResultsViewModel: Has `ClearSelection()` but does NOT notify RowComparisonViewModel
- RowComparisonViewModel: Receives static snapshot, NO event subscription to search selection changes
- MainWindowViewModel: Only connects close comparison → clear search (one direction)
- Missing: clear search → update/close comparison (opposite direction)

**Root cause**: No bidirectional event subscription between ViewModels. RowComparisonViewModel is isolated snapshot.

**Recommendation**: KEEP OPEN. Valid architectural issue. Consider implementing IRowComparisonCoordinator mediator pattern for bidirectional sync, or auto-close comparison when source selection cleared.

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
