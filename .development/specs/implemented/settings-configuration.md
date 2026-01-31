# Settings & Configuration

**Status**: implemented
**Release**: v0.5.0
**Priority**: should-have
**Depends on**: none

## Implementation Status (2025-01-14)

**Not started.** Current app uses hardcoded defaults and `appsettings.json` for internal config only. No user-facing settings dialog exists.

Related code:
- `AppSettings.cs` — internal config (performance, logging, foundation layer), not user preferences
- Theme toggle in menu — hardcoded, not persisted
- Menu item "Settings" — exists but `IsEnabled="False"`

## Summary

Centralized settings UI for user preferences, with persistent storage and menu access.

## User Stories

- As a user, I want to access settings from the menu
- As a user, I want my preferences saved between sessions
- As a user, I want to set default behaviors (theme, header rows)

## Requirements

### Functional

#### Phase 1 (Initial Release)
- [ ] Menu access: `File > Settings` (or `Edit > Preferences`)
- [ ] Settings dialog with basic options
- [ ] Persistent storage (JSON in user's app data folder)
- [ ] Initial settings:
  - [ ] Theme preference (Light / Dark / System)
  - [ ] Default header row count (1, 2, 3, custom)
  - [ ] Default output folder (for exports, normalized files, etc.)
  - [ ] Default export format for reports (CSV / Excel)
  - [ ] Normalized file naming pattern (preset options):
    - `{filename}.{ext}` (sovrascrive, per sostituzione diretta)
    - `{date}_{filename}.{ext}` (con data ISO, evita collisioni)
    - `{filename}_{date}.{ext}` (data come suffisso)
    - `{date}_{time}_{filename}.{ext}` (timestamp completo)

#### Phase 2 (Post UI Rework)
- [ ] Gear icon in sidebar (when sidebar implemented)
- [ ] Additional settings:
  - [ ] Telemetry opt-in/out (when telemetry implemented)
  - [ ] Font size
  - [ ] Language (when i18n implemented)

### Non-Functional
- UX: Settings discoverable via standard menu location
- Storage: Human-readable JSON at `%AppData%/SheetAtlas/settings.json` (Windows) or equivalent

## Technical Notes

- **Decision**: Menu-only for initial release, gear icon added with UI rework
- Settings service: `ISettingsService` with Load/Save methods
- JSON schema versioned for future migrations
- Default values if settings file missing or corrupted

## UI Mockup (Phase 1)

```
┌───────────────────────────────────────────────┐
│ Settings                                  [X] │
├───────────────────────────────────────────────┤
│                                               │
│ Appearance                                    │
│ ┌───────────────────────────────────────────┐ │
│ │ Theme:  [Light ▼]                         │ │
│ └───────────────────────────────────────────┘ │
│                                               │
│ Data Processing                               │
│ ┌───────────────────────────────────────────┐ │
│ │ Default header rows: [1 ▼]                │ │
│ │ Export format:       [Excel ▼]            │ │
│ │ Normalized naming:   [{filename}.{ext} ▼] │ │
│ └───────────────────────────────────────────┘ │
│                                               │
│ File Locations                                │
│ ┌───────────────────────────────────────────┐ │
│ │ Output folder: [~/Documents/SheetAtlas ▼] │ │
│ │                              [Browse...]  │ │
│ └───────────────────────────────────────────┘ │
│                                               │
│                        [Cancel]  [Save]       │
└───────────────────────────────────────────────┘
```

## Acceptance Criteria

- [ ] Settings accessible from File menu
- [ ] Theme preference persists after restart
- [ ] Header row default applied to new file loads
- [ ] Default output folder used by export functions
- [ ] Graceful handling of missing/corrupt settings file
