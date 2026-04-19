# ADR-015: Non-Destructive File Removal

**Status**: Active
**Date**: 2026-04-19 (documenting pre-existing behavior)
**Impact**: reference
**Summary**: "Remove from List" and "Clean All Data" are two distinct UI operations with opposite semantics. Removal from the sidebar is non-destructive and preserves search results, comparisons, and the underlying `ExcelFile`. Full disposal (including memory reclamation) only happens via the explicit "Clean All Data" path.

## Context

A user who loads multiple Excel files and performs cross-file analysis (searches spanning several files, row comparisons between files) builds up derivative state that references the loaded `ExcelFile` instances: entries in `TreeSearchResultsViewModel`, open `RowComparisonViewModel` instances, selected regions.

Two conflicting needs emerged:

1. **Decluttering**: after running analyses, the user may want to hide a file from the left sidebar without losing the analytical artifacts already produced from it (search hits, saved comparisons).
2. **Memory reclamation**: `ExcelFile` holds large `SASheetData` arrays (100–500 MB for typical workbooks) which land in the .NET Large Object Heap. Disposing them requires explicit action plus aggressive GC to actually return memory to the OS.

A single "Remove" operation cannot serve both. Collapsing them means either leaking analytical context (if removal disposes) or silently holding half a gigabyte of RAM per hidden file (if removal does not dispose).

## Decision

Expose two separate commands in the UI with deliberately asymmetric semantics:

### "Remove from List" — non-destructive

- Handler: `OnRemoveFromListRequested` → `ILoadedFilesManager.RemoveFile(file)`.
- Removes the file from the sidebar collection and raises `FileRemoved`.
- **Does not** call `file.Dispose()`.
- Search results and row comparisons that reference the file's `ExcelFile` remain valid and accessible.
- The `ExcelFile` stays alive as long as any derivative object holds a reference to it.
- Used for: "Remove from List" menu entry, failed-load notification dismissal (`OnRemoveNotificationRequested`), retry flow (which re-loads the file).

### "Clean All Data" — destructive

- Handler: `OnCleanAllDataRequested` (in `MainWindowViewModel.EventHandlers.cs`) and equivalently `FileDetailsCoordinator.HandleCleanAllData`.
- Sequence:
  1. Clear `SelectedFile` if it matches.
  2. `TreeSearchResultsViewModel.RemoveSearchResultsForFile(file.File)`.
  3. `SearchViewModel.RemoveResultsForFile(file.File)`.
  4. `_comparisonCoordinator.RemoveComparisonsForFile(file.File)`.
  5. `file.Dispose()` — disposes the underlying `ExcelFile` and its `SASheetData` arrays.
  6. `_filesManager.RemoveFile(file)` — removes from sidebar.
  7. Aggressive GC (`GCLargeObjectHeapCompactionMode.CompactOnce` + two full `GC.Collect` passes) on the thread pool, to force LOH compaction and return memory to the OS.
- `UnloadAllFilesAsync` reuses this exact path for every file, guaranteeing single/bulk operations share one cleanup sequence.

## Rationale

- **User model fidelity**: the sidebar is a navigation surface, not a data-lifetime declaration. Hiding a file should not invalidate results the user has already produced and may still be consulting.
- **Explicit memory reclamation**: disposal is expensive (LOH compaction, blocking GC). Tying it to an explicit user action prevents surprise latency on routine operations.
- **Single cleanup path**: both the per-file "Clean All Data" menu action and the bulk "Unload All" command funnel through `OnCleanAllDataRequested`, keeping the destructive sequence in exactly one place.

## Consequences

### Positive

- Analytical context survives casual sidebar cleanup — aligns with how users actually work (load many, explore, narrow down).
- Memory reclamation is predictable and user-triggered, not a side effect of navigation.
- `ExcelFile` lifetime is governed by reachability from live UI artifacts, which is the correct invariant.
- Bulk and single cleanup share one code path, so fixes and improvements apply uniformly.

### Negative

- Memory footprint of hidden-but-not-cleaned files can grow if the user removes many files via "Remove from List" without ever invoking "Clean All Data". Acceptable for a local desktop tool; would be a concern in a server context.
- The two commands must be clearly labelled in the UI; a user who expects "Remove" to also free memory will be surprised.
- External code reviewers (human or automated) routinely flag `RemoveFile` as a missing-Dispose bug. Mitigation: XML doc comments on both methods reference this ADR.

## Alternatives Considered

1. **Single "Remove" command that disposes**: rejected. Destroys open search results and comparisons silently; breaks the user's mental model of the sidebar as a view, not a lifetime.
2. **Weak references from search/comparison to ExcelFile**: rejected. Adds complexity, defers errors to the moment the user clicks an orphaned result, produces worse UX than the current explicit split.
3. **Automatic cleanup on sidebar removal with in-UI "keep alive" toggle**: rejected. Extra cognitive load for the common case; no clear default.
4. **Reference counting on ExcelFile**: rejected. The current implicit reachability model already achieves the same effect via the GC; explicit refcounts would add a whole class of bugs (forgotten release) for no observable benefit.

## Related

- `ILoadedFilesManager.RemoveFile` — non-destructive entry point.
- `FileDetailsCoordinator.HandleCleanAllData` — destructive entry point.
- `MainWindowViewModel.EventHandlers.cs` — `OnCleanAllDataRequested` implementation with aggressive GC.
- `MainWindowViewModel.FileOperations.cs` — `UnloadAllFilesAsync` reuses the destructive path.
- ADR-001: Error Handling Philosophy (disposal happens eagerly on explicit user intent, not implicitly).
