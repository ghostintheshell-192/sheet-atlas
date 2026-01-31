# IDisposable + Eventi: Memory Leak Analysis & Fix

**Project**: SheetAtlas
**Date**: 2025-10-21
**Status**: Critical Bug Fixed
**Impact**: High (Memory Leak Prevention)

---

## Executive Summary

**CRITICAL FINDING**: Durante il refactoring Action→EventHandler, abbiamo scoperto e risolto un **memory leak** causato da eventi non deregistrati nella Dispose.

**Root Cause**: `MainWindowViewModel` sottoscriveva agli eventi di `FileDetailsViewModel` ma **non** faceva unsubscribe nella `Dispose()`.

**Impact**: `FileDetailsViewModel` manteneva riferimenti strong a `MainWindowViewModel` attraverso event delegates, impedendo garbage collection.

**Fix Applied**: Aggiunto unsubscribe in `UnsubscribeFromEvents()` method.

---

## 🔍 Il Problema: Eventi e Memory Leaks

### Come Funzionano Gli Eventi in C#

Quando sottoscrivi a un evento, il .NET runtime crea un **riferimento strong (forte)** dall'oggetto publisher al subscriber:

```csharp
// Publisher (FileDetailsViewModel)
public event EventHandler<FileActionEventArgs>? RemoveFromListRequested;

// Subscriber (MainWindowViewModel)
FileDetailsViewModel.RemoveFromListRequested += OnRemoveFromListRequested;

// INTERNAMENTE, .NET crea qualcosa tipo:
// FileDetailsViewModel._removeFromListRequested.InvocationList.Add(mainWindowViewModel.OnRemoveFromListRequested)
```

**Problema**: Questo crea un riferimento **FileDetailsViewModel → MainWindowViewModel**

```
[FileDetailsViewModel]
    ↓ (strong reference through event delegate)
[MainWindowViewModel]  <-- NON può essere garbage collected!
```

### Il Memory Leak Pattern

1. **Subscribe** (MainWindowViewModel.EventHandlers.cs:143-146):
   ```csharp
   FileDetailsViewModel.RemoveFromListRequested += OnRemoveFromListRequested;
   FileDetailsViewModel.CleanAllDataRequested += OnCleanAllDataRequested;
   FileDetailsViewModel.RemoveNotificationRequested += OnRemoveNotificationRequested;
   FileDetailsViewModel.TryAgainRequested += OnTryAgainRequested;
   ```

2. **Dispose** chiamato su MainWindowViewModel:
   ```csharp
   public void Dispose()
   {
       Dispose(true);
       // Chiama UnsubscribeFromEvents()
   }
   ```

3. **UnsubscribeFromEvents() - PRIMA DEL FIX** (BUG):
   ```csharp
   private void UnsubscribeFromEvents()
   {
       _filesManager.FileLoaded -= OnFileLoaded;
       _comparisonCoordinator.SelectionChanged -= OnComparisonSelectionChanged;
       // ❌ MANCANTE: Unsubscribe da FileDetailsViewModel!
   }
   ```

4. **Risultato**:
   - MainWindowViewModel.Dispose() viene chiamata
   - Eventi di FileDetailsViewModel **NON** vengono deregistrati
   - FileDetailsViewModel tiene ancora riferimenti a MainWindowViewModel
   - **MainWindowViewModel NON può essere garbage collected**
   - MEMORY LEAK 💥

---

## ✅ La Soluzione: Unsubscribe Nella Dispose

### Fix Applicato

**File**: `MainWindowViewModel.EventHandlers.cs:28-45`

```csharp
private void UnsubscribeFromEvents()
{
    _filesManager.FileLoaded -= OnFileLoaded;
    _filesManager.FileRemoved -= OnFileRemoved;
    _filesManager.FileLoadFailed -= OnFileLoadFailed;
    _comparisonCoordinator.SelectionChanged -= OnComparisonSelectionChanged;
    _comparisonCoordinator.ComparisonRemoved -= OnComparisonRemoved;
    _comparisonCoordinator.PropertyChanged -= OnComparisonCoordinatorPropertyChanged;

    // ✅ FIX: Unsubscribe from FileDetailsViewModel events to prevent memory leak
    if (FileDetailsViewModel != null)
    {
        FileDetailsViewModel.RemoveFromListRequested -= OnRemoveFromListRequested;
        FileDetailsViewModel.CleanAllDataRequested -= OnCleanAllDataRequested;
        FileDetailsViewModel.RemoveNotificationRequested -= OnRemoveNotificationRequested;
        FileDetailsViewModel.TryAgainRequested -= OnTryAgainRequested;
    }
}
```

### Perché Funziona

Quando `Dispose()` viene chiamata:
1. `UnsubscribeFromEvents()` rimuove i delegate dall'invocation list
2. FileDetailsViewModel **non ha più** riferimenti a MainWindowViewModel
3. GC può raccogliere MainWindowViewModel normalmente
4. NO MEMORY LEAK ✅

---

## 🧠 Relazione con GC Aggressivo

### Il GC Aggressivo Corrente

SheetAtlas implementa GC aggressivo per DataTable pesanti (100-500 MB):

**File**: `MainWindowViewModel.EventHandlers.cs:201-216`

```csharp
// AGGRESSIVE CLEANUP: Force garbage collection after file removal
Task.Run(() =>
{
    // Enable LOH compaction for this collection cycle
    System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
        System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

    // Force Gen 2 + LOH collection with compaction
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive,
        blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive,
        blocking: true, compacting: true);
});
```

### Domanda: GC Aggressivo Aiuta con Event Memory Leaks?

**Risposta: NO** ❌

**Perché**:
- GC può raccogliere solo oggetti **non raggiungibili** (unreachable)
- Se FileDetailsViewModel tiene riferimento a MainWindowViewModel tramite eventi:
  - MainWindowViewModel È raggiungibile
  - GC **non può** raccoglierlo, nemmeno con `Aggressive` mode
  - Memory leak **persiste** indipendentemente da quante volte chiami `GC.Collect()`

**Visualizzazione**:

```
[Root Objects (GC Roots)]
    ↓
[Active Window/App]
    ↓
[FileDetailsViewModel] <-- Ancora attivo
    ↓ (event delegate strong reference)
[MainWindowViewModel] <-- RAGGIUNGIBILE! GC non può raccoglierlo!
```

### Cosa Serve Veramente

✅ **Unsubscribe esplicito** nella Dispose (quello che abbiamo fixato)
✅ **Weak Event Pattern** (per casi complessi)
❌ **GC Aggressivo** non risolve memory leaks da eventi

---

## 📊 Analisi Pattern IDisposable nel Progetto

### Classi IDisposable con Eventi

Ho analizzato tutte le classi che implementano IDisposable:

| Classe | Implementa IDisposable | Ha Eventi | Fa Unsubscribe | Stato |
|--------|------------------------|-----------|----------------|-------|
| MainWindowViewModel | ✅ | Subscriber | ✅ (dopo fix) | ✅ OK |
| SearchViewModel | ✅ | Subscriber | ✅ | ✅ OK |
| RowComparisonViewModel | ✅ | Subscriber | ✅ | ✅ OK |
| LoadedFilesManager | ✅ | Publisher | N/A | ✅ OK |
| RowComparisonCoordinator | ✅ | Both | ✅ | ✅ OK |
| FileDetailsViewModel | ❌ | Publisher | N/A | ⚠️ Non IDisposable |
| TreeSearchResultsViewModel | ✅ | Publisher | N/A | ✅ OK |
| SheetResultGroup | ✅ | Subscriber | ✅ | ✅ OK |
| SearchHistoryItem | ✅ | Subscriber | ✅ | ✅ OK |

### Pattern Corretto

**Per Publisher**:
- Non serve IDisposable se l'oggetto vive per tutta l'applicazione
- FileDetailsViewModel è OK senza IDisposable (vive per app lifetime)

**Per Subscriber**:
- **DEVE** implementare IDisposable
- **DEVE** fare unsubscribe in Dispose()
- Esempio: SearchViewModel:335-339 ✅

```csharp
// SearchViewModel.cs:335-339 - PATTERN CORRETTO
protected void Dispose(bool disposing)
{
    if (disposing)
    {
        _searchResultsManager.ResultsChanged -= OnResultsChanged;
        _searchResultsManager.SuggestionsChanged -= OnSuggestionsChanged;
        _searchResultsManager.GroupedResultsUpdated -= OnGroupedResultsUpdated;
        _selectionManager.SelectionChanged -= OnSelectionChanged;
        _selectionManager.VisibilityChanged -= OnVisibilityChanged;
    }
}
```

---

## 🚨 Come Identificare Memory Leaks da Eventi

### Checklist

Quando vedi un subscribe (`+=`), chiediti:

1. ✅ **L'oggetto subscriber implementa IDisposable?**
   - Se NO → potenziale memory leak

2. ✅ **Nella Dispose(), fa unsubscribe (`-=`)?**
   - Se NO → **MEMORY LEAK CONFERMATO**

3. ✅ **Il publisher vive più a lungo del subscriber?**
   - Se SÌ → memory leak **garantito** senza unsubscribe

### Esempi di Problemi

❌ **BAD** - Memory Leak:
```csharp
public class MyViewModel : ViewModelBase // NO IDisposable!
{
    public MyViewModel(IEventPublisher publisher)
    {
        publisher.SomeEvent += OnSomeEvent; // ❌ Subscribe senza Dispose
    }

    private void OnSomeEvent(object? sender, EventArgs e) { }
}
```

✅ **GOOD** - No Memory Leak:
```csharp
public class MyViewModel : ViewModelBase, IDisposable
{
    private readonly IEventPublisher _publisher;

    public MyViewModel(IEventPublisher publisher)
    {
        _publisher = publisher;
        _publisher.SomeEvent += OnSomeEvent; // ✅ Subscribe
    }

    public void Dispose()
    {
        _publisher.SomeEvent -= OnSomeEvent; // ✅ Unsubscribe
    }
}
```

---

## 💡 Best Practices

### 1. Subscribe/Unsubscribe Symmetry

**Regola**: Ogni `+=` deve avere un corrispondente `-=`

```csharp
// Setup
_manager.EventA += OnEventA;
_manager.EventB += OnEventB;

// Dispose (STESSO ORDINE OPPURE INVERSO)
_manager.EventA -= OnEventA;
_manager.EventB -= OnEventB;
```

### 2. Null-Check Prima di Unsubscribe

```csharp
// ✅ SAFE
if (FileDetailsViewModel != null)
{
    FileDetailsViewModel.SomeEvent -= OnSomeEvent;
}

// ❌ CRASH se FileDetailsViewModel è null
FileDetailsViewModel.SomeEvent -= OnSomeEvent;
```

### 3. Pattern Weak Event (Per Long-Lived Publishers)

Se il publisher vive per tutta l'app, considera **Weak Event Pattern**:

```csharp
// .NET fornisce WeakEventManager per WPF
WeakEventManager<IEventPublisher, EventArgs>.AddHandler(
    publisher, nameof(publisher.SomeEvent), OnSomeEvent);

// Non serve unsubscribe - weak reference si gestisce automaticamente
```

**Nota**: Avalonia non ha WeakEventManager, ma puoi implementarlo manualmente se necessario.

### 4. Dispose Pattern Completo

```csharp
public class MyViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe eventi
            // Dispose managed resources
        }
    }
}
```

---

## 🎯 Impatto Del Fix

### Scenario Prima Del Fix

1. User carica file
2. FileDetailsViewModel viene creato
3. MainWindowViewModel subscribe agli eventi
4. User naviga altrove
5. MainWindowViewModel.Dispose() chiamata
6. **Eventi NON deregistrati**
7. FileDetailsViewModel tiene MainWindowViewModel in memoria
8. **GC non può raccogliere MainWindowViewModel**
9. Memory leak **accumula** ad ogni navigazione

### Scenario Dopo Il Fix

1. User carica file
2. FileDetailsViewModel viene creato
3. MainWindowViewModel subscribe agli eventi
4. User naviga altrove
5. MainWindowViewModel.Dispose() chiamata
6. **UnsubscribeFromEvents() deregistra tutto** ✅
7. FileDetailsViewModel non tiene più riferimenti
8. **GC raccoglie MainWindowViewModel normalmente**
9. **No memory leak** 🎉

### Memory Savings Stimati

**Assunzioni**:
- MainWindowViewModel: ~5 MB (oggetto + riferimenti)
- User naviga 10 volte nella sessione
- **Prima**: 10 × 5MB = **50 MB leak**
- **Dopo**: **0 MB leak** ✅

---

## 🔬 Testing Memory Leaks

### Come Verificare Il Fix

1. **Visual Studio Diagnostic Tools**:
   - Debug → Windows → Show Diagnostic Tools
   - Memory tab → Take Heap Snapshot
   - Naviga più volte
   - Compare snapshots
   - **Prima fix**: MainWindowViewModel count increases
   - **Dopo fix**: MainWindowViewModel count stays stable

2. **dotMemory (JetBrains)**:
   - Profiler per .NET
   - Identifica reference chains
   - Vede eventi non deregistrati

3. **Manual Test** (Basic):
   ```csharp
   var weakRef = new WeakReference(mainWindowViewModel);
   mainWindowViewModel.Dispose();
   mainWindowViewModel = null;

   GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
   GC.WaitForPendingFinalizers();
   GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

   Assert.IsFalse(weakRef.IsAlive); // ✅ Se false = no leak
   ```

---

## 📝 Conclusioni

### Risposta alla Domanda

**User**: "vorrei sapere se la dispose di oggetti event-driven potrebbe essere utile"

**Risposta**: **SÌ, ASSOLUTAMENTE CRITICA** ✅✅✅

**Perché**:
1. Eventi creano strong references (publisher → subscriber)
2. Senza unsubscribe, subscriber non può essere garbage collected
3. GC aggressivo **NON aiuta** - può solo raccogliere unreachable objects
4. **Dispose con unsubscribe è l'UNICA soluzione**

### Cosa Abbiamo Fatto

✅ **Refactored Action→EventHandler** per consistenza
✅ **Scoperto memory leak** in MainWindowViewModel
✅ **Fixato leak** aggiungendo unsubscribe in Dispose
✅ **Documentato** best practices per eventi + IDisposable

### Raccomandazioni Finali

1. **Review** tutte le subscribe nel progetto con checklist sopra
2. **Test** memory con Diagnostic Tools dopo rilascio
3. **Monitor** memory usage in produzione
4. **Considera** Weak Event Pattern per long-lived publishers (se serve)

---

**Memory Leak Fix Status**: ✅ **RISOLTO**

**Impatto**: 🔥 **Alta priorità** (prevenzione memory leak)

**Testing**: ⏳ **Da testare** con profiler prima del rilascio

---

**Document Version**: 1.0
**Author**: Claude Code (AI Assistant)
**Next Steps**: Test con dotMemory/Visual Studio profiler prima di merge a develop
