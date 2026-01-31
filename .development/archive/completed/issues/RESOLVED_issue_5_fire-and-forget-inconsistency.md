# Issue #4: Fire-and-Forget Inconsistency

**Priority**: HIGH
**Files**: LoadedFilesManager.cs (lines 368, 208)
**Status**: ✅ RESOLVED (2025-10-22)

## Problem
Initial file load uses fire-and-forget for SaveFileLogAsync, but retry uses blocking await. Creates race condition where UI reads logs before they're saved.

## Current Code
```csharp
// Initial load (line 368)
_ = Task.Run(async () => await SaveFileLogAsync(excelFile));

// Retry (line 208)
await SaveFileLogAsync(reloadedFile);
```

## Fix Applied
Made both paths consistent by awaiting SaveFileLogAsync before triggering events.

## Implementation Details
1. **AddFileToCollectionCore**: Changed from void → async Task
2. **SaveFileLogAsync**: Moved BEFORE FileLoaded event trigger
3. **Added skipLogSave parameter**: Prevents duplicate saves in retry scenario
4. **Optimized retry sequence**: Save log BEFORE removing old file to minimize UI flicker
5. **Added OnFileReloaded handler**: Auto-selects reloaded file and shows FileDetails tab
6. **Preserved selection during retry**: OnFileRemoved skips deselect when isRetry=true

## Retry Sequence Optimization
**Before (with flicker):**
- Remove file → deselect
- Load new file (1-2 sec) ← gap
- Save log (1 sec) ← gap
- Add file → reselect

**After (no flicker):**
- Load new file (1-2 sec) ← old file visible
- Save log (1 sec) ← old file visible
- Remove + Add (instant)
- Auto-reselect

## Impact
- ✅ No race condition: Log always saved before event
- ✅ No duplicate saves: Single save per file
- ✅ Smooth retry UX: Minimal flicker during retry
- ✅ Auto-selection: File automatically selected after retry
- ✅ All 193 tests passing
