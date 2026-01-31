# Vertical Comparison (Column-to-Column)

**Status**: backlog
**Release**: unassigned (target: post-v1.0)
**Priority**: nice-to-have
**Depends on**: foundation-layer.md, row-comparison.md

## Summary

Compare columns instead of rows, useful for time-series data or when data is organized vertically.

**Note**: Niche feature, not a personal need. Keeping for completeness and potential user requests.

## User Stories

- As a user, I want to compare "Q1 2023" column with "Q1 2024" column
- As a user, I want to see differences in vertical data layouts
- As a user, I want to switch between row and column comparison modes

## Requirements

### Functional
- [ ] Column Selection (via Search UI toggle)
  - [ ] Add toggle in Search: "Compare rows" (default) vs "Compare columns"
  - [ ] In column mode: search results show columns instead of rows
  - [ ] Select columns from results (same UX as row selection)
  - [ ] "Compare Selected Columns" action (reuses comparison view)
  - [ ] **Note**: Column filtering is SEPARATE — it reduces visible columns for row comparison, not for selecting columns to compare. Keep responsibilities distinct.

- [ ] Comparison View
  - [ ] Side-by-side column display
  - [ ] Same diff highlighting as row comparison
  - [ ] Row alignment (by position or by key column)
  - [ ] Statistics: match rate, difference count

- [ ] Alignment Options
  - [ ] By position (row 1 ↔ row 1)
  - [ ] By key column (match by ID/name column)
  - [ ] Manual adjustment if needed

### Non-Functional
- UX: intuitive switch between row/column modes
- Performance: same as row comparison

## Technical Notes

- Reuse comparison logic from row comparison
- May need transposition of data internally
- Key column alignment is similar to SQL join logic

## Open Questions

- [x] ~~How to select columns?~~ → Toggle in Search UI, same selection flow as rows
- [ ] How to select key column for alignment? (dropdown after selecting columns?)
- [x] ~~Same view or separate view from row comparison?~~ → Same view, reuse ComparisonView
- [ ] Can compare columns from different files? (probably yes, same as rows)

## Acceptance Criteria

- [ ] Can compare two columns visually
- [ ] Differences highlighted correctly
- [ ] Both alignment modes work
- [ ] Works across files
