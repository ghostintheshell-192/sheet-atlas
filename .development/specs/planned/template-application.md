# Template Application

**Status**: planned
**Release**: TBD
**Priority**: should-have
**Depends on**: foundation-layer.md, template-validation.md, column-filtering.md, data-region-selection.md

## Summary

Apply a template to files, generating new standardized versions. Includes both **data normalization** (types, formats) and **structural normalization** (column mapping, ordering).

## Use Cases

1. **Vendor standardization**: 10 files from different vendors → all in MY internal format
2. **Ad-hoc cleanup**: files created "by eye" by users, all slightly different → standardized
3. **Format migration**: old format → new format with different column structure

## Core Principles

- **Non-destructive**: always create new files, never modify originals
- **Transparent**: clear report of what changed
- **Incremental**: start simple, refine with usage feedback

---

## Phase 1: Single File Application (MVP)

Apply template to ONE file at a time. Learn from this before adding batch.

### Template Source

- [ ] Use template from template-validation system (JSON)
- [ ] Or extract from another loaded file ("use this file as template")

### Column Mapping (Manual)

- [ ] User maps: template column ↔ source file column
- [ ] UI: dropdown per template column (select source column)
- [ ] Unmapped template columns → empty in output + warning
- [ ] Unmapped source columns → ignored (not copied) + info in report

### Data Processing

- [ ] Apply data normalization (existing Foundation Layer logic)
- [ ] Respect DataRegion if defined (from data-region-selection spec)
- [ ] Type coercion per template column definition

### Output

- [ ] Generate new file: `{filename}_templated.{ext}` (same format as input)
- [ ] Preserve formulas where possible (see export-results spec)
- [ ] Save to output folder (from Settings)

### Preview & Report

- [ ] Preview before generating:
  - Row count to process
  - Column mapping summary
  - Warnings (unmapped columns, type mismatches)
- [ ] Post-application report:
  - Rows processed
  - Cells normalized
  - Issues encountered

### UI (TBD — to refine during implementation)

- [ ] "Apply Template" action on loaded file
- [ ] Template selection (from library or another file)
- [ ] Mapping dialog with dropdowns
- [ ] Preview panel
- [ ] Progress indicator for large files

---

## Phase 2: Batch Application (Future)

Apply template to multiple files. **Spec TBD after Phase 1 learnings.**

### Conceptual Direction

- [ ] Select multiple files → apply same template
- [ ] "Remember mapping" — save column name associations
- [ ] Auto-apply where column names match
- [ ] Manual resolution where they don't

### Open Questions (to resolve after Phase 1)

- How to handle files with different structures in same batch?
- UX for reviewing/confirming mapping per file vs. trust auto-mapping?
- Performance for large batches (100+ files)?

---

## Phase 3: Advanced Features (Future)

Ideas for later iterations. **Not committed.**

- [ ] Column transformations (split, merge, derive)
- [ ] Conditional mapping rules
- [ ] Template versioning
- [ ] Undo/revert capability

---

## Technical Notes

- Reuse `ITemplateValidationService` for template loading
- Reuse Foundation Layer's `NormalizationResult` for data processing
- Reuse `IExcelWriterService` (from export-results) for output generation
- Column mapping stored as `Dictionary<string, string>` (template_col → source_col)

## Acceptance Criteria

### Phase 1

- [ ] Can select template and apply to single file
- [ ] Column mapping UI works correctly
- [ ] Output file has template structure with source data
- [ ] Unmapped columns handled gracefully (warning, not error)
- [ ] Preview shows accurate summary before applying
- [ ] Report shows what was done after applying

### Phase 2 & 3

*To be defined after Phase 1 implementation and user feedback.*
