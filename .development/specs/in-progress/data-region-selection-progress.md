# Data Region Selection - Implementation Progress

**Status**: in-progress
**Started**: 2026-02-03
**Spec**: data-region-selection.md

---

## Architecture Decisions

All architectural decisions are documented in ADRs:

| ADR | Topic | Key Decision |
| --- | ----- | ------------ |
| [ADR-009](../../reference/decisions/009-dataregion-data-model.md) | Data Model | `Dictionary<string, DataRegion>`, no ActiveRegion, per-region ColumnMetadata |
| [ADR-010](../../reference/decisions/010-dataregion-ui-pattern.md) | UI Pattern | Canvas selection (required), Regions Sidebar, per-file display |
| [ADR-011](../../reference/decisions/011-dataregion-persistence.md) | Persistence | JSON in dedicated subfolder per file |
| [ADR-012](../../reference/decisions/012-dataregion-cross-file-detection.md) | Cross-File Detection | Header-anchored detection, not fixed bounds, user confirms via preview |

### Additional Design Decisions

| Topic | Decision | Phase |
| ----- | -------- | ----- |
| Search behavior | Search in regions selected via Sidebar; explicit, no defaults | 2 |
| Normalization storage | `RegionId` field in CellMetadata | 2 |
| Template scope | Single region (Phase 2) → multiple regions (Phase 3) | 2-3 |
| Export behavior | File export: whole sheet with per-region normalization. Search/comparison export: unchanged | 2 |
| Merged cells | Prevent partial selection (snap-to-bounds, like Excel) | 2 |
| Column indexing | Absolute + `RegionName` context in SearchResult | 2 |
| Region propagation | Detect similar files, explicit user action to apply. See ADR-012 for detection algorithm | 2 |
| Similarity detection | Simplified for Phase 2 (column count + header names only). Full system (type detection, weighting, background analysis) in Phase 3 | 2-3 |
| Cross-file bounds | Header-anchored detection (not fixed bounds). Phase 2: stop at empty row. Phase 3: type-based window analysis. User confirms via preview | 2-3 |
| Auto-detection (within file) | Excluded. User defines regions manually via canvas | - |
| Template mismatch | Error (not intersection) | 2 |
| Default region name | Sheet name (covers entire sheet) | 1 |

---

## Impact Analysis

### Core Domain

#### DataRegion.cs (ValueObject)

- Add `string Name` (required, used as Dictionary key)
- Add `int? StartColumn`, `int? EndColumn`
- Update `IsValid()` to validate column bounds
- Add `bool ContainsCell(int row, int col)` helper
- Add `bool OverlapsWith(DataRegion other)` for validation
- Add `static DataRegion WholeSheet(string name, int rowCount, int colCount)` factory

#### SASheetData.cs (Entity)

- Add `Dictionary<string, DataRegion>? _dataRegions`
- Add `Dictionary<string, Dictionary<int, ColumnMetadata>>? _regionColumnMetadata`
- Add region CRUD: `AddDataRegion()`, `RemoveDataRegion()`, `GetDataRegion()`
- Add `IReadOnlyDictionary<string, DataRegion> DataRegions` property
- Add `IEnumerable<RowView> EnumerateDataRows(DataRegion region)` (filtered iterator)
- Add `ColumnMetadata? GetColumnMetadata(string regionName, int columnIndex)`
- Add `void SetColumnMetadata(string regionName, int columnIndex, ColumnMetadata metadata)`
- Initialize with default region (sheet name = whole sheet) when no user regions defined

#### CellMetadata (in SACellData.cs)

- Add `string? RegionId` (name of DataRegion that normalized this cell)

#### SearchResult.cs

- Add `string? RegionName` (region where match was found)

### Core Services

#### SearchService.cs

- Add `IEnumerable<DataRegion>? regions` parameter to `SearchInSheet()`
- Filter cells by `region.ContainsCell(row, col)`
- Set `SearchResult.RegionName` when creating results
- If no regions specified → search whole sheet (backward compatible)

#### RowComparisonService.cs

- Add `DataRegion? region` parameter to `CreateRowComparison()`
- Extract only columns within region bounds
- RowComparison.ColumnHeaders include only region columns

#### SheetAnalysisOrchestrator.cs

- Becomes **reactive**: re-analyzes when user selects a new region
- Add `AnalyzeRegionAsync(SASheetData sheet, DataRegion region, CancellationToken ct)`
- Call ColumnAnalysisService only on columns within region
- Store ColumnMetadata per-region (via `SASheetData.SetColumnMetadata(regionName, ...)`)

#### SimilarityComparisonService.cs (NEW - Phase 2 simplified)

- Phase 2: compare column count + header names (case-insensitive)
- Synchronous, triggered by explicit user action ("Find Similar Files")
- Returns match/no-match per file

### Template System

#### ExcelTemplate.cs

- Phase 2: Add `DataRegion? TargetRegion` (template validates this region)
- Phase 3: Evolve to `List<TemplateRegion> Regions` (template = full file schema)

#### TemplateValidationService.cs

- Validate region bounds against file structure
- If template has region but file doesn't → error
- If template region bounds don't match file → error (not intersection)

### Infrastructure

#### File Readers (OpenXmlFileReader, CsvFileReader)

- **No changes needed**: read entire sheet, DataRegion applied after loading

#### ExcelWriterService.cs

- Export whole sheet, normalization applied per-region
- Use `CellMetadata.RegionId` to determine which region normalized each cell
- Cells outside any region: copied unchanged

#### ComparisonExportService.cs

- **No changes needed**: exports comparison results, not raw files

### UI Layer

#### RegionsSidebarView (NEW)

- Third sidebar alongside Files and Columns
- Per-file grouping (no automatic name-based merging)
- Checkboxes for region selection (multi-select)
- Region info: name, bounds, row/column count

#### RegionSelectionCanvas (NEW)

- Sheet grid preview in FileDetailsView
- Click-and-drag selection
- Snap-to-merged-cells
- Visual highlight of selected region, dimmed area outside
- Coordinates display

#### FileDetailsViewModel

- Add `ObservableCollection<DataRegion> DataRegions`
- Add commands: `AddRegionCommand`, `RemoveRegionCommand`
- Display region info (name, bounds, size)

#### SearchViewModel

- Use `SelectedRegions` from RegionsSidebarViewModel
- Display "Searched in: [Region Name]" in results

#### TemplateManagementViewModel

- When creating template: include current DataRegion
- Validation: warn if template region doesn't match file

---

## Implementation Phases

### Phase 1: Core Domain (No UI)

**Goal**: DataRegion supports columns, SASheetData supports Dictionary of regions

1. Extend DataRegion ValueObject (Name required, StartColumn, EndColumn, validation, helpers)
2. Extend SASheetData Entity (Dictionary storage, per-region ColumnMetadata, CRUD, filtered iterators)
3. Add RegionId to CellMetadata
4. Unit tests (validation, overlaps, filtered iteration, region CRUD)

**Deliverable**: Core domain ready and tested, no UI

### Phase 2: Services Integration

**Goal**: Search and Compare respect DataRegions

1. Modify SearchService (region parameter, filter by bounds, RegionName in results)
2. Modify RowComparisonService (extract only columns within region)
3. Modify SheetAnalysisOrchestrator (per-region analysis, reactive re-analysis)
4. Add RegionName to SearchResult
5. Integration tests

**Deliverable**: Search/Compare/Analysis respect regions

### Phase 3: Template Integration

**Goal**: Templates save/load DataRegions

1. Extend ExcelTemplate (TargetRegion)
2. Modify TemplateValidationService (region bounds validation)
3. Modify TemplateRepository (save/load with regions)

**Deliverable**: Template system region-aware

### Phase 4: Export Integration

**Goal**: Export respects DataRegions

1. Modify ExcelWriterService (per-region normalization via RegionId)
2. Verify ComparisonExportService unchanged

**Deliverable**: Export region-aware

### Phase 5: UI

**Goal**: Full UI with canvas and Regions Sidebar

1. RegionsSidebarView (per-file grouping, checkboxes, region info)
2. RegionSelectionCanvas (grid preview, click-and-drag, snap-to-merged-cells)
3. FileDetailsViewModel updates (region display and management)
4. SearchViewModel updates (use SelectedRegions from sidebar)
5. Similarity detection UI (simplified: "Find Similar Files" button, file list with match indicator)

**Deliverable**: Complete UI for DataRegion management

### Phase 6: Persistence

**Goal**: DataRegions persist across sessions

1. Implement DataRegionPersistenceService (JSON read/write per ADR-011)
2. Load regions on file open
3. Save regions on user action (define/modify/delete)
4. Handle missing/corrupted regions.json gracefully

**Deliverable**: DataRegions persist to disk

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Performance (per-region iteration) | Medium | Filtered iterators (yield, no allocations). Benchmark with 10k+ rows |
| Complexity explosion (multiple regions) | High | Phase 2 = single region UX. Default = whole sheet. Advanced users opt-in in Phase 3 |
| Template compatibility (old templates) | Medium | Template without region = whole sheet. Validation warns, doesn't fail |
| Export data loss | High | Default = export whole sheet. Normalization per-region via RegionId |

---

## Next Steps

### Before Phase 1

1. **Verify existing code** (confirm no blockers):
   - [ ] SASheetData.cs - current cell storage and ColumnMetadata
   - [ ] SheetAnalysisOrchestrator.cs - current analysis flow
   - [ ] SearchService.cs - current search logic
   - [ ] ExcelWriterService.cs - current export logic
   - [ ] TemplateValidationService.cs - current validation

2. **Create implementation task list**:
   - [ ] Break down Phase 1 into atomic tasks
   - [ ] Define acceptance criteria per task

---

*Document created: 2026-02-03*
*Last updated: 2026-02-05 (cleaned up: decisions moved to ADRs)*
