# Data Region Selection

**Status**: planned
**Release**: v0.5.x (Fase 2), v1.x+ (Fase 3)
**Priority**: should-have
**Depends on**: multi-row-headers.md, template-validation.md, settings-configuration.md

## Summary

Allow users to define a rectangular data region within an Excel sheet, for cases where:
- Sheets contain mixed content (tables + diagrams + empty areas)
- Multiple tables are stacked vertically or horizontally
- Only a portion of the sheet contains relevant data

This extends `multi-row-headers` (which only handles header row count) to full 2D region selection.

## Problem Statement

Real-world Excel files often contain:
```
| empty | empty | ASCII diagram | empty | DATA TABLE | empty |
```
or:
```
| Table A headers |
| Table A data    |
| empty row       |
| Table B headers |
| Table B data    |
```

Current behavior: SheetAtlas assumes the entire sheet is data (after header rows).
Desired behavior: User can select which rectangular area contains the data of interest.

## User Stories

- As a user, I want to select a rectangular area of a sheet as my "data region"
- As a user, I want search/compare to operate only within my selected region
- As a user, I want to save my region selection in a template for reuse
- As a user, I want to temporarily ignore DataRegion and search everywhere
- As a user loading multiple similar files, I want to apply the same DataRegion to all

## Requirements

### Fase 2: Single DataRegion per Sheet

- [ ] DataRegion Definition (extends existing ValueObject)
  - [ ] Add StartColumn / EndColumn to DataRegion record
  - [ ] HeaderStartRow / HeaderEndRow (already exists)
  - [ ] DataStartRow / DataEndRow (already exists)

- [ ] UI: Region Selection Canvas
  - [ ] Sheet preview/grid view in file details
  - [ ] Click-and-drag to select rectangular area
  - [ ] Visual highlight of selected region
  - [ ] "Clear selection" button (revert to whole sheet)
  - [ ] Area outside DataRegion appears dimmed/grayed

- [ ] Per-File DataRegion
  - [ ] Each loaded file can have its own DataRegion (or none)
  - [ ] Without DataRegion = whole sheet (backward compatible)
  - [ ] Show status in file list: icon/badge for "DataRegion defined"

- [ ] Search Integration
  - [ ] Global toggle: "Use DataRegion" (checkbox in search bar)
  - [ ] When ON: search only within DataRegion (or whole sheet if none)
  - [ ] When OFF: search everywhere, ignore DataRegion
  - [ ] Search results show context: "Found in DataRegion" vs "Found in whole sheet"

- [ ] Compare Integration
  - [ ] Row comparison uses only rows within DataRegion
  - [ ] Column matching uses only columns within DataRegion

- [ ] Template Integration
  - [ ] DataRegion saved as part of template
  - [ ] Applying template = applying its DataRegion
  - [ ] Validation: warn if DataRegion exceeds sheet bounds

- [ ] Export Integration
  - [ ] Export whole sheet, but normalization applied only to DataRegion
  - [ ] Rest of sheet copied unchanged (diagrams, notes preserved)

### Fase 3: Multiple DataRegions per Sheet (v1.x+)

- [ ] Multiple regions per sheet
  - [ ] User can define Region A, Region B, etc.
  - [ ] Each region has a name/label
  - [ ] Regions cannot overlap

- [ ] Region-aware operations
  - [ ] Search: choose which region(s) to search
  - [ ] Compare: compare rows within same region only
  - [ ] Export: option to export specific region

- [ ] Auto-detection (nice-to-have)
  - [ ] Detect regions automatically based on empty rows/columns
  - [ ] User can accept/modify detected regions

## UX Considerations

### Feedback & Transparency

| Element | Behavior |
|---------|----------|
| File list | Badge showing DataRegion status (📊 defined, 📄 whole sheet) |
| Search bar | Toggle "Use DataRegion" clearly visible |
| Search results | Show "Searched in: DataRegion A1:F50" or "Searched in: whole sheet" |
| Region mismatch | Warning when template DataRegion exceeds file bounds |

### Principle: Filter, not Block

DataRegion is a **filter** to focus operations, not a **block** that prevents access:
- User can always toggle off "Use DataRegion" to search everywhere
- Operations without DataRegion = full sheet (current behavior preserved)
- No functionality is removed, only added

### Edge Cases

| Case | Handling |
|------|----------|
| DataRegion larger than sheet | Use intersection (warn user) |
| DataRegion has 0 data rows | Show warning, allow but flag |
| File structure changed | Prompt to redefine region |
| Template applied to incompatible file | Validation error with details |

## Technical Notes

- `DataRegion` ValueObject already exists at `Domain/ValueObjects/DataRegion.cs`
- Needs extension: add `StartColumn`, `EndColumn` properties
- `SASheetData` already supports `HeaderRowCount`, extend to use full `DataRegion`
- Consider: `SASheetData.ApplyDataRegion(DataRegion)` method for filtering

### Existing Code to Modify

- `DataRegion.cs` - add column bounds
- `SASheetData.cs` - add DataRegion property, filter methods
- `SearchService.cs` - respect DataRegion when searching
- `RowComparisonService.cs` - respect DataRegion when comparing
- `SheetAnalysisOrchestrator.cs` - apply DataRegion during analysis
- File readers - possibly pass DataRegion hint

## Open Questions

- [ ] How to handle merged cells that span DataRegion boundary?
- [ ] Should auto-detection be Fase 2 or Fase 3?
- [ ] Performance impact of region filtering on large sheets?

## Acceptance Criteria

### Fase 2
- [ ] User can select rectangular region via UI
- [ ] Search respects DataRegion (with toggle)
- [ ] Compare respects DataRegion
- [ ] DataRegion saved in template
- [ ] Export preserves content outside DataRegion
- [ ] Clear visual feedback of what's "active"

### Fase 3
- [ ] Multiple named regions per sheet
- [ ] Region selector in search/compare UI
- [ ] Auto-detection of regions (optional)
