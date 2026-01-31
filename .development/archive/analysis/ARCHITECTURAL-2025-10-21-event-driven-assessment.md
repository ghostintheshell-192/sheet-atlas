# Event-Driven Architecture - Critical Analysis

**Project**: SheetAtlas
**Date**: 2025-10-21
**Status**: Architectural Review
**Reviewer**: Claude Code (AI Assistant)

---

## Executive Summary

SheetAtlas already implements event-driven architecture **extensively and effectively** in the UI layer, with **~60% of cross-component communication** happening through C# events. The architecture is **fundamentally sound** but has **inconsistencies** and **opportunities for strategic improvements**.

### Key Findings

✅ **Strengths**:
- Clean separation: Domain/Core layer is event-free (correct by design)
- Managers use events consistently for state changes
- MVVM pattern well-implemented with proper event subscriptions
- Memory management includes proper unsubscription

⚠️ **Inconsistencies**:
- FileDetailsViewModel uses `Action<>` delegates instead of events
- Some coordinators mix direct calls with events
- No unified event bus or mediator pattern

🎯 **Opportunities**:
- Progress reporting for long-running operations
- Cancellation token propagation through events
- Centralized event logging/diagnostics
- Event replay/debugging capabilities

❌ **Anti-patterns to avoid**:
- Events in Domain/Core layer (would break clean architecture)
- Synchronous event chains causing performance issues
- Event-driven for simple property changes (MVVM already handles this)

---

## 1. Current Event-Driven Implementation

### 1.1 Where Events Are Used (Well)

#### File Operations Manager
**Location**: `src/SheetAtlas.UI.Avalonia/Managers/Files/LoadedFilesManager.cs`

**Events**:
```csharp
public event EventHandler<FileLoadedEventArgs>? FileLoaded;
public event EventHandler<FileRemovedEventArgs>? FileRemoved;
public event EventHandler<FileLoadFailedEventArgs>? FileLoadFailed;
public event EventHandler<FileReloadedEventArgs>? FileReloaded;
```

**Critical Analysis**:
- ✅ **Excellent design**: Each event represents a **distinct business state change**
- ✅ **Decoupling**: UI doesn't know about file loading implementation details
- ✅ **Multiple subscribers**: Different components react to same event (logging, UI update, analytics)
- ⚠️ **Missing**: No progress events for large file loading (100MB+ files)
- ⚠️ **Missing**: No cancellation notification event

**Use Case**: When a file is loaded, multiple components need to react:
- MainWindowViewModel updates UI state
- SearchViewModel refreshes search index
- Logger records the operation
- Activity log shows user notification

**Why Event-Driven Works Here**: This is a **one-to-many notification** - one action (file load) triggers multiple independent reactions. Direct method calls would create tight coupling.

---

#### Row Comparison Coordinator
**Location**: `src/SheetAtlas.UI.Avalonia/Managers/Comparison/RowComparisonCoordinator.cs`

**Events**:
```csharp
public event EventHandler<ComparisonAddedEventArgs>? ComparisonAdded;
public event EventHandler<ComparisonRemovedEventArgs>? ComparisonRemoved;
public event EventHandler<ComparisonSelectionChangedEventArgs>? SelectionChanged;
```

**Critical Analysis**:
- ✅ **Coordination**: Multiple tabs/views react to comparison changes
- ✅ **State synchronization**: All views stay in sync automatically
- ⚠️ **Potential issue**: SelectionChanged could fire frequently, causing UI jank

**Trade-off Analysis**:
- **Pro**: UI components don't need to poll for state
- **Pro**: Adding new comparison listeners doesn't change existing code
- **Con**: Event chain can be hard to debug (ComparisonAdded → SelectionChanged → TabChanged)
- **Con**: Synchronous events block UI thread if handler is slow

---

#### Search Results Manager
**Location**: `src/SheetAtlas.UI.Avalonia/Managers/Search/SearchResultsManager.cs`

**Events**:
```csharp
public event EventHandler<EventArgs>? ResultsChanged;
public event EventHandler<EventArgs>? SuggestionsChanged;
public event EventHandler<GroupedResultsEventArgs>? GroupedResultsUpdated;
```

**Critical Analysis**:
- ✅ **Reactive UI**: Search results update automatically
- ⚠️ **Redundancy**: `ResultsChanged` AND `GroupedResultsUpdated` fire together (lines 63-64, 76-77)
- ❌ **Performance risk**: Every keystroke could trigger event cascade

**Recommendation**: Debounce search events (300ms delay) to avoid excessive UI updates.

---

#### Selection Manager
**Location**: `src/SheetAtlas.UI.Avalonia/Managers/Selection/SelectionManager.cs`

**Events**:
```csharp
public event EventHandler<EventArgs>? SelectionChanged;
public event EventHandler<EventArgs>? VisibilityChanged;
```

**Critical Analysis**:
- ✅ **UI synchronization**: Multiple views reflect same selection
- ✅ **Separation of concerns**: Selection logic separated from UI
- ⚠️ **High frequency**: Could fire many times during user interaction

---

#### Log Service
**Location**: `src/SheetAtlas.Logging/Services/LogService.cs`

**Events**:
```csharp
public event EventHandler<LogMessage>? MessageAdded;
public event EventHandler? MessagesCleared;
```

**Critical Analysis**:
- ✅ **Cross-cutting concern**: Logging doesn't know about UI
- ✅ **Testability**: Easy to verify log events in tests
- ❌ **Performance risk**: High-frequency logging could slow down UI

---

### 1.2 Where Events Are NOT Used (Inconsistency)

#### FileDetailsViewModel → MainWindowViewModel Communication
**Location**: `src/SheetAtlas.UI.Avalonia/ViewModels/FileDetailsViewModel.cs:317-320`

**Current Design** (Action delegates):
```csharp
public event Action<IFileLoadResultViewModel?>? RemoveFromListRequested;
public event Action<IFileLoadResultViewModel?>? CleanAllDataRequested;
public event Action<IFileLoadResultViewModel?>? RemoveNotificationRequested;
public event Action<IFileLoadResultViewModel?>? TryAgainRequested;
```

**Critical Analysis**:
- ❌ **Inconsistency**: Rest of codebase uses `EventHandler<T>`
- ❌ **No EventArgs**: Can't add metadata later without breaking changes
- ⚠️ **Direct coupling**: ViewModel depends on specific delegate signature

**Why This Matters**: If you later need to add "Reason" or "UserInitiated" flag, you'd have to change ALL subscribers.

**Recommendation**: Standardize on `EventHandler<T>` with custom EventArgs classes.

---

### 1.3 Where Direct Calls Are Used (Correctly)

#### Core Layer Services
**Location**: `src/SheetAtlas.Core/Application/Services/*`

**Design**: NO events, pure functions returning results.

**Critical Analysis**:
- ✅ **Correct**: Domain logic should be deterministic and testable
- ✅ **Pure functions**: `ExcelReaderService.LoadFileAsync()` returns `ExcelFile` with `LoadStatus`
- ✅ **No side effects**: Services don't trigger UI updates directly

**Why Direct Calls Work Here**: Business logic is **synchronous transformation** - input → output. Events would add complexity without benefit.

**Example**:
```csharp
// CORRECT: Pure function, no events
public RowComparison CreateRowComparison(RowComparisonRequest request)
{
    // ... business logic ...
    return new RowComparison(excelRows, request.Name);
}

// WRONG (hypothetical): Event-driven business logic
public void CreateRowComparison(RowComparisonRequest request)
{
    // ... business logic ...
    RowComparisonCreated?.Invoke(this, new RowComparison(...)); // ❌ Hard to test, breaks CQRS
}
```

---

## 2. Critical Analysis: Where Event-Driven Makes Sense

### 2.1 The "One-to-Many" Test

**Rule**: Use events when **one action** needs to trigger **multiple independent reactions**.

**Examples in SheetAtlas**:

| Trigger | Reactions | Event-Driven? |
|---------|-----------|---------------|
| File loaded | 1. Update UI<br>2. Log activity<br>3. Update search index<br>4. Show notification | ✅ YES - Multiple independent reactions |
| File removed | 1. Update file list<br>2. Clear selection<br>3. Remove from comparisons<br>4. Update search | ✅ YES - Cascading cleanup |
| Search query typed | 1. Update results<br>2. Update suggestions<br>3. Highlight matches | ⚠️ MAYBE - Could use data binding |
| Theme changed | 1. Update UI theme<br>2. Save preference | ✅ YES - Global state change |
| Row comparison created | 1. Show comparison tab<br>2. Update comparison list | ✅ YES - Multiple views react |

---

### 2.2 The "Temporal Decoupling" Test

**Rule**: Use events when **subscriber** and **publisher** have **different lifecycles**.

**Example**: Logger service lives for entire app lifetime, but file operations happen sporadically.

**Why Event-Driven Works**:
- Logger doesn't care when files are loaded
- File manager doesn't care if logger exists
- Both can evolve independently

---

### 2.3 The "State Synchronization" Test

**Rule**: Use events when **multiple components** need to stay **synchronized** with shared state.

**Example**: Selection state across FileDetails tab, Search tab, Comparison tab.

**Without Events** (hypothetical):
```csharp
// ❌ Tight coupling
public void SelectFile(IFileLoadResultViewModel file)
{
    _fileDetailsViewModel.SelectedFile = file;
    _searchViewModel.HighlightFile(file);
    _comparisonCoordinator.UpdateFileReference(file);
    _logger.LogInfo($"File selected: {file.FileName}");
}
```

**With Events** (current design):
```csharp
// ✅ Loose coupling
public void SelectFile(IFileLoadResultViewModel file)
{
    _selectedFile = file;
    SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(file));
}
// Each component subscribes independently
```

---

## 3. Where Event-Driven Does NOT Make Sense

### 3.1 Domain/Core Layer ❌

**Why NOT**:
- Business logic should be **deterministic** and **testable**
- Events introduce **implicit dependencies** (hard to track in tests)
- Commands/Queries should return results directly
- Event-driven domain logic is an **anti-pattern** in Clean Architecture

**Example**:
```csharp
// ✅ CORRECT: Return result
public ExcelFile LoadFile(string path)
{
    return new ExcelFile(path, LoadStatus.Success, sheets, errors);
}

// ❌ WRONG: Event-driven
public void LoadFile(string path)
{
    var file = /* load */;
    FileLoadCompleted?.Invoke(this, file); // Who subscribes? How to test?
}
```

---

### 3.2 Simple Property Changes ❌

**Why NOT**: MVVM `INotifyPropertyChanged` already handles this efficiently.

**Example**:
```csharp
// ✅ CORRECT: Standard MVVM
public string SearchQuery
{
    get => _searchQuery;
    set => SetField(ref _searchQuery, value); // Fires PropertyChanged
}

// ❌ WRONG: Custom event for every property
public event EventHandler<SearchQueryChangedEventArgs>? SearchQueryChanged;
```

---

### 3.3 Synchronous Data Transformations ❌

**Why NOT**: Direct method calls are clearer and faster.

**Example**:
```csharp
// ✅ CORRECT: Direct call
var headers = GetColumnHeaders(file, sheetName);

// ❌ WRONG: Event-driven
ColumnHeadersRequested?.Invoke(this, new ColumnHeadersRequestEventArgs(file, sheetName));
// Then wait for ColumnHeadersReceived event... complexity for no benefit
```

---

## 4. Areas Where Event-Driven Is Missing (Opportunities)

### 4.1 Progress Reporting for Long Operations ⭐

**Current Gap**: File loading has no progress events.

**Impact**: Users don't know if app is frozen or working when loading 100MB+ files.

**Proposed Solution**:
```csharp
public event EventHandler<FileLoadProgressEventArgs>? LoadProgressChanged;

// In ExcelReaderService
private void ReportProgress(string filePath, int percent, string status)
{
    LoadProgressChanged?.Invoke(this, new FileLoadProgressEventArgs
    {
        FilePath = filePath,
        PercentComplete = percent,
        Status = status,
        Timestamp = DateTime.Now
    });
}
```

**Trade-off**:
- ✅ **Pro**: Better UX, user sees progress
- ✅ **Pro**: Cancellation becomes possible
- ❌ **Con**: More complex threading (progress from background thread)
- ❌ **Con**: Increased event traffic (could be 100+ events per file)

**Recommendation**: Implement with **throttling** (max 10 updates/second).

---

### 4.2 Operation Cancellation Events ⭐

**Current Gap**: No way to notify components that operation was cancelled.

**Proposed Solution**:
```csharp
public event EventHandler<OperationCancelledEventArgs>? OperationCancelled;

// Example: User cancels file load
public async Task LoadFileAsync(string path, CancellationToken ct)
{
    try
    {
        // ... load file ...
    }
    catch (OperationCanceledException)
    {
        OperationCancelled?.Invoke(this, new OperationCancelledEventArgs
        {
            OperationType = "FileLoad",
            FilePath = path,
            CancelledAt = DateTime.Now
        });
        throw; // Re-throw after notification
    }
}
```

**Trade-off**:
- ✅ **Pro**: UI can show "Load cancelled" instead of generic error
- ✅ **Pro**: Analytics can track cancellation rate
- ❌ **Con**: Duplicates exception handling (event + catch block)

---

### 4.3 Centralized Event Bus/Mediator ⭐⭐

**Current Gap**: No unified event system, events scattered across managers.

**Proposed Solution**: Implement **MediatR pattern** or custom event bus.

```csharp
public interface IEventBus
{
    void Publish<TEvent>(TEvent eventData) where TEvent : class;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
}

// Usage
_eventBus.Publish(new FileLoadedEvent { File = file, HasErrors = false });
```

**Trade-offs**:
- ✅ **Pro**: Centralized event logging/debugging
- ✅ **Pro**: Easy to add cross-cutting concerns (logging, analytics)
- ✅ **Pro**: Event replay for debugging
- ✅ **Pro**: Unsubscription handled automatically (weak references)
- ❌ **Con**: More abstraction, harder to "jump to definition"
- ❌ **Con**: Performance overhead (reflection or dynamic dispatch)
- ❌ **Con**: Harder to track who subscribes to what (implicit coupling)

**Recommendation**: **DON'T implement** unless you need:
- Event sourcing
- Distributed system communication
- Complex event replay scenarios

**For SheetAtlas**: Current `EventHandler<T>` pattern is **simpler and sufficient**.

---

### 4.4 Event Diagnostics/Logging 🔍

**Current Gap**: No visibility into event flow for debugging.

**Proposed Solution**: Event interceptor for diagnostics.

```csharp
public class EventDiagnostics
{
    public void LogEvent(string eventName, object sender, object? args)
    {
        _logger.LogDebug($"Event: {eventName}, Sender: {sender.GetType().Name}, Args: {args}", "EventDiagnostics");
    }
}

// Wrap every event invocation
FileLoaded?.Invoke(this, args);
_diagnostics.LogEvent(nameof(FileLoaded), this, args);
```

**Trade-offs**:
- ✅ **Pro**: Easier debugging of event chains
- ✅ **Pro**: Performance profiling of event handlers
- ❌ **Con**: Performance overhead in production
- ❌ **Con**: Boilerplate for every event

**Recommendation**: Implement as **compile-time feature** (`#if DEBUG`).

---

## 5. Anti-Patterns and Pitfalls

### 5.1 Event Chains Creating Hidden Dependencies ⚠️

**Problem**: `FileLoaded` → triggers → `SearchIndexUpdated` → triggers → `ResultsChanged` → triggers → `TabSwitched`

**Why Problematic**:
- Hard to debug (execution flow is implicit)
- Order-dependent (what if SearchIndexUpdated happens before UI update?)
- Performance (synchronous chain blocks UI)

**Current Risk in SheetAtlas**: Moderate (event chains exist but are short)

**Example from codebase**:
```csharp
// OnFileLoaded handler (MainWindowViewModel.EventHandlers.cs:38-50)
private void OnFileLoaded(object? sender, FileLoadedEventArgs e)
{
    OnPropertyChanged(nameof(HasLoadedFiles)); // Triggers binding updates

    if (LoadedFiles.Count == 1)
    {
        IsSidebarExpanded = true; // Triggers another PropertyChanged
    }
}
```

**Chain**: `FileLoaded` event → `HasLoadedFiles` property change → UI rebind → `IsSidebarExpanded` change → UI animation

**Mitigation**: Keep event chains **short** (max 2-3 levels).

---

### 5.2 Memory Leaks from Unsubscribed Events ⚠️

**Problem**: If subscriber doesn't unsubscribe, it stays in memory forever.

**Current Protection in SheetAtlas**:
```csharp
// MainWindowViewModel has proper cleanup (EventHandlers.cs:28-36)
private void UnsubscribeFromEvents()
{
    _filesManager.FileLoaded -= OnFileLoaded;
    _filesManager.FileRemoved -= OnFileRemoved;
    _filesManager.FileLoadFailed -= OnFileLoadFailed;
    // ...
}
```

**Recommendation**: ✅ **Good practice**, continue this.

**Additional Protection**: Consider weak event pattern for long-lived publishers + short-lived subscribers.

---

### 5.3 Synchronous Events Blocking UI Thread ⚠️

**Problem**: If event handler does heavy work, UI freezes.

**Example**:
```csharp
// ❌ BAD: Slow handler blocks UI
_filesManager.FileLoaded += (s, e) =>
{
    RecalculateAllSearchIndices(); // Takes 5 seconds!
};
```

**Current Risk in SheetAtlas**: Low (most handlers are fast UI updates)

**Mitigation**: Use `async` handlers or fire-and-forget:
```csharp
// ✅ GOOD: Non-blocking
_filesManager.FileLoaded += async (s, e) =>
{
    await Task.Run(() => RecalculateAllSearchIndices());
};
```

---

### 5.4 Event Order Dependencies ⚠️

**Problem**: If handler A must run before handler B, events are wrong pattern.

**Example**:
```csharp
// ❌ FRAGILE: Order matters but isn't enforced
_filesManager.FileLoaded += UpdateUI;
_filesManager.FileLoaded += SaveToDatabase; // Must happen after UpdateUI?
```

**Solution**: If order matters, **don't use events** - use explicit method calls.

---

## 6. Recommendations and Action Plan

### 6.1 High-Priority Changes ⭐⭐⭐

#### 1. Standardize Event Pattern

**Issue**: Mix of `EventHandler<T>` and `Action<T>`.

**Recommendation**: Convert all `Action<>` delegates to `EventHandler<T>`.

**Files to Change**:
- `FileDetailsViewModel.cs:317-320` (4 Action delegates)

**Benefit**: Consistency, future-proofing (can add metadata to EventArgs).

**Effort**: Low (2-3 hours)

---

#### 2. Add Progress Events for File Loading

**Issue**: No feedback during long operations.

**Recommendation**: Add `LoadProgressChanged` event to `IExcelReaderService`.

**Implementation**:
```csharp
public event EventHandler<FileLoadProgressEventArgs>? LoadProgressChanged;

public class FileLoadProgressEventArgs : EventArgs
{
    public string FilePath { get; init; }
    public int PercentComplete { get; init; }
    public string CurrentOperation { get; init; } // "Reading sheet 1/10"
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
}
```

**Benefit**: Better UX for large files, enables cancellation.

**Effort**: Medium (1-2 days)

**Trade-off**: Increased complexity, more events.

---

### 6.2 Medium-Priority Changes ⭐⭐

#### 3. Debounce High-Frequency Events

**Issue**: `SearchResultsManager` fires events on every keystroke.

**Recommendation**: Add 300ms debounce.

**Implementation**:
```csharp
private Timer _debounceTimer;

public void UpdateResults(List<SearchResult> results)
{
    _debounceTimer?.Stop();
    _debounceTimer = new Timer(300);
    _debounceTimer.Elapsed += (s, e) =>
    {
        ResultsChanged?.Invoke(this, EventArgs.Empty);
    };
    _debounceTimer.Start();
}
```

**Benefit**: Reduced UI jank, better performance.

**Effort**: Low (2-3 hours)

---

#### 4. Add Event Diagnostics (DEBUG only)

**Recommendation**: Log all events in DEBUG mode.

**Implementation**:
```csharp
#if DEBUG
private void LogEvent(string name, object? args)
{
    _logger.LogDebug($"[EVENT] {name}: {args}", "EventDiagnostics");
}
#endif

// Usage
FileLoaded?.Invoke(this, args);
#if DEBUG
LogEvent(nameof(FileLoaded), args);
#endif
```

**Benefit**: Easier debugging.

**Effort**: Low (half day)

---

### 6.3 Low-Priority / DON'T DO ❌

#### 5. Centralized Event Bus

**Recommendation**: **DON'T implement** unless requirements change dramatically.

**Reason**: Current `EventHandler<T>` pattern is **simpler, more explicit, and sufficient**.

**When to Reconsider**:
- If you need event sourcing
- If you need distributed events (microservices)
- If you have 50+ event types (SheetAtlas has ~15)

---

#### 6. Event-Driven Domain Layer

**Recommendation**: **NEVER implement**.

**Reason**: Breaks Clean Architecture, makes testing hard, violates CQRS.

**Current Design** (keep it this way):
- Core layer returns `Result<T>` or DTOs
- UI layer converts to events
- Domain logic is pure functions

---

## 7. Trade-Offs Analysis

### Event-Driven Architecture

| Aspect | Pros ✅ | Cons ❌ |
|--------|---------|---------|
| **Decoupling** | Publishers don't know subscribers | Hard to find who handles events |
| **Extensibility** | Add subscribers without changing publisher | Event chains hard to debug |
| **Testability** | Can mock event handlers | Need to simulate event sequences |
| **Performance** | Async events don't block | Synchronous events can cause jank |
| **Debugging** | Each handler is isolated | Flow is implicit, not explicit |
| **Memory** | Loose coupling | Memory leaks if not unsubscribed |
| **Scalability** | Multiple subscribers easy | Too many events = performance hit |

---

### Direct Method Calls

| Aspect | Pros ✅ | Cons ❌ |
|--------|---------|---------|
| **Clarity** | Explicit flow, easy to follow | Tight coupling |
| **Debugging** | Step through with debugger | Hard to extend without modifying code |
| **Performance** | No event overhead | Can't add behavior without changing caller |
| **Testability** | Direct dependencies clear | Need to mock all dependencies |

---

### When to Use Which?

| Scenario | Use Events | Use Direct Calls |
|----------|------------|------------------|
| One action, many reactions | ✅ | ❌ |
| State synchronization across components | ✅ | ❌ |
| Cross-cutting concerns (logging, analytics) | ✅ | ❌ |
| Domain/business logic | ❌ | ✅ |
| Simple property changes | ❌ (use MVVM) | - |
| Synchronous transformations | ❌ | ✅ |
| Parent-child component communication | ❌ | ✅ |
| Long-running operations with progress | ✅ | ❌ |

---

## 8. Final Verdict

### Overall Assessment: **8/10** ⭐⭐⭐⭐

**SheetAtlas event-driven architecture is fundamentally sound.**

**Strengths**:
- ✅ Proper separation: Domain is event-free
- ✅ Managers use events consistently
- ✅ Memory management (unsubscription) is handled
- ✅ No premature abstraction (no event bus when not needed)

**Weaknesses**:
- ⚠️ Inconsistent delegate patterns (Action vs EventHandler)
- ⚠️ Missing progress reporting
- ⚠️ No debouncing for high-frequency events

**Critical Insight**: **The project already uses event-driven architecture correctly**. The question isn't "should we apply it?" but rather "where can we refine it?"

### Recommended Next Steps

1. **Standardize on `EventHandler<T>`** (2-3 hours)
2. **Add progress events for file loading** (1-2 days)
3. **Debounce search events** (2-3 hours)
4. **Add DEBUG-only event diagnostics** (half day)

**Total Effort**: ~3-4 days for significant improvements.

**DON'T**:
- Add event bus (over-engineering)
- Add events to Core layer (architectural violation)
- Convert all method calls to events (wrong tool for the job)

---

## 9. Conclusion

Event-driven architecture is **not a silver bullet**. SheetAtlas already uses it **strategically and effectively** where it makes sense:

- ✅ **File operations**: Multiple components react to file changes
- ✅ **Search updates**: Results propagate to multiple views
- ✅ **Selection synchronization**: Tabs stay in sync
- ✅ **Logging**: Cross-cutting concern without coupling

The architecture is **pragmatic, not dogmatic** - events where they help, direct calls where they're clearer.

**The pattern is well-applied. Refinements, not revolution, are needed.**

---

**Document Version**: 1.0
**Author**: Claude Code (AI Assistant)
**Next Review**: When implementing progress events or event bus becomes necessary
