# SheetAtlas - Architectural Analysis Report

**Date**: 2025-11-26
**Scope**: C# / .NET 8 + Avalonia UI codebase
**Focus**: Dependency issues, concurrency problems, error handling gaps, MVVM violations, state management

---

## Executive Summary

The SheetAtlas codebase demonstrates **good overall architectural discipline** with proper MVVM separation, dependency injection, and disposal patterns. However, **5 critical architectural issues** have been identified that could cause runtime crashes or memory leaks under specific conditions. Most are edge cases that require specific user actions to trigger, but are reliably reproducible.

---

## CRITICAL ISSUES (Must Fix)

### ISSUE #1: FileDetailsViewModel NOT Disposed - Memory Leak

**Severity**: HIGH
**Type**: MVVM Violation + Resource Leak
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs:184`

**Problem**:
```csharp
protected void Dispose(bool v)
{
    if (v)
    {
        UnsubscribeFromEvents();
        _filesManager.Dispose();
        _comparisonCoordinator.Dispose();
        SearchViewModel?.Dispose();
        // FileDetailsViewModel?.Dispose();  // <-- COMMENTED OUT!
        TreeSearchResultsViewModel?.Dispose();
    }
}
```

FileDetailsViewModel is set via `SetFileDetailsViewModel()` (line 157-167), remains a field property throughout the app lifecycle, but is **never disposed**. This ViewModel:
- Holds `ILogService` and `IFileLogService` dependencies
- Manages `ErrorLogs` ObservableCollection
- Subscribes to events from FileDetailsViewModel that are never unsubscribed

**Runtime Impact**:
- When MainWindowViewModel disposes, FileDetailsViewModel remains in memory
- If any external code holds a reference to FileDetailsViewModel, it keeps the logger services alive
- Long-running application sessions accumulate undisposed ErrorLog collections
- Not immediately catastrophic (services are singletons), but violates disposal contract

**Scenario**:
```csharp
// This triggers in any app session:
1. App creates MainWindowViewModel (via DI as Singleton)
2. App.OnFrameworkInitializationCompleted() calls SetFileDetailsViewModel()
3. User performs actions that populate FileDetailsViewModel.ErrorLogs
4. App shutdown → MainWindowViewModel.Dispose() called
5. FileDetailsViewModel is skipped, remains in Gen 2 memory
```

**Fix**:
Uncomment line 184 and ensure FileDetailsViewModel implements IDisposable:
```csharp
FileDetailsViewModel?.Dispose();
```

---

### ISSUE #2: SearchViewModel Event Handler Not Cleaned Up in Specific Path

**Severity**: HIGH
**Type**: Event Handler Leak
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.EventHandlers.cs:37-41`

**Problem**:
```csharp
private void UnsubscribeFromEvents()
{
    // ... other unsubscribes ...
    if (SearchViewModel != null && _searchViewModelPropertyChangedHandler != null)
    {
        SearchViewModel.PropertyChanged -= _searchViewModelPropertyChangedHandler;
        _searchViewModelPropertyChangedHandler = null;
    }
}
```

The handler cleanup only occurs if **both conditions are true**:
1. `SearchViewModel != null` AND
2. `_searchViewModelPropertyChangedHandler != null`

However, looking at `SetSearchViewModel()` (lines 129-155), the handler is **always created and subscribed**:
```csharp
_searchViewModelPropertyChangedHandler = (s, e) => { ... };
SearchViewModel.PropertyChanged += _searchViewModelPropertyChangedHandler;
```

But there's a subtle issue: if `SetSearchViewModel()` is never called (which shouldn't happen in normal flow but could in testing or error paths), then `_searchViewModelPropertyChangedHandler` is **null** while `SearchViewModel` is **not null** (because it's exposed as a property).

**Actually, the REAL issue is different**: The handler assignment happens **inside SetSearchViewModel()**, but if the method is called multiple times without cleanup, a new handler is assigned without removing the old one:

Looking at line 137:
```csharp
_searchViewModelPropertyChangedHandler = (s, e) => { ... };
```

This **overwrites** the previous handler reference without unsubscribing the old one. If `SetSearchViewModel()` is called twice, the first handler remains subscribed but unreferenced.

**Runtime Impact**:
- Each call to SetSearchViewModel() creates a new handler and overwrites `_searchViewModelPropertyChangedHandler`
- Old handler remains subscribed to SearchViewModel.PropertyChanged
- Orphaned handler keeps SearchViewModel and Dispatcher alive longer than expected
- Multiple handlers accumulate in PropertyChanged event

**Scenario**:
```csharp
1. SetSearchViewModel(firstSearchVM) - handler subscribed, reference stored
2. SetSearchViewModel(secondSearchVM) - new handler created, reference overwrites old
3. Old handler still subscribed to firstSearchVM.PropertyChanged but unreferenced
4. firstSearchVM can't be GC'd while firstSearchVM.PropertyChanged still has the orphaned handler
```

**Fix**:
In `SetSearchViewModel()`, unsubscribe old handler before creating new one:
```csharp
public void SetSearchViewModel(SearchViewModel searchViewModel)
{
    SearchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));

    // Unsubscribe old handler first
    if (_searchViewModelPropertyChangedHandler != null)
    {
        // Note: this shouldn't happen normally, but defensive programming
        if (SearchViewModel != null)
            SearchViewModel.PropertyChanged -= _searchViewModelPropertyChangedHandler;
    }

    SearchViewModel.Initialize(LoadedFiles);

    // ... rest of method ...
}
```

---

### ISSUE #3: RowComparisonViewModel Theme Handler Leak on Replacement

**Severity**: MEDIUM
**Type**: Event Handler Leak + Incomplete Disposal
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Comparison/RowComparisonCoordinator.cs:136-147`

**Problem**:
In `RemoveComparisonsForFile()`, when updating a comparison after file removal, a **new RowComparisonViewModel is created** but the old one's theme handler is not properly cleaned:

```csharp
// Create new comparison after removing file's rows
var newViewModel = new RowComparisonViewModel(updatedComparison, _comparisonViewModelLogger, _themeManager);
newViewModel.CloseRequested += OnComparisonCloseRequested;

var index = _rowComparisons.IndexOf(comparisonViewModel);
_rowComparisons[index] = newViewModel;  // Replace in collection

// Later...
comparisonViewModel.CloseRequested -= OnComparisonCloseRequested;  // Unsubscribe only CloseRequested
```

The old `comparisonViewModel` has:
1. ✅ CloseRequested event unsubscribed (line 147)
2. ❌ ThemeManager.ThemeChanged event still subscribed (from line 61 in RowComparisonViewModel constructor)

```csharp
// In RowComparisonViewModel.Dispose() - line 137-140
if (_themeManager != null)
{
    _themeManager.ThemeChanged -= OnThemeChanged;
}
```

The old ViewModel is never disposed, so ThemeChanged handler remains subscribed.

**Runtime Impact**:
- Each comparison update leaves an orphaned ViewModel with active theme handler
- Orphaned ViewModels accumulate in memory over time
- Memory leak accelerated by frequent file operations
- Potential null reference if theme changes are triggered after ViewModel should be disposed

**Scenario**:
```csharp
1. User loads 3 files (A, B, C)
2. Selects row from A, row from B, row from C → comparison created
3. Removes file A from collection
4. RowComparisonCoordinator.RemoveComparisonsForFile() called
5. Creates new comparison without A's rows
6. Old comparisonViewModel created but only CloseRequested unsubscribed
7. Old ViewModel still subscribed to _themeManager.ThemeChanged
8. If theme changes: OnThemeChanged() on dead ViewModel accessing disposed resources
```

**Fix**:
Dispose old ViewModel before replacing:
```csharp
var newViewModel = new RowComparisonViewModel(updatedComparison, _comparisonViewModelLogger, _themeManager);
newViewModel.CloseRequested += OnComparisonCloseRequested;

var index = _rowComparisons.IndexOf(comparisonViewModel);
_rowComparisons[index] = newViewModel;

// Dispose old BEFORE unsubscribing
comparisonViewModel.Dispose();  // This unsubscribes ThemeChanged
comparisonViewModel.CloseRequested -= OnComparisonCloseRequested;
```

Or in Dispose(bool):
```csharp
protected void Dispose(bool disposing)
{
    if (disposing)
    {
        foreach (var comparison in _rowComparisons)
        {
            comparison.CloseRequested -= OnComparisonCloseRequested;
            comparison.Dispose();  // Call Dispose to unsubscribe ThemeChanged
        }
        _rowComparisons.Clear();
    }
}
```

---

### ISSUE #4: SearchHistoryItem Anonymous Lambda Event Handler Not Cleaned Up

**Severity**: MEDIUM
**Type**: Event Handler Leak
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/TreeSearchResultsViewModel.cs:97`

**Problem**:
```csharp
public void AddSearchResults(string query, IReadOnlyList<SearchResult> results)
{
    var searchItem = new SearchHistoryItem(query, results);

    searchItem.SelectionChanged += (s, e) => NotifySelectionChanged();  // <-- ANONYMOUS LAMBDA

    foreach (var fileGroup in searchItem.FileGroups)
    {
        foreach (var sheetGroup in fileGroup.SheetGroups)
        {
            sheetGroup.SetupSelectionEvents(NotifySelectionChanged);
        }
    }

    SearchHistory.Insert(0, searchItem);
}
```

An **anonymous lambda handler** `(s, e) => NotifySelectionChanged()` is subscribed to `searchItem.SelectionChanged`, but:

1. **No reference is stored** to unsubscribe later
2. When `SearchHistory.Remove(existing)` is called (line 90), the old SearchHistoryItem is removed from the collection
3. But the anonymous handler **remains subscribed** to the removed item's SelectionChanged event

```csharp
// In Dispose() - line 253-256
foreach (var searchItem in SearchHistory)
{
    searchItem.Dispose();
}
```

Only items **still in SearchHistory** are disposed. Items removed from the collection (line 90) are abandoned with orphaned event handlers.

**Runtime Impact**:
- Each search creates a SearchHistoryItem with an anonymous handler
- When search is updated (line 90), old item is removed but handler remains subscribed
- The old SearchHistoryItem can't be GC'd because SelectionChanged still holds a reference to it
- Memory accumulates: old items × (handler per item + all contained SearchResultItem objects)
- UI lag when SelectionChanged is invoked (iterates through orphaned handlers)

**Scenario**:
```csharp
1. User performs search #1 → SearchHistoryItem created with lambda handler
2. User performs search #2 → New SearchHistoryItem created
3. Search #1 result is still in history (kept in collection)
4. User performs search #3 → New SearchHistoryItem created, pushing search #2 out
5. Search #2's SelectHistoryItem removed from SearchHistory collection
6. BUT: Lambda handler still subscribed to SearchHistoryItem.SelectionChanged
7. SearchHistoryItem #2 can't be GC'd (handler keeps it alive)
```

**Fix**:
Store handler reference and unsubscribe when item is removed:

```csharp
public void AddSearchResults(string query, IReadOnlyList<SearchResult> results)
{
    var existing = SearchHistory.FirstOrDefault(s => s.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

    // ... save expansion state ...

    if (existing != null)
    {
        // IMPORTANT: Unsubscribe handlers BEFORE removing from collection
        existing.SelectionChanged -= (s, e) => NotifySelectionChanged();  // Won't work - no ref!
        SearchHistory.Remove(existing);
    }

    // Use method reference instead of lambda
    var searchItem = new SearchHistoryItem(query, results);
    searchItem.SelectionChanged += OnSearchItemSelectionChanged;  // Store reference!

    // ... rest ...
}

private void OnSearchItemSelectionChanged(object? sender, EventArgs e)
{
    NotifySelectionChanged();
}

public void Dispose()
{
    // ...
    foreach (var searchItem in SearchHistory)
    {
        searchItem.SelectionChanged -= OnSearchItemSelectionChanged;  // Unsubscribe!
        searchItem.Dispose();
    }
    // ...
}
```

---

### ISSUE #5: Fire-and-Forget GC.Collect() Without Await - Async State Leak

**Severity**: MEDIUM
**Type**: Async State Machine Leak + Thread Safety
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.EventHandlers.cs:216-223`

**Problem**:
```csharp
_ = RetryLoadFileAsync(file);  // Fire-and-forget
```

And in the event handler:
```csharp
// AGGRESSIVE CLEANUP: Force garbage collection after file removal
Task.Run(() =>
{
    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
});
```

Two problems:

1. **Fire-and-forget with Task.Run()**: The GC.Collect() call is fire-and-forget but NOT awaited. If an exception occurs on the background thread:
   - No exception is logged
   - The task failure is silent
   - The app continues running with potentially worse memory state

2. **Race condition**: `GCSettings.LargeObjectHeapCompactionMode` is a **static property**. Setting it on a background thread while the UI thread might also set it creates a race:
   ```csharp
   // Thread A (UI)
   Task.Run(() => {
       GCSettings.LargeObjectHeapCompactionMode = CompactOnce;
       GC.Collect(...);
   });

   // Thread B (GC)
   // Might read stale value if race condition triggers
   ```

3. **Double GC.Collect() with blocking**: This is intentional for LOH cleanup, but if ANY exception occurs between the two calls, the second call never happens and the memory state is indeterminate.

**Runtime Impact**:
- Exception on GC thread is silently swallowed
- Memory state becomes unpredictable if GC fails
- Race condition rare but possible: GCSettings.LargeObjectHeapCompactionMode could be set by multiple threads
- Large file unload operations become unreliable - memory might not be freed

**Scenario**:
```csharp
1. User unloads large Excel file (500MB+)
2. OnCleanAllDataRequested() called
3. Dispose() and RemoveFile() successful
4. Task.Run(() => GC.Collect(...)) queued on ThreadPool
5. ThreadPool is under load, delay before execution
6. Meanwhile, UI loads another file
7. Two threads accessing GCSettings.LargeObjectHeapCompactionMode simultaneously
8. Possible read-write race on static field
9. OR: ThreadPool exception (OOM on GC thread) - silently swallowed
```

**Fix**:
Use lock for static field access and properly await with error handling:

```csharp
private static readonly object _gcLock = new object();

private async Task PerformAggressiveGarbageCollectionAsync()
{
    try
    {
        await Task.Run(() =>
        {
            lock (_gcLock)
            {
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                    System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            }
        });

        _logger.LogInfo("Aggressive GC completed successfully", "MainWindowViewModel");
    }
    catch (Exception ex)
    {
        _logger.LogError("Aggressive GC failed", ex, "MainWindowViewModel");
        // Don't crash, but log for diagnostics
    }
}

private void OnCleanAllDataRequested(object? sender, FileActionEventArgs e)
{
    // ... existing cleanup code ...

    // Await the GC operation properly
    _ = PerformAggressiveGarbageCollectionAsync();
}
```

Or use Dispatcher to stay on UI thread:
```csharp
Dispatcher.UIThread.InvokeAsync(async () =>
{
    await PerformAggressiveGarbageCollectionAsync();
}, DispatcherPriority.Background);
```

---

## MEDIUM-SEVERITY ISSUES (Should Fix)

### ISSUE #6: RelayCommand<T> Lambda Captures Not Validated

**Severity**: MEDIUM
**Type**: Defensive Programming Gap
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Commands/RelayCommand.cs:118`

**Problem**:
```csharp
public void Execute(object? parameter)
{
    if (!CanExecute(parameter))
        return;

    try
    {
        _execute((T)parameter!);  // Unsafe cast without validation
    }
    // ...
}
```

The cast `(T)parameter!` uses `!` (null-forgiving operator) without validation. If the bound control passes wrong type:

```csharp
RelayCommand<string> command = new(...);
// But binding passes int instead
command.Execute(42);  // Passes: (string)42 with !
```

This won't throw immediately due to the `!` operator, but:
1. Boxing converts 42 to object
2. Unboxing to string fails at runtime
3. If caught, error is logged but not visually reported
4. User sees command appeared to work but did nothing

**Fix**: Validate type before cast:
```csharp
public void Execute(object? parameter)
{
    if (!CanExecute(parameter))
        return;

    try
    {
        if (parameter != null && !(parameter is T))
        {
            _logger?.LogError($"Command parameter type mismatch. Expected {typeof(T).Name}, got {parameter.GetType().Name}", "RelayCommand<T>");
            return;
        }

        _execute((T)parameter!);
    }
    catch (InvalidCastException ex)
    {
        _logger?.LogError($"Failed to cast command parameter", ex, "RelayCommand<T>");
    }
    // ...
}
```

---

### ISSUE #7: RowComparisonCoordinator Comparison Reference Stability

**Severity**: MEDIUM
**Type**: State Management - Incomplete Disposal on Replace
**Location**: `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Comparison/RowComparisonCoordinator.cs:131-147`

**Problem**:
When file is removed and comparison is updated (not removed entirely), the old ViewModel is replaced but not explicitly disposed:

```csharp
var newViewModel = new RowComparisonViewModel(updatedComparison, _comparisonViewModelLogger, _themeManager);
newViewModel.CloseRequested += OnComparisonCloseRequested;

var index = _rowComparisons.IndexOf(comparisonViewModel);
_rowComparisons[index] = newViewModel;  // <-- Collection replaces reference

// ... later ...
comparisonViewModel.CloseRequested -= OnComparisonCloseRequested;
```

The old `comparisonViewModel`:
- Is removed from the `_rowComparisons` collection (replaced)
- Has its CloseRequested unsubscribed
- But is never Dispose() called
- Keeps alive: ThemeManager event handler, Columns ObservableCollection, _allCells list

If the new comparison is selected immediately (line 144: `SelectedComparison = newViewModel`), the old one might still be referenced by external code.

**Runtime Impact**:
- Old ViewModel with large Columns collection remains in memory
- Old theme event handler consumes memory and CPU cycles
- Multiple file edits accumulate orphaned ViewModels

**Fix**: Already identified in ISSUE #3. Dispose explicitly.

---

## ARCHITECTURAL PATTERNS OBSERVED

### ✅ Good Practices

1. **Proper Dependency Injection**: All managers and services injected via constructor
2. **MVVM Separation**: ViewModels don't directly create Views
3. **Observable Collections**: Used correctly for UI binding
4. **Async/Await**: Properly used with Task-based API
5. **Error Handling**: RelayCommand wraps all command execution
6. **Disposal Pattern**: Most components implement IDisposable
7. **Event Subscription**: Generally properly managed (except noted issues)

### ⚠️ Patterns with Issues

1. **Anonymous Lambda Event Handlers**: Creates untrackable subscriptions (ISSUE #4)
2. **Fire-and-Forget Tasks**: Some tasks (`Task.Run()`) without proper error handling (ISSUE #5)
3. **Singleton Lifetime**: Services registered as Singleton but not all cleanup paths hit Dispose
4. **ViewModel Replacement**: Old ViewModels not explicitly disposed (ISSUE #3, #7)

---

## RECOMMENDED IMMEDIATE ACTIONS

### Priority 1 (Do First)
1. **Uncomment FileDetailsViewModel.Dispose()** in MainWindowViewModel.Dispose() - Line 184
2. **Fix RowComparisonViewModel replacement** to call Dispose() on old instance - RowComparisonCoordinator:131-147
3. **Fix SearchHistoryItem handler leak** by storing handler reference - TreeSearchResultsViewModel:97

### Priority 2 (Do Soon)
4. **Fix SearchViewModel handler overwrite** by unsubscribing old before new - MainWindowViewModel.EventHandlers.cs:137-154
5. **Proper error handling for GC.Collect()** with async/await and logging - MainWindowViewModel.EventHandlers.cs:216-223
6. **Validate RelayCommand<T> parameter types** to catch binding errors early - RelayCommand.cs:118

### Priority 3 (Nice to Have)
7. Add integration tests that:
   - Load/unload files repeatedly and monitor memory growth
   - Verify all event handlers are unsubscribed on Dispose
   - Test theme changes during comparison updates
   - Verify no orphaned ViewModels remain after cleanup

---

## TESTING RECOMMENDATIONS

### Memory Leak Test
```csharp
[Test]
public void MainWindowViewModel_Dispose_DisposesAllChildren()
{
    var vm = new MainWindowViewModel(...);
    var searchVM = vm.SearchViewModel;
    var fileDetailsVM = vm.FileDetailsViewModel;

    vm.Dispose();

    // Verify all event handlers are unsubscribed
    // Can't directly test, but verify no exceptions from orphaned handlers
}
```

### Handler Cleanup Test
```csharp
[Test]
public void SearchHistoryItem_MultipleSearches_NoOrphanedHandlers()
{
    var viewModel = new TreeSearchResultsViewModel(...);

    // Simulate 10 searches
    for (int i = 0; i < 10; i++)
    {
        viewModel.AddSearchResults($"query{i}", CreateTestResults());
    }

    // Only 5 items should remain in history
    Assert.AreEqual(5, viewModel.SearchHistory.Count);

    // Dispose
    viewModel.Dispose();

    // Verify no event handlers remain
    // (Can't directly test EventHandler list, but verify no crashes on GC)
}
```

---

## APPENDIX: Files Analyzed

- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.EventHandlers.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.Commands.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/SearchViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/FileDetailsViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/RowComparisonViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/TreeSearchResultsViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/SearchHistoryItem.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/ViewModels/FileLoadResultViewModel.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Files/LoadedFilesManager.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Comparison/RowComparisonCoordinator.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Search/SearchResultsManager.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/Selection/SelectionManager.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Managers/FileDetails/FileDetailsCoordinator.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/Commands/RelayCommand.cs`
- `/data/repos/sheet-atlas/src/SheetAtlas.UI.Avalonia/App.axaml.cs`

---

## CONCLUSION

SheetAtlas has **solid architectural foundations** with proper MVVM compliance, dependency injection, and async patterns. The identified issues are **not fundamental design flaws**, but rather **edge cases in event handler lifecycle management** that accumulate with long app sessions or specific user action sequences.

**Estimated impact**:
- **No immediate crashes** in normal usage
- **Memory growth** over 8+ hours of active file operations
- **Potential hangs** if too many orphaned handlers accumulate
- **Reliable reproduction** requires specific test sequences

All issues are **fixable with localized changes** (no architecture redesign needed). The fixes total approximately **4-6 hours of work** and should be completed before commercial release.

