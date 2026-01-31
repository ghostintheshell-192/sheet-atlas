# Memory Optimization - Work in Progress

## Problem Statement
- **Current**: 670 MB RAM for 40 MB of Excel data (17:1 ratio)
- **After Clear All Data + GC**: Memory stays at ~675 MB (only ~14 MB released)
- **Root cause**: DataTable overhead (10-14x) + memory leaks

## Completed Work

### Phase 1: Code Quality (✓ Committed)
1. **Nullability warnings** - Fixed all warnings in SearchResult, SearchService, OpenXmlFileReader
2. **Quick Win #1** - Removed ItemArray.ToList() unnecessary copy
3. **Quick Win #2** - Removed fake async methods (Task.FromResult → sync)
4. **Quick Win #3** - Added string interning for duplicate cell values (ConcurrentDictionary pool)

### Phase 2: Memory Leak Infrastructure (✓ Committed)
1. **TreeSearchResultsViewModel.RemoveSearchResultsForFile()** - Clears search history when file removed
2. **IRowComparisonCoordinator.RemoveComparisonsForFile()** - Clears comparisons when file removed
3. **MainWindowViewModel.OnCleanAllDataRequested()** - Clears SelectedFile reference

**Impact**: ~14 MB released after Clear + GC (2% improvement) - infrastructure is correct but insufficient

### Phase 3: IDisposable Pattern Implementation (✓ Completed)
1. **ExcelFile.Dispose()** - Disposes all DataTable instances when file removed
2. **FileLoadResultViewModel.Dispose()** - Nulls ExcelFile reference to allow GC
3. **IFileLoadResultViewModel** - Extended to inherit IDisposable
4. **SearchResultsManager.RemoveResultsForFile()** - Clears search results referencing removed files
5. **Automatic Aggressive GC** - Forces Gen 2 + LOH collection with compaction after Clear All Data

**Non-Standard IDisposable Pattern**:
- ExcelFile does NOT call `GC.SuppressFinalize()` (intentionally)
- Finalizer always runs as safety net for DataTable cleanup
- Justified by DataTable's 10-14x memory overhead and lazy GC behavior
- Comprehensive code comments explain rationale and TODO for when DataTable is replaced

**Impact**: **352 MB released after Clear + GC (59% reduction)** - Memory returns to baseline (~240 MB)

## Remaining Work (Optimization Opportunities)

### ✓ Priority 1: IDisposable Pattern (COMPLETED)
- ✓ **ExcelFile** implements IDisposable
- ✓ **DataTable.Dispose()** called when files removed
- ✓ Dispose all DataTables in ExcelFile.Dispose()
- ✓ Call Dispose() in OnCleanAllDataRequested before RemoveFile
- ✓ FileLoadResultViewModel implements IDisposable to null references

### ✓ Priority 2: SearchViewModel Cleanup (COMPLETED)
- ✓ **SearchResultsManager.RemoveResultsForFile()** clears results when file removed
- ✓ SearchViewModel wrapper method added
- ✓ Called in OnCleanAllDataRequested cleanup path

### Priority 3: String Pool Management
- **CellValueReader._stringPool** grows indefinitely (ConcurrentDictionary)
- No cleanup mechanism when files removed
- Options:
  - Per-file string pools (dispose with file)
  - Periodic cleanup based on file references
  - WeakReference values in dictionary

### Priority 4: DataTable Replacement (Long-term)
- Replace DataTable with custom lightweight structures
- Target: 3-5x ratio instead of 10-14x
- Major refactoring required (affects entire codebase)

## Memory Analysis Tools

### Monitor Script
`.personal/scripts/monitor-sheetatlas.sh` - Tracks RSS memory every 2 seconds

### Test Scenario
1. Load files (~40 MB data) → ~670 MB RAM
2. Search + Create comparisons
3. Clear All Data on selected file
4. Force GC (Tools → Force GC or Ctrl+Shift+G)
5. Expected: ~50-70% reduction if leaks fixed
6. Actual: ~2% reduction (leak still present)

## Technical Notes

### Why DataTable is Heavy
- Row metadata (DataRowState, change tracking)
- Column constraints and validation
- Schema information
- Index structures
- ~10-14x overhead over raw data

### Leak Sources Confirmed
1. ✓ SearchHistory - FIXED (Phase 2)
2. ✓ RowComparisons - FIXED (Phase 2)
3. ✓ SelectedFile - FIXED (Phase 2)
4. ✓ DataTable instances - FIXED (Phase 3 - IDisposable pattern)
5. ✓ SearchViewModel.SearchResults - FIXED (Phase 3 - RemoveResultsForFile)
6. ⚠️ String pool - NOT CLEARED (low priority, minor impact)

### Next Session Plan
1. ✓ All critical memory leaks resolved (59% reduction achieved)
2. Consider string pool optimization if further memory reduction needed
3. Long-term: Replace DataTable with lightweight structures (3-5x target ratio)

## Files Modified (Phase 3)

```
src/SheetAtlas.Core/Domain/Entities/ExcelFile.cs
src/SheetAtlas.UI.Avalonia/ViewModels/FileLoadResultViewModel.cs
src/SheetAtlas.UI.Avalonia/ViewModels/IFileLoadResultViewModel.cs
src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs
src/SheetAtlas.UI.Avalonia/ViewModels/SearchViewModel.cs
src/SheetAtlas.UI.Avalonia/Managers/Search/ISearchResultsManager.cs
src/SheetAtlas.UI.Avalonia/Managers/Search/SearchResultsManager.cs
src/SheetAtlas.UI.Avalonia/Views/MainWindow.axaml
```

**Total**: 8 files, +137 lines

## Metrics

| Metric | Before | After Phase 1 | After Phase 2 | After Phase 3 | Target |
|--------|--------|---------------|---------------|---------------|--------|
| Build warnings | 10+ | 0 | 0 | 0 (3 false positives) | 0 |
| Memory (5 files loaded) | 593 MB | 593 MB | 593 MB | 593 MB | ~200 MB |
| Memory after Clear All Data + GC | 593 MB | 593 MB | 579 MB | **241 MB** | ~100 MB |
| Memory reduction | 0% | 0% | 2% | **59%** | 80%+ |
| Ratio (RAM/Data) | ~15:1 | ~15:1 | ~14:1 | **~6:1** | 3-5:1 |

**Key Result**: Clear All Data now releases 352 MB (59% reduction), memory returns to baseline (~240 MB)

---
**Last updated**: 2025-10-11
**Status**: Phase 3 completed - Critical memory leaks resolved
**Next priority**: Optional string pool optimization, or long-term DataTable replacement
