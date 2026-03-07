# Sheet Atlas - Current Status

*Last updated: 2026-03-07*

## Project Phase

**Current release**: v0.6.0 (alpha)
**Next release**: v0.7.0 - TBD

## Completed in v0.6.0

**v0.6.0** (2026-03-07):
- **DataRegion System** - Named data regions for scoped operations (ADR-009/010/011/012)
  - Core domain model, service integration, JSON persistence
  - Full UI: SheetGridCanvas with drag selection, TreeView sidebar, dedicated tab
  - Cross-file detection with header-anchored matching
- **Normalize & Export** - In-place normalization with column-level type correction
  - Copies original file, corrects values/formats based on dominant column type
  - Region-scoped normalization
- **QuickBar Toolbar** - Contextual toolbar with toggle from View menu
- **Coordinate-Preserving Storage** (ADR-013) - SASheetData preserves original Excel positions
- **Template Validation Scoped to DataRegion** (ADR-014)
- **Welcome Tab** - Getting-started guidance for new users
- **Reusable Components** - ClosableTabHeader, EmptyStateView

## Completed in v0.5.x

**v0.5.3** (2026-01-31):
- **Facade Pattern** - FileReaderContext to reduce constructor over-injection
- **Column Filtering in Export** - Column selection with semantic names
- **CSV Format Inference** - Auto-detection of formats (percentages, scientific, decimals)
- **Documentation** - ARCHITECTURE.md with Mermaid diagrams, ADR-008

**v0.5.2** (2026-01-28):
- **JSON Serialization** - Source-generated for PublishTrimmed support
- **CI Improvements** - Auto-set version from git tag

**v0.5.1** (2026-01-21):
- **Export Comparison Results** - Preserves numeric types and formats
- **Date formatting fix** - Comparison view was showing OLE serial numbers

**v0.5.0** (2026-01-18):
- **Settings UI** - Tab with user preferences
- **Column Filtering** - Checkboxes to filter columns in sidebar
- **Column Grouping Warnings** - Badges for case/type variations
- **Theme Fixes** - Theme persistence, system theme detection on Linux

See: `specs/implemented/` for completed specifications.

## In Progress / Next Steps

1. **Performance profiling** - Loading time increased with new enrichment steps
2. **Website screenshots** - UI changed significantly since v0.5.x

## Quick Links

| What | Where |
|------|-------|
| **Specs** | `specs/` |
| **Tech debt** | `tech-debt/` |
| **ADR** | `reference/decisions/` |
| **Architecture** | `docs/project/ARCHITECTURE.md` |

## Methodology

**Spec-Driven Development**: each feature has a dedicated specification in `specs/`.
- `specs/implemented/` - working features
- `specs/planned/` - confirmed for upcoming releases
- `specs/backlog/` - validated but not scheduled
