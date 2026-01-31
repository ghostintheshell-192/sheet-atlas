# Enhanced File Logging

**Status**: archived
**Release**: n/a
**Priority**: n/a
**Archived**: 2025-12-10

## Archive Note

**Most of this spec is already implemented.** Archived after review session.

### Already done:
- ✅ Per-file history (JSON files per load attempt)
- ✅ In-app log viewer (File Details tab)
- ✅ Structured JSON format with schema
- ✅ CleanupOldLogsAsync method exists

### Extracted as separate TODOs:
- Call CleanupOldLogsAsync at app startup → tech-debt
- Add log retention setting to Settings UI → settings-configuration.md
- Fix DurationMs (always 0) → tech-debt
- Add severity filter to File Details → backlog enhancement

### Dropped (overkill for this app):
- Correlation IDs (file path is natural correlation)
- Search in logs
- Export logs

---

## Original Summary

Improve the structured file logging system with automatic cleanup, better UI integration, and per-file history tracking.

## User Stories

- As a user, I want to see the history of operations on each file
- As a user, I want old logs cleaned up automatically
- As a user, I want to understand why a file failed to load (historical context)

## Requirements

### Functional
- [ ] Automatic Log Cleanup
  - [ ] Configurable retention period (default: 30 days)
  - [ ] Cleanup runs on app startup
  - [ ] Option to disable cleanup
  - [ ] Setting in configuration

- [ ] Per-File History
  - [ ] Track load attempts per file
  - [ ] Show history in file details view
  - [ ] "Why did this fail last time?" context

- [ ] UI Integration
  - [ ] Log viewer in-app (not just external file)
  - [ ] Filter by severity, date, file
  - [ ] Search in logs
  - [ ] Export logs for support

- [ ] Improved Log Content
  - [ ] Structured JSON format
  - [ ] Correlation IDs for related operations
  - [ ] Performance metrics (load time, memory)

### Non-Functional
- Performance: logging should not impact app speed
- Storage: reasonable disk usage with cleanup
- Privacy: no sensitive file content in logs

## Technical Notes

- Existing notes: `ideas/structured-file-logging-1.md`
- Current implementation: IFileLogService with CleanupOldLogsAsync (not called)
- Consider: SQLite for structured queries vs flat files

## Open Questions

- [ ] In-app viewer or keep external file approach?
- [ ] SQLite vs JSON files for storage?
- [ ] What metrics are useful to track?

## Acceptance Criteria

- [ ] Old logs cleaned up automatically
- [ ] Per-file history accessible in UI
- [ ] Logs searchable and filterable
- [ ] Retention configurable in settings
