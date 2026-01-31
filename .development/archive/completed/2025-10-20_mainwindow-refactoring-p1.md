---
type: refactor
priority: medium
status: resolved
discovered: 2025-10-19
resolved: 2025-10-20
commits: [1b557e3, 46179ef, 85f9f1f, 0b56a6d, 02ceedb, e20060b, 9a6ec77]
related: []
---

# MainWindowViewModel Phase 1 Refactoring

## Problem

MainWindowViewModel was becoming a "God Class" with too many responsibilities:
- File management
- Tab navigation
- Search coordination
- Comparison management
- File details management
- ~500+ lines of code

**Impact:** Hard to test, difficult to maintain, violates Single Responsibility Principle

## Analysis

The ViewModel was handling multiple concerns that could be extracted into focused coordinator classes following event-driven architecture patterns.

## Solution Implemented

### Phase 1 Refactorings Completed

#### P0.1: Remove deprecated ShowSearchResultsCommand ✅
- Commit: 1b557e3
- Removed unused command

#### P0.2: Flat cache in SearchHistoryItem ✅
- Commit: 46179ef
- O(n³) → O(n) performance improvement
- Better selection state management

#### P0.3: Extract TabNavigationCoordinator ✅
- Commit: 85f9f1f
- Reduced MainWindowViewModel by 59 lines
- Centralized tab switching logic

#### P0.4: Extract FileDetailsCoordinator ✅
- Commit: 0b56a6d
- Reduced MainWindowViewModel by 67 lines
- Event-driven architecture (FileReloaded event)
- Fixed race condition in file retry
- Anti-flicker mechanism during retry

#### P1.5: Readability Improvements ✅
- Commits: 02ceedb, e20060b, 9a6ec77
- Simplified property propagation
- Extracted SelectedFile setter logic
- Better code organization

### Evaluated and Deferred

#### P1.2: FilePicker management
- **Decision**: Keep in MainWindowViewModel (correct architecture)
- FilePicker is a top-level UI concern

#### P1.3: MenuBar commands
- **Decision**: Keep in MainWindowViewModel (no extraction needed)
- Menu commands are ViewModel responsibility

#### P1.4: SelectionManager extraction
- **Decision**: Defer to Phase 2
- Requires full event-driven migration

## Alternatives Considered

- **Option A**: Extract everything immediately → Rejected: Over-engineering, breaks working code
- **Option B**: Incremental extraction (P0-P1) → **Selected**: Safer, testable progress
- **Option C**: Full rewrite → Rejected: Too risky

## Trade-offs

**Pros:**
- ~126 lines removed from MainWindowViewModel
- Better separation of concerns
- Event-driven architecture foundation
- Easier to test coordinators independently
- Improved code organization

**Cons:**
- More files to manage (acceptable trade-off)
- Slight increase in indirection

## Testing

**Regression testing:**
- Tab switching works correctly ✅
- File details update properly ✅
- File retry mechanism works ✅
- No flicker during retry ✅
- All events fire correctly ✅

## Impact

- MainWindowViewModel more maintainable
- Foundation for Phase 2 (full event-driven migration)
- Consistent coordinator pattern established
- Better code quality, easier to reason about

## Future Considerations

- Phase 2: Complete event-driven architecture migration
- Extract SelectionManager when event system is mature
- Consider coordinator pattern for other ViewModels
