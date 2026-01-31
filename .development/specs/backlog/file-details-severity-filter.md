# File Details - Severity Filter

**Status**: backlog
**Release**: unassigned
**Priority**: nice-to-have
**Depends on**: none (File Details tab already exists)

## Summary

Add a filter dropdown to the File Details error log table to filter by severity level (Critical, Error, Warning, Info).

## User Stories

- As a user, I want to filter errors by severity to focus on critical issues first
- As a user, I want to hide Info messages when troubleshooting errors

## Requirements

### Functional
- [ ] Add filter dropdown above error log table
- [ ] Options: All, Critical, Error, Warning, Info
- [ ] Default: All (show everything)
- [ ] Filter applies instantly (no button needed)
- [ ] Show count per severity in dropdown (e.g., "Error (3)")

### Non-Functional
- Performance: instant filtering (client-side, data already loaded)
- UX: consistent with other filter patterns in the app

## Technical Notes

- `ErrorLogs` ObservableCollection already has `LogLevel` per row
- Use CollectionView with filter predicate, or filtered ObservableCollection
- `LogLevelColor` already exists for visual distinction

## Acceptance Criteria

- [ ] Dropdown visible in File Details tab
- [ ] Filtering works correctly for each level
- [ ] "All" shows everything
- [ ] Filter state resets when switching files (or persists — TBD)
