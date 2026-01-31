# Theme System

**Status**: implemented
**Release**: v0.2.0
**Priority**: should-have

## Summary

Light and dark theme support with consistent styling across all UI components.

## User Stories

- As a user, I want to switch between light and dark themes
- As a user, I want the app to respect my system preference

## Requirements

### Functional
- [x] Light theme with professional appearance
- [x] Dark theme optimized for extended use
- [x] Theme toggle in UI
- [x] Consistent colors across all components
- [x] Theme-aware comparison colors

### Non-Functional
- Accessibility: sufficient contrast ratios

## Technical Notes

- Avalonia theme resources
- DynamicResource for runtime switching
- See tech-debt: `theme-change-bug.md` (minor issue with workaround)

## Acceptance Criteria

- [x] Both themes fully styled
- [x] No visual glitches when switching
- [x] Comparison colors readable in both themes
