---
type: bug
priority: low
status: open
discovered: 2025-12-10
related: []
related_decision: null
report: null
---

# Log Cleanup Never Called

## Problem

`FileLogService.CleanupOldLogsAsync()` exists but is never invoked. Log files accumulate indefinitely in `%LOCALAPPDATA%/SheetAtlas/Logs/Files/`.

## Analysis

The method is fully implemented:

- Takes `retentionDays` parameter (default 30)
- Deletes JSON files older than cutoff
- Removes empty directories
- Returns count of deleted files

But no code calls it — not at startup, not periodically, not anywhere.

## Possible Solutions

- **Option A**: Call at app startup (App.axaml.cs OnStartup) - Simple, one-time cleanup per session
- **Option B**: Call periodically (background timer) - More thorough but adds complexity
- **Option C**: Call on-demand (button in Settings) - User control but may never be used

## Recommended Approach

**Option A** — Call once at startup. Simple, effective, no user action needed.

```csharp
// In App.axaml.cs initialization
var logService = services.GetRequiredService<IFileLogService>();
_ = logService.CleanupOldLogsAsync(30); // fire-and-forget, don't block startup
```

## Notes

- Retention days should eventually be configurable via Settings (see settings-configuration.md)
- Low priority because disk space impact is minimal (JSON files are small)

## Related Documentation

- **Code Location**: `src/SheetAtlas.Core/Application/Services/FileLogService.cs:CleanupOldLogsAsync()`
- **Related Spec**: `.personal/specs/archived/enhanced-file-logging.md`

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
