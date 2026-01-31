# Sheet Atlas - Current Status

*Last updated: 2026-01-31*

## Project Phase

**Current release**: v0.5.3 (alpha)
**Next release**: v0.6.0 - Data Regions

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

1. **Data Region Selection** - Handle complex sheets with mixed content
2. **Multi-row Headers** - Support for multi-row headers common in financial reports

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
