# Column Grouping (Semantic)

**Status**: implemented
**Release**: v0.4.0 (core), v0.5.0 (UI refinements)
**Priority**: should-have
**Depends on**: ~~column-filtering.md~~, column-linking.md

## Summary

Group semantically equivalent columns across files (e.g., "2016 Revenue" ≈ "2017 Revenue" → "Prior Year Revenue"). Runtime experimentation, then save to template if desired.

**Note**: This feature uses the Column Linking foundation (see `column-linking.md`). The linking mechanism is shared with Template Management — this spec focuses on the runtime workflow and UI integration.

## Implementation Status (2025-01-14)

**Most of this spec is already implemented via `column-linking.md`.** The Column Linking feature in v0.4.0 provides the runtime grouping capability. What remains is the "Save to Template" integration and optional auto-suggestions.

## User Stories

- As a user, I want to mark "2016 Revenue" and "2017 Revenue" as the same semantic column
- As a user, I want to experiment with groupings before committing
- As a user, I want to save my groupings to a template for reuse

## Workflow

```
1. Load files → columns appear in sidebar ~~(from column-filtering)~~ (from column-linking)
        ↓
2. Create groups at runtime (experimentation)
   - Manual: drag columns together or use dialog ✅ DONE
   - Auto-suggest: system proposes similar columns (nice-to-have)
        ↓
3. Test: see how grouping affects comparison ✅ DONE
        ↓
4. Happy? → Save to template (optional) ⏳ PENDING
   - Goes into template's column header definitions
   - Reusable across sessions
```

## Requirements

### Functional

#### Runtime Grouping
- [x] Create group from selected columns *(via drag & drop merge in ColumnsSidebar)*
- [x] Name the group (e.g., "Prior Year Revenue") *(via Rename... context menu)*
- [x] Visual indicator: grouped columns show group name *(badge with count, expandable tree)*
- [x] Ungroup action *(via Ungroup context menu)*
- [x] Groups affect comparison (grouped columns compared together) *(RowComparisonView integration)*

#### Auto-Suggestions (nice-to-have)
- [ ] Detect similar column headers across files
- [ ] Suggest groupings (non-intrusive)
- [ ] Algorithms:
  - [ ] Fuzzy string matching
  - [ ] Year/date pattern extraction
  - [ ] Prefix/suffix matching

#### Save to Template
- [ ] "Save groups to template" action
- [ ] Groups become renamed headers in template
- [ ] Links to template-validation feature

### Non-Functional
- [x] UX: grouping should feel like experimentation, not commitment
- [ ] Discovery: suggestions helpful but not annoying *(deferred with auto-suggestions)*

## Relationship with Other Features

| Feature | Relationship |
|---------|-------------|
| ~~Column Filtering~~ | ~~Shares column list UI, but different action~~ *Column Filtering will add checkbox layer on top of existing sidebar* |
| Column Linking | **Provides the implementation** — this spec is now a subset |
| Template Validation | Groups can be saved as renamed headers in template |

## Technical Notes

- ~~Same sidebar as column-filtering (shared UI)~~ *Sidebar is ColumnsSidebarView from column-linking*
- Uses `ColumnLink` structure from column-linking.md ✅
- Group = temporary `ColumnLink` until saved to template ✅
- Auto-grouping provided by `IColumnLinkingService.CreateInitialGroups()` ✅
- Auto-suggest (fuzzy matching) is optional enhancement on top

## Acceptance Criteria

- [x] Can create/name/delete groups at runtime
- [x] Grouped columns treated as equivalent in comparison
- [ ] Can save groups to template
- [x] Groups don't persist unless explicitly saved *(runtime only, no persistence yet)*
