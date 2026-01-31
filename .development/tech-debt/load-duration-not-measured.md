---
type: bug
priority: low
status: open
discovered: 2025-12-10
related: []
related_decision: null
report: null
---

# Load Duration Not Measured

## Problem

`LoadAttemptInfo.DurationMs` is always 0. The file loading time is never actually measured.

## Analysis

In `FileLogService.cs` or wherever `LoadAttemptInfo` is created, `DurationMs` is set to 0 or left at default. There's a TODO comment in the code acknowledging this.

The timing should be measured in `LoadedFilesManager` or the file reading service, wrapping the actual load operation with a `Stopwatch`.

## Possible Solutions

- **Option A**: Measure in LoadedFilesManager around ProcessLoadedFileAsync
- **Option B**: Measure in individual readers (XlsxReaderService, etc.)
- **Option C**: Add timing to ExcelFile entity itself

## Recommended Approach

**Option A** — Measure at the orchestration level in LoadedFilesManager. This captures the full user-perceived load time.

```csharp
var sw = Stopwatch.StartNew();
var excelFile = await _fileService.ReadFileAsync(path);
sw.Stop();
// Pass sw.ElapsedMilliseconds when creating FileLogEntry
```

## Notes

- Low priority — useful for diagnostics but not user-facing
- Could be shown in File Details for power users

## Related Documentation

- **Code Location**: `src/SheetAtlas.Core/Application/DTOs/LoadAttemptInfo.cs`
- **Code Location**: `src/SheetAtlas.UI.Avalonia/Managers/Files/LoadedFilesManager.cs`
