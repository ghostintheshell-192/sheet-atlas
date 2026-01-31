# Template Validation

**Status**: implemented
**Release**: v0.4.0 (released 2025-12-16)
**Priority**: must-have
**Depends on**: foundation-layer.md, settings-configuration.md, column-linking.md

## Implementation Progress (2025-12-10)

### Completed
- [x] **Tab Templates dedicato** — extracted from FileDetails into separate tab
- [x] **Template library UI** — left panel with list, refresh, import, delete
- [x] **Save as Template** — creates template from selected file
- [x] **Validate against Template** — single file validation
- [x] **Batch validation** — validate multiple files at once (Ctrl+Click to multi-select)
- [x] **Per-file results UI** — expandable results showing issues per file
- [x] **Template storage** — JSON in `{Documents}/SheetAtlas/Templates/`
- [x] **Global template library** — all templates visible, not filtered by file

### Remaining
- [ ] Edit column headers in template (via column-linking, see column-linking.md)
- [ ] Configurable validation rules (NotEmpty, Pattern, Range, List)
- [ ] .xltx/.xlt file filter support (minor)
- [ ] UI polish

---

## Summary

Save file structure as template (like Excel .xltx), then validate other files against it to ensure data quality and format consistency.

## User Stories

- As a user, I want to save a file's structure as a reusable template
- As a user, I want to ensure vendor files match my expected format
- As a user, I want to catch missing columns or wrong data types before analysis

## Core Principle

**SheetAtlas does not modify data or metadata** (except for normalization export). Templates are a **copy** of file structure without data.

**Exception: column headers in templates**

Users may rename column headers **in the template only** (original file is never modified). This enables semantic matching across files with different naming conventions.

Example:
```
Original file: "2016 Revenue" (immutable)
        ↓
   Save as Template
        ↓
Template: "2016 Revenue" → user renames → "Prior Year Revenue"
```

Rationale:
- Original file stays intact
- Template is already an abstraction
- Enables matching "2016 Revenue" (file A) with "2017 Revenue" (file B) as same semantic column

## Requirements

### Functional

#### Template Creation
- [ ] "Save as Template" action on loaded file
- [ ] Edit column headers in template (optional, for semantic naming)
- [ ] Template captures **everything Excel saves in .xltx/.xlt**:
  - [ ] Column structure (names, order, count)
  - [ ] Data types per column
  - [ ] Cell formatting (number formats, styles)
  - [ ] Conditional formatting rules
  - [ ] Formulas (will show N/A or error without data - that's OK)
  - [ ] Merged cells structure
  - [ ] Column widths, row heights
  - [ ] Sheet names and order
- [ ] Excluded (for now):
  - [ ] Actual cell data/values
  - [ ] Macros (nice-to-have for future)

#### Template Storage
- [ ] Save in `{output_folder}/templates/` directory
- [ ] Format: JSON (human-readable, versionable)
- [ ] Naming: user-provided name or derived from source file
- [ ] Template library: list saved templates in UI

#### Validation Rules (applied when validating against template)
- [ ] Structure validation:
  - [ ] Expected columns present
  - [ ] Column order matches (optional: strict or flexible)
  - [ ] Required vs optional columns
- [ ] Data validation per column:
  - [ ] NotEmpty - column must have values
  - [ ] DataType - must match expected type (date/number/text)
  - [ ] Pattern - regex match (e.g., email format)
  - [ ] Range - numeric min/max
  - [ ] List - allowed values only

#### Validation Process
- [ ] "Validate against Template" action
- [ ] Select template from library
- [ ] Generate ValidationReport with issues
- [ ] Show issues inline in UI (per cell/column)
- [ ] Severity levels: Error, Warning, Info

### Non-Functional
- UX: validation should be fast and non-blocking
- Flexibility: validation rules are optional, not mandatory
- **Implementation flexibility**: exact UI/UX to be refined based on user testing

## Excel Template Format Support (.xltx, .xlt)

SheetAtlas supports loading native Excel template files directly:

- **Supported formats**:
  - `.xltx` — XML-based template (Excel 2007+)
  - `.xlt` — Legacy binary template (Excel 97-2003)
- **Excluded**: `.xltm` (macro-enabled) — macros not relevant for template structure

**Use case**: user has an Excel template file and wants to use it directly as SheetAtlas template, without needing a populated .xlsx first.

**Implementation**: minimal — these formats are structurally identical to .xlsx/.xls, just add extensions to file filters.

**Workflow**:
1. User opens .xltx/.xlt file
2. File loads normally (empty or with sample data)
3. "Save as Template" works as with any other file

## Technical Notes

- Template format similar to .xltx internal structure but as JSON
- Reuse Foundation Layer for type detection during validation
- .xltx/.xlt are essentially .xlsx/.xls with different extension — reuse existing readers

## Open Questions (to resolve during implementation)

- [ ] Should formulas be included? (leaning yes, even if they show errors)
- [ ] How to handle template from multi-sheet file?
- [ ] UI for template library: sidebar, dialog, or dedicated view?
- [ ] Allow editing validation rules after template creation?

## Acceptance Criteria

- [ ] Can save any loaded file as template
- [ ] Template captures all structural metadata (no data)
- [ ] Can validate file against saved template
- [ ] Validation issues displayed with location and severity
- [ ] Templates persist in `templates/` folder
