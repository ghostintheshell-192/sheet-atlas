# UI Rework

**Status**: backlog
**Release**: unassigned (target: pre-v1.0, after all features complete)
**Priority**: should-have
**Depends on**: all other features (this is final polish)

## Summary

Final UI/UX polish pass before v1.0 release. Focus on user flow clarity, visual consistency, and modern appearance.

**Timing**: Do this LAST, after all features are implemented, so we know exactly what needs to be polished.

## User Stories

- As a new user, I want clear guidance on what to do after loading files
- As a user, I want intuitive navigation without hunting for features
- As a user, I want error details to be actionable (which cell? where?)
- As a user, I want a modern sidebar experience like VS Code

## Requirements

### 1. Onboarding & User Flow
- [ ] **Post-load guidance**: After loading files, guide user toward Search tab
  - Options: auto-open Search tab, highlight it, show tooltip, or inline prompt
- [ ] **Review tab structure**: Currently Search must precede Comparison (empty otherwise)
  - Consider: auto-open Search when files loaded?
  - Consider: hide Comparison tab until there's something to compare?
- [ ] **Feature discoverability**: How do users find normalization, templates, export?
  - Menu-driven vs tab-driven vs contextual (in FileDetails)?
  - TBD after all features implemented

### 2. File Details - Error Display Enhancement
- [ ] **Add cell location column** to error log table
  - Show "B5" or "R5C2" format for quick identification
  - Already have `CellReference` in `ExcelError`, just not displayed
- [ ] **Make location clickable?** (future: could highlight in a preview)
- [ ] Related: severity filter (separate spec: `file-details-severity-filter.md`)

### 3. Sidebar Redesign (VS Code style)
- [ ] **Icon buttons on edge** instead of full-height collapse button
  - Vertical icon strip (like VS Code activity bar)
  - Click icon = toggle that panel
- [ ] **Multiple sidebar sections?**
  - Files list (current)
  - Search? (or keep as tab)
  - Future: column filter sidebar
- [ ] **Collapse behavior**: Click active icon = collapse, click another = switch
- [ ] **Visual**: Icons should indicate which panel is active

### 4. General Polish (lower priority)
- [ ] Button hover/active states consistency
- [ ] Scrollbar styling
- [ ] Loading indicators refinement
- [ ] Subtle transitions/animations

### Non-Functional
- Performance: no impact from visual changes
- Accessibility: maintain/improve contrast ratios
- Consistency: unified design language across both themes

## Technical Notes

- Sidebar redesign may require new layout structure (Grid with icon strip + content area)
- Avalonia has good support for custom styling, but complex layouts need planning
- Consider: create mockups/sketches before implementing

## Open Questions

- [x] ~~Where should normalization/template features live?~~ → **Dedicated Templates tab** (extracted from FileDetails)
- [ ] Should Search tab auto-open after file load?
- [ ] Hide empty Comparison tab, or show placeholder with instructions?
- [ ] Icon set for sidebar — use existing Avalonia icons or custom?

## Decisions Made

### Templates tab (2025-12-10)
Template Management va estratto da FileDetails in un tab dedicato:
- FileDetails torna informativo (stato file, log errori, actions)
- Templates tab: lista template, create from file, validate, apply (futuro)
- Motivo: template management è operativo/complesso, FileDetails è informativo/passivo

## Acceptance Criteria

- [ ] New user can figure out what to do without guessing
- [ ] Error locations visible and clear in File Details
- [ ] Sidebar feels modern (VS Code-like toggle behavior)
- [ ] Both themes updated and consistent
- [ ] No usability regressions
