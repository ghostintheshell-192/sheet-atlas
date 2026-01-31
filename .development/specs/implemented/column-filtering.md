# Column Filtering

**Status**: implemented
**Release**: v0.5.0
**Priority**: should-have
**Depends on**: foundation-layer.md, column-linking.md

## Summary

~~Sidebar for selecting which columns to include in search/comparison/export.~~ Add filtering capability to the existing Columns sidebar (from column-linking). Checkbox selection to include/exclude columns from search/comparison/export.

## Implementation Notes (2025-01-14)

**Sidebar already exists** via `column-linking.md` (ColumnsSidebarView). This spec adds a filtering layer on top:
- Current: sidebar shows columns with grouping/linking capabilities
- New: add checkboxes to include/exclude columns from operations

The UI structure (TreeView, expand/collapse) is already done. This spec adds:
1. Checkbox selection per column/group
2. "Select All / Clear / Invert" quick actions
3. Filtering logic in search/comparison/export

## User Stories

- As a user, I want to focus on specific columns during analysis
- As a user, I want to hide irrelevant columns from search/comparison
- As a user, I want my column selection to affect exports

## Requirements

### Functional

#### Column Filter ~~Sidebar~~ Layer
- [ ] ~~Collapsible sidebar (basic styling, refined in UI rework)~~ *Sidebar exists*
- [ ] ~~TreeView: File > Sheet > Columns~~ *TreeView exists (column-based, not file-based)*
- [ ] Checkbox selection (three-state for parents/groups)
- [ ] Quick actions: Select All, Clear, Invert
- [ ] Filter affects:
  - [ ] Search results
  - [ ] Comparison results
  - [ ] Exports

#### Visual Feedback
- [ ] Badge showing "X of Y columns selected"
- [ ] ~~Filtered-out columns grayed out in main view~~ *Reconsider: may conflict with highlighting*

### Non-Functional
- UX: filtering should be fast and non-disruptive
- ~~UI: basic/functional first, polished in UI rework phase~~ *Sidebar polish already done*

## Technical Notes

- ~~Sidebar is basic implementation, visual polish deferred to UI rework~~ *Sidebar is done*
- ~~Column list shared with Column Grouping feature (separate spec)~~ *Column list is from ColumnLinkingViewModel*
- Add `IsIncluded` property to `ColumnLinkViewModel`
- Filtering service to propagate selection to search/comparison/export

## Acceptance Criteria

- [ ] ~~Sidebar shows all columns hierarchically~~ *Already done via column-linking*
- [ ] Checkboxes allow selecting/deselecting columns
- [ ] Selection filters search/comparison/export results
- [ ] Visual feedback shows what's selected
