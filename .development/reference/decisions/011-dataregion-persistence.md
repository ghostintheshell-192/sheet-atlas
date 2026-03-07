# Decision 011: DataRegion Persistence

**Date**: February 2026
**Status**: Proposed
**Impact**: medium
**Related**: data-region-selection spec, ADR-009 (data model)
**Summary**: DataRegions persisted as JSON files in dedicated subfolder under file-specific storage. Path: ~/.local/share/SheetAtlas/<file-hash>/dataregions/regions.json

## Context

Users define DataRegions manually via canvas selection. These definitions need to persist across sessions so users don't have to redefine them every time they open a file.

Questions:

1. Where to store region definitions?
2. What format?
3. How to handle file modifications?

## Decision

### 1. Storage Location

Regions are stored in a dedicated subfolder within the existing per-file storage structure:

```
~/.local/share/SheetAtlas/
  └── <filename>-<hash>/
      ├── logs/
      │   └── session-2026-02-03.log
      └── dataregions/           ← NEW
          └── regions.json
```

**Why this location**:

- Follows existing pattern (logs are stored per-file with hash)
- Hash ensures uniqueness if same filename exists in different locations
- Dedicated subfolder allows for future versioning/history

**Alternatives considered**:

| Location | Pro | Contra |
| -------- | --- | ------ |
| Inside Excel file (custom properties) | Portable | Modifies original file |
| Global config file | Simple | Doesn't scale with many files |
| Session only (memory) | Simplest | Lost on close |
| Per-file JSON (chosen) | Persistent, non-invasive | Extra files to manage |

### 2. JSON Format

```json
{
  "version": 1,
  "lastModified": "2026-02-03T10:30:00Z",
  "sheets": {
    "Data": {
      "regions": {
        "Sales Data": {
          "headerStartRow": 0,
          "headerEndRow": 0,
          "dataStartRow": 1,
          "dataEndRow": 99,
          "startColumn": 0,
          "endColumn": 5
        },
        "Summary": {
          "headerStartRow": 105,
          "headerEndRow": 105,
          "dataStartRow": 106,
          "dataEndRow": 120,
          "startColumn": 0,
          "endColumn": 3
        }
      }
    },
    "Sheet2": {
      "regions": {}
    }
  }
}
```

**Structure**:

- `version`: Schema version for future migrations
- `lastModified`: Timestamp for debugging/auditing
- `sheets`: Dictionary keyed by sheet name
- `regions`: Dictionary keyed by region name (matches in-memory model)

**What is NOT persisted**:

- **ColumnMetadata**: Recalculated on load from the Excel file. Avoids stale data if file is modified externally, and the computational cost is negligible.
- **IsAutoDetected**: Not relevant for Phase 2 (auto-detection excluded). Can be added to the schema when needed without breaking existing files (schema version handles migration).

### 3. File Modification Handling

If user modifies the original Excel file and reloads:

- Existing regions.json is preserved
- On load, regions are validated against new file structure
- Invalid regions (out of bounds) trigger warning, not deleted automatically
- User decides: adjust region or remove

**Why dedicated subfolder** (`dataregions/` not just `regions.json`):

- User might modify file, reload, change regions multiple times
- Future: could store region history/versions
- Clear separation from logs (diagnostic) vs regions (configuration)

## Rationale

- **Per-file storage**: Regions are file-specific, storage should match
- **JSON format**: Human-readable, easy to debug, standard
- **Dedicated subfolder**: Room for growth, clear organization
- **Don't modify original**: Non-invasive, user's file untouched

## Consequences

- New service: `DataRegionPersistenceService`
- Load regions on file open (if regions.json exists)
- Save regions on user action (define/modify/delete region)
- Handle missing/corrupted regions.json gracefully
- Future feature: Export regions as standalone file (user chooses location)

## Future Considerations

- **Export regions**: User might want to share region definitions
- **Import regions**: Apply saved regions to similar files
- **Region templates**: Predefined regions for common file structures (separate from ExcelTemplate)

## Related Decisions

- See `data-region-selection-progress.md` for full discussion (Q16)
- ADR-009: Data model (Dictionary structure matches JSON structure)
