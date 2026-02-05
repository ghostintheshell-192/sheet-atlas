# Multi-Row Headers

**Status**: implemented
**Release**: v0.5.x
**Priority**: should-have
**Depends on**: settings-configuration.md

## Summary

Support Excel files where headers span multiple rows, via a global setting for default header row count.

This is the **simple** solution for the common case (80%+ of files). For complex scenarios (data regions, mixed sheets), see `data-region-selection.md`.

## User Stories

- As a user, I want to set a default header row count (1, 2, or 3) in Settings
- As a user, I want files with 2-row headers to load correctly without manual intervention
- As a user, I want concatenated header text (e.g., "Q1" + "Revenue" = "Q1 Revenue")

## Requirements

### Functional

- [x] Global Setting for Header Row Count
  - [x] Add to Settings: "Default header rows: 1 / 2 / 3 / Custom"
  - [x] Default value: 1 (current behavior)
  - [x] Apply to all newly loaded files

- [x] Header Concatenation
  - [x] When HeaderRowCount > 1, concatenate header cells vertically
  - [x] Separator option: space (default), hyphen, or newline
  - [x] Handle empty cells gracefully (skip in concatenation)

- [x] Visual Indicator
  - [x] Show header row count in file details panel
  - [x] Display concatenated column names

### Non-Functional

- Backward compatible: HeaderRowCount=1 is the default (no change for existing users)
- Simple UX: just a dropdown in Settings

## Technical Notes

- Architecture already supports `HeaderRowCount` property in SASheetData
- `SetHeaderRowCount()` method exists, needs to be called during file load
- Search and Compare already skip rows < HeaderRowCount
- See ADR: `002-row-indexing-semantics.md`

## Out of Scope (see data-region-selection.md)

- Per-file override (Fase 2)
- Column boundaries (Fase 2)
- Multiple data regions per sheet (Fase 3)
- UI canvas for visual selection (Fase 2)

## Acceptance Criteria

- [x] Setting exists in Settings panel
- [x] Files with 2-row headers load correctly when setting = 2
- [x] Concatenated headers displayed in column names
- [x] Search correctly skips all header rows
- [x] Comparison uses correct data rows
