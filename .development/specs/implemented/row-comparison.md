# Row Comparison

**Status**: implemented
**Release**: v0.2.0
**Priority**: must-have

## Summary

Compare rows between files/sheets with visual diff highlighting, showing matches, differences, and structural issues.

## User Stories

- As a user, I want to compare selected rows side-by-side
- As a user, I want to see which cells differ between rows
- As a user, I want warnings when comparing structurally different data

## Requirements

### Functional
- [x] Compare rows from search results
- [x] Side-by-side visual diff display
- [x] Color coding: match (neutral), different (red gradient), new (green), missing (red)
- [x] Intensity gradient based on difference frequency
- [x] Structural warnings (missing headers, position mismatches)
- [x] Multiple comparisons in collapsible stack
- [x] Close individual comparisons

### Non-Functional
- Performance: instant for typical row sizes

## Technical Notes

- ComparisonView with CollapsibleSection
- ComparisonTypeToBackgroundConverter for color coding
- RowComparison service in Core layer

## Acceptance Criteria

- [x] Differences clearly visible
- [x] Can compare rows from different files
- [x] Structural issues don't crash comparison
