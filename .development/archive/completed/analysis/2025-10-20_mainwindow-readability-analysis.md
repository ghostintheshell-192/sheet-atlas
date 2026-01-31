# MainWindowViewModel Readability Analysis

**Date**: 2025-10-20
**File**: `src/SheetAtlas.UI.Avalonia/ViewModels/MainWindowViewModel.cs`
**Current Size**: 690 lines, 26 methods, 15 commands

---

## Executive Summary

MainWindowViewModel è complesso ma **ben strutturato**. La complessità deriva dal ruolo di **orchestratore centrale**, non da cattiva architettura.

**Opportunità di miglioramento identificate**: 7 strategie per migliorare leggibilità senza stravolgere l'architettura.

---

## Current Structure Analysis

### Breakdown per Responsabilità

```
┌─────────────────────────────────────────────────────────────┐
│ MainWindowViewModel (690 lines)                             │
├─────────────────────────────────────────────────────────────┤
│ 1. Dependencies (9 services)                        ~30 L   │
│ 2. State fields (5 fields)                          ~15 L   │
│ 3. Pass-through properties (10 delegations)         ~50 L   │
│ 4. Commands initialization (15 commands)            ~100 L  │
│ 5. Event subscriptions (12 event handlers)          ~150 L  │
│ 6. SetViewModel methods (3 methods)                 ~50 L   │
│ 7. File operations (Load, Unload)                   ~80 L   │
│ 8. File lifecycle event handlers (4 handlers)       ~50 L   │
│ 9. Help menu methods (3 methods)                    ~90 L   │
│ 10. IDisposable implementation                      ~45 L   │
│ 11. Whitespace, comments, braces                    ~30 L   │
└─────────────────────────────────────────────────────────────┘
```

### Complexity Hotspots

**High complexity** (>25 lines):

1. ✅ `OnTabNavigatorPropertyChanged` (24 lines) - repetitive if/else chain
2. ✅ `UnloadAllFilesAsync` (46 lines) - multi-step cleanup
3. ✅ `LoadFileAsync` (35 lines) - try/catch with dialog
4. ✅ `SelectedFile` setter (36 lines) - anti-flicker logic + coordination

**Medium complexity** (15-25 lines):
5. `ShowAboutDialogAsync`, `OpenDocumentationAsync`, `OpenErrorLogAsync`
6. Event handlers for file lifecycle

**Low complexity** (<15 lines):

- Command initialization (mostly single-line delegates)
- Simple pass-through properties

---

## Readability Improvement Strategies

### Strategy 1: Extract Property Propagation to Helper Method ⭐ HIGH IMPACT - COMPLETED

### Strategy 2: Command Initialization with Command Dictionary Pattern

**Problem**: 15 separate command properties + initialization

**Current Code** (lines 134-244):

```csharp
public ICommand LoadFileCommand { get; }
public ICommand UnloadAllFilesCommand { get; }
public ICommand ToggleThemeCommand { get; }
// ... 12 more ...

public MainWindowViewModel(...)
{
    // ... dependencies ...

    LoadFileCommand = new RelayCommand(async () => await LoadFileAsync());
    UnloadAllFilesCommand = new RelayCommand(async () => await UnloadAllFilesAsync());
    ToggleThemeCommand = new RelayCommand(() => { ThemeManager.ToggleTheme(); return Task.CompletedTask; });
    // ... 12 more ...
}
```

**Proposed Solution A: Extract to CommandsInitializer** (Partial Class):

```csharp
// MainWindowViewModel.cs (main file)
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    // Fields, properties, main logic...
}

// MainWindowViewModel.Commands.cs (partial file)
public partial class MainWindowViewModel
{
    public ICommand LoadFileCommand { get; }
    public ICommand UnloadAllFilesCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    // ... all commands ...

    private void InitializeCommands()
    {
        LoadFileCommand = new RelayCommand(async () => await LoadFileAsync());
        UnloadAllFilesCommand = new RelayCommand(async () => await UnloadAllFilesAsync());
        ToggleThemeCommand = new RelayCommand(() =>
        {
            ThemeManager.ToggleTheme();
            return Task.CompletedTask;
        });
        // ... all command initialization ...
    }
}
```

**Proposed Solution B: Group with #regions** (Simpler):

```csharp
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    #region Commands
    public ICommand LoadFileCommand { get; }
    public ICommand UnloadAllFilesCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    // ... all 15 commands ...
    #endregion

    #region Constructor and Initialization
    public MainWindowViewModel(...)
    {
        // Dependencies...

        // Initialize commands
        LoadFileCommand = new RelayCommand(async () => await LoadFileAsync());
        UnloadAllFilesCommand = new RelayCommand(async () => await UnloadAllFilesAsync());
        // ...
    }
    #endregion
}
```

**Benefits**:

- ✅ Solution A: Commands in separate file (-100 lines from main file)
- ✅ Solution B: Collapsible regions, easier navigation
- ✅ Clear separation of concerns

**Recommendation**: **Solution B** (regions) - simpler, no file splitting needed

**Estimated impact**: Visual organization improvement, easier navigation

---

### Strategy 3: Extract Help Menu Methods to Partial Class

**Problem**: Help menu methods (ShowAbout, OpenDocumentation, ViewErrorLog) aggiungono ~90 righe ma non sono core logic

**Proposed Solution**:

```csharp
// MainWindowViewModel.HelpCommands.cs (partial file)
public partial class MainWindowViewModel
{
    private async Task ShowAboutDialogAsync()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var message = $"SheetAtlas - Excel Cross Reference Viewer\n" +
                     $"Version: {version}\n\n" +
                     // ...
        await _dialogService.ShowInformationAsync(message, "About");
        _logger.LogInfo("Displayed About dialog", "MainWindowViewModel");
    }

    private async Task OpenDocumentationAsync() { /* ... */ }
    private async Task OpenErrorLogAsync() { /* ... */ }
}
```

**Benefits**:

- ✅ -90 lines from main file
- ✅ Help-related logic grouped together
- ✅ Easier to find and maintain

**Estimated impact**: ~90 lines moved to separate file

---

### Strategy 4: Simplify UnloadAllFilesAsync with Helper Methods

**Problem**: UnloadAllFilesAsync è 46 righe con molti step sequenziali

**Current Code** (lines 417-462):

```csharp
private async Task UnloadAllFilesAsync()
{
    if (!LoadedFiles.Any()) return;

    // Ask for confirmation
    var confirmed = await _dialogService.ShowConfirmationAsync(...);
    if (!confirmed) return;

    _activityLog.LogInfo(...);

    // Clear selection first
    SelectedFile = null;

    // Clear all comparisons first
    var comparisonsToRemove = RowComparisons.ToList();
    foreach (var comparison in comparisonsToRemove)
    {
        _comparisonCoordinator.RemoveComparison(comparison);
    }

    // Clear all search results
    TreeSearchResultsViewModel?.ClearHistory();
    SearchViewModel?.ClearSearchCommand.Execute(null);

    // Remove all files (iterate backwards to avoid collection modification issues)
    var filesToRemove = LoadedFiles.ToList();
    foreach (var file in filesToRemove)
    {
        file.Dispose();
        _filesManager.RemoveFile(file);
    }

    _activityLog.LogInfo("All files unloaded successfully", "FileUnload");
    _logger.LogInfo($"Unloaded {filesToRemove.Count} file(s)", "MainWindowViewModel");
}
```

**Proposed Solution**:

```csharp
private async Task UnloadAllFilesAsync()
{
    if (!LoadedFiles.Any()) return;

    if (!await ConfirmUnloadAllAsync()) return;

    _activityLog.LogInfo($"Unloading all {LoadedFiles.Count} file(s)...", "FileUnload");

    ClearAllUIState();
    RemoveAllComparisons();
    ClearAllSearchResults();
    RemoveAllFiles();

    _activityLog.LogInfo("All files unloaded successfully", "FileUnload");
}

private async Task<bool> ConfirmUnloadAllAsync()
{
    return await _dialogService.ShowConfirmationAsync(
        $"Are you sure you want to unload all {LoadedFiles.Count} file(s)?\n\n" +
        "This will clear all data, search results, and comparisons.",
        "Unload All Files");
}

private void ClearAllUIState()
{
    SelectedFile = null;
}

private void RemoveAllComparisons()
{
    foreach (var comparison in RowComparisons.ToList())
    {
        _comparisonCoordinator.RemoveComparison(comparison);
    }
}

private void ClearAllSearchResults()
{
    TreeSearchResultsViewModel?.ClearHistory();
    SearchViewModel?.ClearSearchCommand.Execute(null);
}

private void RemoveAllFiles()
{
    var filesToRemove = LoadedFiles.ToList();
    foreach (var file in filesToRemove)
    {
        file.Dispose();
        _filesManager.RemoveFile(file);
    }
    _logger.LogInfo($"Unloaded {filesToRemove.Count} file(s)", "MainWindowViewModel");
}
```

**Benefits**:

- ✅ Main method diventa self-documenting (6 righe invece di 46)
- ✅ Ogni step ha nome descrittivo
- ✅ Facile testare singoli step
- ✅ Riutilizzabili (es. ClearAllSearchResults potrebbe servire altrove)

**Drawback**: ⚠️ 5 metodi invece di 1 (ma più leggibili)

**Estimated impact**: Main method -40 lines, +4 helper methods (~10 lines each)

---

### Strategy 5: Consolidate Event Handler Delegates

**Problem**: Troppi one-liner event handler delegates (lines 465-499)

**Current Code**:

```csharp
private void OnRemoveFromListRequested(IFileLoadResultViewModel? file) =>
    _fileDetailsCoordinator.HandleRemoveFromList(file);

private void OnCleanAllDataRequested(IFileLoadResultViewModel? file) =>
    _fileDetailsCoordinator.HandleCleanAllData(
        file,
        TreeSearchResultsViewModel,
        SearchViewModel,
        fileToCheck =>
        {
            if (SelectedFile == fileToCheck)
            {
                SelectedFile = null;
            }
        });

private void OnRemoveNotificationRequested(IFileLoadResultViewModel? file) =>
    _fileDetailsCoordinator.HandleRemoveNotification(file);
```

**Proposed Solution**: Keep as-is OR inline subscription

**Option A: Keep current** (RECOMMENDED)

- ✅ Event handler nomi descrittivi
- ✅ Facile debugging (nome metodo in stack trace)
- ✅ Possibilità di aggiungere logica in futuro

**Option B: Inline** (NOT recommended - peggiora leggibilità)

```csharp
// Constructor
FileDetailsViewModel.RemoveFromListRequested += (file) =>
    _fileDetailsCoordinator.HandleRemoveFromList(file);
```

**Recommendation**: **Keep current approach** - è già ottimale

---

### Strategy 6: Extract SelectedFile Setter Logic to Method

**Problem**: SelectedFile setter ha 36 righe con logica complessa (anti-flicker)

**Current Code** (lines 50-86):

```csharp
public IFileLoadResultViewModel? SelectedFile
{
    get => _selectedFile;
    set
    {
        // Prevent auto-deselection during file retry to avoid UI flicker
        if (value == null && _retryingFile != null)
        {
            return;
        }

        if (SetField(ref _selectedFile, value))
        {
            // Clear retry flag when new file is selected
            _retryingFile = null;

            // Update FileDetailsViewModel when selection changes
            if (FileDetailsViewModel != null)
            {
                FileDetailsViewModel.SelectedFile = value;
            }

            // Show/hide File Details tab based on selection
            if (value != null)
            {
                _tabNavigator.ShowFileDetailsTab();
            }
            else
            {
                _tabNavigator.CloseFileDetailsTab();
            }
        }
    }
}
```

**Proposed Solution**:

```csharp
public IFileLoadResultViewModel? SelectedFile
{
    get => _selectedFile;
    set => UpdateSelectedFile(value);
}

private void UpdateSelectedFile(IFileLoadResultViewModel? newFile)
{
    // Prevent auto-deselection during file retry to avoid UI flicker
    if (ShouldBlockDeselection(newFile))
        return;

    if (!SetField(ref _selectedFile, newFile))
        return;

    // Clear retry flag when new file is selected
    _retryingFile = null;

    // Propagate selection to FileDetailsViewModel
    PropagateSelectionToFileDetails(newFile);

    // Update tab visibility based on selection
    UpdateFileDetailsTabVisibility(newFile);
}

private bool ShouldBlockDeselection(IFileLoadResultViewModel? newFile)
{
    return newFile == null && _retryingFile != null;
}

private void PropagateSelectionToFileDetails(IFileLoadResultViewModel? file)
{
    if (FileDetailsViewModel != null)
    {
        FileDetailsViewModel.SelectedFile = file;
    }
}

private void UpdateFileDetailsTabVisibility(IFileLoadResultViewModel? file)
{
    if (file != null)
    {
        _tabNavigator.ShowFileDetailsTab();
    }
    else
    {
        _tabNavigator.CloseFileDetailsTab();
    }
}
```

**Benefits**:

- ✅ Property setter diventa 1 riga
- ✅ Logica anti-flicker isolata con nome descrittivo
- ✅ Testabile individualmente
- ✅ Auto-documentante

**Estimated impact**: Property -30 lines, +4 helper methods (~15 lines total)

---

### Strategy 7: Partial Class File Organization

**Proposed Structure**:

```
MainWindowViewModel.cs                  (~250 lines)
├── Dependencies, fields
├── Core properties (SelectedFile, LoadedFiles, etc.)
├── Main orchestration logic
└── IDisposable

MainWindowViewModel.Commands.cs         (~120 lines)
├── All ICommand properties
└── Command initialization

MainWindowViewModel.EventHandlers.cs    (~180 lines)
├── All event handler subscriptions
├── Event handler methods
└── Propagation helpers

MainWindowViewModel.HelpCommands.cs     (~90 lines)
├── ShowAboutDialogAsync
├── OpenDocumentationAsync
└── ViewErrorLogAsync

MainWindowViewModel.FileOperations.cs   (~50 lines)
├── LoadFileAsync
└── UnloadAllFilesAsync (with helpers)
```

**Benefits**:

- ✅ Main file ~250 lines (from 690) - molto più leggibile!
- ✅ Ogni file ha responsabilità chiara
- ✅ Facile navigazione (jump to file per concern)
- ✅ Collassabile in Solution Explorer

**Drawbacks**:

- ⚠️ 5 file invece di 1 (ma organizzati logicamente)
- ⚠️ Bisogna ricordarsi dove cercare (mitigato da naming chiaro)

---

## Recommended Implementation Plan

### Phase 2: Extract Complex Logic (2-3 hours, low risk)

1. **Simplify UnloadAllFilesAsync** (Strategy 4)
   - Extract to self-documenting helper methods

   **Impact**: -40 lines, clearer intent

### Phase 3: File Organization (3-4 hours, medium effort)

2. ⚠️ **Split into partial classes** (Strategy 7)
   - MainWindowViewModel.Commands.cs
   - MainWindowViewModel.EventHandlers.cs
   - MainWindowViewModel.HelpCommands.cs
   - MainWindowViewModel.FileOperations.cs

   **Impact**: Main file 250 lines (from 690), organized by concern

**Total estimated effort**: 5-7 hours
**Total impact**: Main file size -60% (690 → 250 lines)

---

## Conclusion

MainWindowViewModel è complesso perché è un **orchestratore centrale** - questa è la sua natura.

**Recommended approach**:

1. **Short term**: Implement Phase 1 + Phase 2 (regions + extract complex logic) → ~60% readability improvement
2. **Long term**: Consider P1.4 (SelectionManager extraction) quando fai event-driven full migration

**Don't do**: Non creare MenuBarViewModel, StatusBarViewModel, etc. - peggiorano solo la situazione (binding hell).

**Philosophy**: Preferisci **"clear separation of concerns within single file"** (regions) vs **"multiple tiny files"** (partial classes) - questione di gusti del team.

---

*Analysis completed: 2025-10-20*
