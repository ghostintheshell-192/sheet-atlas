# Memory Leak in UnloadAllFiles - Investigation Notes

**Date**: 2025-10-21
**Discovered During**: Strategy 4 refactoring (UnloadAllFilesAsync readability improvement)

---

## Problem Summary

`UnloadAllFiles` does NOT properly release memory when unloading files. Memory monitoring shows that files remain in memory after unload.

## Root Cause Analysis

### Memory Retention Chain

1. **ExcelRow holds strong reference to ExcelFile**:
   ```csharp
   // SheetAtlas.Core/Domain/Entities/RowComparison.cs:10
   public class ExcelRow
   {
       public ExcelFile SourceFile { get; }  // ❌ Strong reference!
   ```

2. **RowComparison contains List<ExcelRow>**:
   - Each ExcelRow references the source ExcelFile
   - RowComparison keeps these references alive

3. **RowComparisonViewModel does NOT implement IDisposable**:
   - When comparisons are removed from UI, references remain
   - No cleanup mechanism for internal RowComparison data

4. **Current UnloadAllFiles sequence**:
   ```csharp
   ClearAllUIState();          // ✅ Clears selection
   RemoveAllComparisons();     // ⚠️ Removes from UI collection only
   ClearAllSearchResults();    // ✅ Clears search
   RemoveAllFiles();           // ❌ Dispose() called, but comparisons still reference files!
   ```

### Why Memory Stays Allocated

Even after `file.Dispose()` is called:
- ✅ DataTables inside SASheetData are disposed
- ✅ File references are nulled in FileLoadResultViewModel
- ❌ **BUT**: RowComparison objects in removed comparisons still hold ExcelFile references
- ❌ GC cannot collect ExcelFile because RowComparison.Rows[].SourceFile keeps it alive

---

## Potential Solutions

### Option 1: Implement IDisposable in RowComparisonViewModel ⭐ RECOMMENDED
```csharp
public class RowComparisonViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        // Null out the Comparison reference to break the chain
        _comparison = null;
        // Unsubscribe from theme events
        if (_themeManager != null)
            _themeManager.ThemeChanged -= OnThemeChanged;
    }
}
```

**Benefits**:
- Clean architectural solution
- Follows IDisposable pattern used elsewhere
- Can be called in RemoveComparison()

**Impact**: Medium (requires updating RowComparisonCoordinator to call Dispose)

---

### Option 2: Call RemoveComparisonsForFile() per file
```csharp
private void RemoveAllFiles()
{
    var filesToRemove = LoadedFiles.ToList();
    foreach (var file in filesToRemove)
    {
        // Remove comparisons that reference this file BEFORE disposing
        if (file.File != null)
        {
            _comparisonCoordinator.RemoveComparisonsForFile(file.File);
        }

        file.Dispose();
        _filesManager.RemoveFile(file);
    }
    _logger.LogInfo($"Unloaded {filesToRemove.Count} file(s)", "MainWindowViewModel");
}
```

**Benefits**:
- Targeted fix for UnloadAll scenario
- No architectural changes needed

**Drawbacks**:
- Duplicates comparison cleanup (RemoveAllComparisons already does this)
- Doesn't fix the root cause (RowComparisonViewModel not disposable)

---

### Option 3: Event-Driven Cleanup (Future Architecture)

When full event-driven architecture is implemented:
- FileRemoved event → automatically triggers RemoveComparisonsForFile
- No manual coordination needed
- Cleaner separation of concerns

**Status**: Wait for event-driven migration completion

---

## Recommended Action Plan

1. **Short term** (this branch): Complete readability refactoring without addressing memory leak
   - Memory leak existed BEFORE this refactoring
   - Not a regression, just discovered during testing

2. **Next branch**: Implement Option 1 (IDisposable in RowComparisonViewModel)
   - Create branch: `fix/comparison-viewmodel-memory-leak`
   - Add Dispose() to RowComparisonViewModel
   - Update RemoveComparison to call Dispose()
   - Test with memory monitoring script

3. **Long term**: Re-evaluate after event-driven migration
   - May be automatically resolved by event handling
   - Review ExcelRow.SourceFile reference design (consider WeakReference?)

---

## Testing Notes

**To reproduce**:
1. Load multiple large Excel files (50MB+)
2. Create row comparisons between files
3. Run `.personal/scripts/monitor-sheetatlas.sh`
4. Click "Unload All Files"
5. Observe: Memory does NOT decrease significantly

**Expected after fix**:
- Memory should drop to near-baseline after UnloadAll
- Only UI overhead should remain (~50-100MB)

---

## Related Files

- `src/SheetAtlas.Core/Domain/Entities/RowComparison.cs` - ExcelRow with SourceFile reference
- `src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs` - Missing IDisposable
- `src/SheetAtlas.UI.Avalonia/Managers/Comparison/RowComparisonCoordinator.cs` - RemoveComparisonsForFile logic
- `src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs` - UnloadAllFilesAsync

---

**Status**: Investigation complete, solution identified, deferred to separate branch
**Priority**: High (memory leaks are serious in long-running desktop apps)
**Estimated effort**: 2-3 hours for Option 1 implementation + testing
