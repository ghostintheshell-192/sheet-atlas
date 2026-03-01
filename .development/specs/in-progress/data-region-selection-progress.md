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
| Normalization storage | `RegionId` field in CellMetadata | 1 |
| Template scope | Single region → multiple regions (Phase 3+) | future |
| Export behavior | File export: whole sheet with per-region normalization. Search/comparison export: unchanged | future |
| Merged cells | Prevent partial selection (snap-to-bounds, like Excel) | future |
| Column indexing | Absolute + `RegionName` context in SearchResult | 1 |
| Region propagation | Detect similar files, explicit user action to apply. See ADR-012 for detection algorithm | future |
| Similarity detection | Simplified (column count + header names only). Full system in Phase 3+ | future |
| Cross-file bounds | Header-anchored detection (not fixed bounds). User confirms via preview | future |
| Auto-detection (within file) | Excluded. User defines regions manually via canvas | - |
| Template mismatch | Error (not intersection) | future |
| Default region name | Sheet name (covers entire sheet) | 1 |
| ContainsCell | Removed per YAGNI. If needed later: point-in-rectangle O(1) on DataRegion | - |

---

## Impact Analysis — What Was Actually Done

### Phase 1: Core Domain (complete)

| File | Changes |
| ---- | ------- |
| `DataRegion.cs` | `Name` (required), `StartColumn`/`EndColumn`, `IsValid()`, `OverlapsWith()`, `WholeSheet()`, `Manual()`, `FromDataRange()`, `AutoDetect()`, `HeaderRowCount` |
| `SASheetData.cs` | `_dataRegions` + `_regionColumnMetadata` dictionaries, CRUD (`Add`/`Remove`/`Get`), `EnumerateDataRows(region)`, per-region `Get`/`SetColumnMetadata(regionName, ...)` |
| `SACellData.cs` | `CellMetadata.RegionId` field |
| `SearchResult.cs` | `RegionName` property |

**Note**: `ContainsCell` was implemented then removed per YAGNI.

### Phase 2: Service Integration + Persistence (complete)

| File | Changes |
| ---- | ------- |
| `ISheetAnalysisOrchestrator.cs` | Added `EnrichRegionAsync(SASheetData, DataRegion, List<ExcelError>)` |
| `SheetAnalysisOrchestrator.cs` | `EnrichRegionAsync` + private `EnrichRegionWithColumnAnalysis` (mirrors global but respects region bounds, sets RegionId) |
| `SearchService.cs` | `SearchInSheet` gained `string? regionName` parameter. When set: row/col iteration bounded by region, `RegionName` set on results. When null: unchanged behavior |
| `DataRegionFile.cs` (NEW) | Persistence DTOs: `DataRegionFile` (root), `SheetRegionsDto` (per-sheet) |
| `IDataRegionPersistenceService.cs` (NEW) | Interface: `SaveAsync`, `LoadAsync`, `DeleteAsync` |
| `DataRegionPersistenceService.cs` (NEW) | JSON persistence in `{LocalAppData}/SheetAtlas/DataRegions/`. Atomic write, graceful errors. Uses `FilePathHelper.GenerateLogFolderName` |
| `AppJsonContext.cs` | Registered `DataRegion`, `DataRegionFile`, `SheetRegionsDto`, related dictionaries |
| `App.axaml.cs` | DI: `IDataRegionPersistenceService` → `DataRegionPersistenceService` |

**What was NOT done (deferred per plan):**
- RowComparisonService — no consumer yet
- SimilarityComparisonService — deferred to cross-file detection phase
- Template system changes — deferred
- Export changes — deferred
- File reader changes — regions NOT auto-loaded on file open yet

### Tests added in Phase 2

| File | Tests |
| ---- | ----- |
| `SheetAnalysisOrchestratorRegionTests.cs` (NEW) | 5 tests: region bounds, per-region metadata, RegionId on cells, column bounds, null throws |
| `SearchServiceRegionTests.cs` (NEW) | 5 tests: region bounds, RegionName on results, nonexistent region, whole-sheet fallback, column filter |
| `DataRegionPersistenceServiceTests.cs` (NEW) | 6 tests: save, load, no-file, corrupted, delete, round-trip |
| `AppJsonContextTests.cs` (extended) | 2 tests: DataRegion serialization, DataRegionFile serialization |

**Total**: 557 tests, all passing.

---

## Implementation Phases (revised)

### Phase 1: Core Domain — COMPLETE

Commits: `c9ec7a0`, `1845a1a`

### Phase 2: Service Integration + Persistence — COMPLETE

Commit: `1e24df5`

### Phase 3: UI (next)

**Goal**: Region management UI — define, view, delete regions

Scope to be planned. Reference mockups: `data-region-selection-mockups.md`

Key components:
1. Region definition UX (how user defines a region — canvas or form-based)
2. Regions display in FileDetailsView
3. Search integration (use selected region)
4. Persistence hooks (save/load on file open/close)

### Future Phases (not yet planned in detail)

- Template integration (ExcelTemplate + TargetRegion)
- Export integration (per-region normalization via RegionId)
- Cross-file detection (ADR-012, similarity service)
- Multiple regions per sheet

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Performance (per-region iteration) | Medium | Filtered iterators (yield, no allocations). Benchmark with 10k+ rows |
| Complexity explosion (multiple regions) | High | Start with single region UX. Default = whole sheet. Advanced users opt-in later |
| Template compatibility (old templates) | Medium | Template without region = whole sheet. Validation warns, doesn't fail |
| Export data loss | High | Default = export whole sheet. Normalization per-region via RegionId |

---

*Document created: 2026-02-03*
*Last updated: 2026-02-05 (Phase 1+2 complete, impact analysis aligned with implementation)*
