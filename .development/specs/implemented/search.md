# Search

**Status**: implemented
**Release**: v0.2.0
**Priority**: must-have

## Summary

Search across all loaded files with hierarchical results display, enabling users to find specific data quickly.

## User Stories

- As a user, I want to search text across all loaded files at once
- As a user, I want to see results grouped by file and sheet
- As a user, I want to select specific results for comparison

## Requirements

### Functional
- [x] Full-text search across all cells
- [x] Case-insensitive search
- [x] Results grouped in TreeView (File > Sheet > Results)
- [x] Show cell address and context for each match
- [x] Select individual results for comparison
- [x] Clear selection per search or globally
- [x] Multiple concurrent searches (search history)

### Non-Functional
- Performance: <1 second for typical datasets

## Technical Notes

- TreeSearchResultsView with hierarchical grouping
- CollapsibleSection for search history management
- Selection state managed per-search

## Acceptance Criteria

- [x] Search finds all matching cells
- [x] Results navigable via keyboard
- [x] Selection persists across searches
