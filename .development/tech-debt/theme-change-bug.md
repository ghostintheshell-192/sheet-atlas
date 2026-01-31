---
type: bug
priority: low
status: open
discovered: 2025-10-19
related: []
---

# Theme Change Bug in Search/Comparison Views

## Problem

After opening and closing RowComparison tab, theme changes don't update text colors in Search view until you switch tabs.

**Reproduction:**
1. Load files → Search → Create comparison
2. Change theme (first time) → Colors update ✅
3. Close comparison tab
4. Change theme (second time) → **Bug**: Text colors don't update ❌
5. Switch to FileDetails tab and back → Colors now correct ✅

## Analysis

### Root Cause
`ComparisonTypeToBackgroundConverter` creates new brushes on-the-fly but Avalonia doesn't know to re-evaluate bindings when global theme resources change. Tab switching forces re-render which picks up new theme.

### Workaround
Switch tabs to force refresh.

## Possible Solutions

- **Option A**: Implement theme-aware converter
  - Subscribe to theme change events
  - Trigger converter re-evaluation
  - Pro: Proper fix
  - Con: Additional complexity

- **Option B**: Converter caching strategy
  - Cache brushes per theme
  - Invalidate cache on theme change
  - Pro: Efficient, clean
  - Con: Requires infrastructure

- **Option C**: Document as known issue
  - Current approach
  - Pro: Simple
  - Con: User experience issue

## Recommended Approach

Related to event-driven architecture refactoring. May be resolved by theme-aware converter refactoring or converter caching strategy.

**Status**: Deferred to event-driven refactoring

## Notes

- Low priority - has workaround (tab switch)
- Will likely be resolved by architectural improvements
- Not critical for alpha release

## Status Verification (2025-11-28)

**Verified by code-reviewer agent**: Bug is **PARTIALLY FIXED**.

**Analysis**:
- RowComparisonViewModel: ✅ Correctly subscribes to `ThemeChanged` event, calls `RefreshCellColors()`
- SearchViewModel: ❌ Does NOT subscribe to theme changes
- ComparisonTypeToBackgroundConverter: Creates brushes on-the-fly based on `IsDarkMode()`
- Root cause: SearchViewModel doesn't trigger binding re-evaluation on theme change

**Current behavior**:
- RowComparison tab: Theme changes work ✅
- Search tab: Theme changes don't work until tab switch ❌

**Recommendation**: KEEP OPEN. Fix requires SearchViewModel (and other affected ViewModels) to subscribe to `IThemeManager.ThemeChanged` and trigger property refresh, similar to RowComparisonViewModel pattern.
