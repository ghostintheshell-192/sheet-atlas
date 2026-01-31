# Error Notification System - SheetAtlas

> Questo documento descrive il sistema di gestione e visualizzazione degli errori implementato in SheetAtlas.

## Filosofia

- **Fail Gracefully**: Gli errori non crashano l'applicazione, vengono gestiti e mostrati all'utente
- **Contestualità**: Errori mostrati inline accanto al file che li ha generati
- **Non Invasivo**: L'utente non viene distolto dal lavoro, può ignorare warning non critici
- **User-Friendly Messages**: Conversione automatica eccezioni tecniche → messaggi comprensibili
- **Persistenza Garantita**: Tutti gli errori >= Warning salvati su file log giornaliero
- **Recovery-First**: Possibilità di retry per operazioni fallite

---

## Architettura: 3 Livelli

### **Livello 1: Inline File Errors**

```drawing
┌────────────────────────────────────────────────┐
│  📄 report-2024.xlsx                    🔴 (3) │  ← Badge conteggio errori
│  ┌──────────────────────────────────────────┐  │
│  │ ⚠️ Errors and Warnings (3)               │  │
│  │ ──────────────────────────────────────── │  │
│  │ 🔴 Critical: File formato corrotto       │  │
│  │ ⚠️ Warning: Foglio 'Data' ignorato       │  │
│  │ ⚠️ Warning: Celle B2:B10 formato misto   │  │
│  └──────────────────────────────────────────┘  │
│  [Try Again] [Remove from List]                │
└────────────────────────────────────────────────┘
```

**Caratteristiche:**

- Ogni file caricato mostra inline gli errori incontrati
- Expander collassabile per nascondere/mostrare dettagli
- Icone di stato per LoadStatus:
  - 🔴 Hexagon rosso = Failed
  - ⚠️ Triangolo arancione = PartialSuccess (warning)
  - ✅ Cerchio verde = Success
- Badge con conteggio errori (sfondo rosso #FFEBEE)
- Colori per severity: Critical=#DC2626, Error=#EF4444, Warning=#F59E0B, Info=#3B82F6
- Azioni contestuali: "Try Again" per Failed, "Remove from List" per Success

**Implementazione:**

- `FileLoadResultView.axaml` - View con expander errori
- `FileLoadResultViewModel` - Espone `Errors` property filtrato (solo Critical/Error/Warning)
- `LogSeverityToColorConverter` - Mappa severity → colori
- Location-aware: Se ExcelError ha CellReference, mostra coordinate Excel (es. "Sheet1!B2")

**Vantaggi:**

- ✅ Non invasivo: errori visibili solo per file con problemi
- ✅ Contestuale: errori accanto al file che li ha generati
- ✅ Actionable: "Try Again" permette recovery immediato
- ✅ Scalabile: Supporta multi-errore per file (stack panel espandibile)

---

### **Livello 2: Modal Dialogs**

```drawing
┌─────────────────────────────────────────┐
│          ⚠️ File Load Error             │
│                                         │
│  Failed to load 3 files:                │
│                                         │
│  🔴 Critical Errors:                    │
│  • report.xlsx: File corrotto           │
│  • data.xlsx: Accesso negato            │
│                                         │
│  ⚠️ Warnings:                           │
│  • sales.xlsx: 2 fogli ignorati         │
│                                         │
│             [OK]                        │
└─────────────────────────────────────────┘
```

**Caratteristiche:**

- Mostrato per errori che richiedono attenzione immediata
- Raggruppamento automatico per severity (Critical/Error/Warning)
- Formattazione multi-errore con emoji per categorie
- Dialog modale che blocca interazione fino a dismissione
- Supporto light/dark theme con DynamicResource

**Implementazione:**

- `IErrorNotificationService` + `ErrorNotificationService`
  - `ShowExceptionAsync(Exception, string context)` - Converte eccezione via IExceptionHandler
  - `ShowErrorAsync(ExcelError)` - Mostra singolo errore
  - `ShowErrorsAsync(IEnumerable<ExcelError>, string title)` - Raggruppa e mostra
- `IDialogService` + `AvaloniaDialogService`
  - `ShowErrorAsync()`, `ShowWarningAsync()`, `ShowInformationAsync()`, `ShowConfirmationAsync()`
  - Crea Window programmaticamente (no MessageBox framework)

**Casi d'uso:**

- File load multiplo fallito (batch operation)
- Errori critici da OutOfMemoryException
- Operazioni che falliscono senza file specifico

**Vantaggi:**

- ✅ Attenzione garantita: utente deve dismissare per continuare
- ✅ Raggruppamento intelligente: evita spam di N dialog per N errori

---

### **Livello 3: File Log Persistente**

```text
📁 %LocalApplicationData%/SheetAtlas/Logs/
  ├── app-2025-10-18.log
  ├── app-2025-10-17.log
  └── app-2025-10-16.log
```

**Formato File:**

```text
[2025-10-18 14:23:15] [ERROR] File Load Failed
Context: LoadFileCommand
Message: File 'report.xlsx' could not be loaded: file is corrupted
Exception: System.IO.InvalidDataException: The file format is invalid
   at SheetAtlas.Core.Services.ExcelReaderService.LoadFileAsync(...)
   at SheetAtlas.Core.Services.LoadedFilesManager.LoadFilesAsync(...)
```

**Caratteristiche:**

- Rotazione giornaliera automatica (file per giorno)
- Solo Level >= Warning scritti (Info esclusi per performance)
- Rich formatting con timestamp, level, context, messaggio, stack trace completo
- Thread-safe con lock su file I/O
- Path cross-platform: `Environment.GetFolderPath(ApplicationData)`

**Implementazione:**

- `ILogService` + `LogService`
- Metodi: `AddLogMessage()`, `GetLogMessages()`, `GetLogFilePath()`
- In-memory list + persistence sincrona su file

**Accessibilità:**

- Menu: Help → View Error Log
- Command `ViewErrorLogCommand` in MainWindowViewModel
- Apre file con editor di sistema (default .log handler)

**Vantaggi:**

- ✅ Persistenza garantita anche in caso di crash
- ✅ Stack trace completo per debugging
- ✅ Facilmente condivisibile per bug report

---

## Componenti Core

### **ExcelError** (Value Object)

```csharp
public class ExcelError
{
    public LogSeverity Level { get; }           // Info, Warning, Error, Critical
    public string Message { get; }              // User-friendly message
    public string? Context { get; }             // Operation context (es. "LoadFile")
    public CellReference? Location { get; }     // Excel coordinate (Sheet1!B2)
    public Exception? InnerException { get; }   // Original exception
    public DateTime Timestamp { get; }
}
```

**Factory Methods:**

- `ExcelError.FileError(message, exception)` → Critical level
- `ExcelError.SheetError(message, sheetName)` → Error level
- `ExcelError.CellError(message, cellRef)` → Error level
- `ExcelError.Warning(message, context)` → Warning level
- `ExcelError.Info(message)` → Info level

**Integrazione:**

- ExcelFile contiene `List<ExcelError> Errors` property
- LoadStatus enum: Success, PartialSuccess (has warnings), Failed

---

### **IExceptionHandler** + **ExceptionHandler**

```csharp
public interface IExceptionHandler
{
    ExcelError Handle(Exception exception, string context);
    string GetUserMessage(Exception exception);
    bool IsRecoverable(Exception exception);
}
```

**Mapping Eccezioni → User Messages:**

| Eccezione | Messaggio User |
|-----------|----------------|
| `FileNotFoundException` | "File not found. Please verify the path." |
| `UnauthorizedAccessException` | "Access denied. Check file permissions." |
| `IOException` | "File is locked or in use by another program." |
| `OutOfMemoryException` | "File too large. Close other applications." |
| `ComparisonException` | Usa `UserMessage` property dell'eccezione |
| Altre | Exception.Message (fallback) |

**IsRecoverable Logic:**

- `true`: FileNotFound, UnauthorizedAccess, IOException → user può risolvere
- `false`: OutOfMemory, InvalidOperation → bug di programmazione

---

### **Custom Exceptions**

#### **SheetAtlasException** (Abstract Base)

```csharp
public abstract class SheetAtlasException : Exception
{
    public string UserMessage { get; }    // Messaggio per UI
    public string ErrorCode { get; }      // Codice errore (es. "FILE_001")
}
```

#### **ComparisonException** (Concrete)

```csharp
public class ComparisonException : SheetAtlasException
{
    public static ComparisonException IncompatibleStructures(string details);
    public static ComparisonException MissingSheet(string sheetName);
    public static ComparisonException NoCommonColumns(string sheet1, string sheet2);
}
```

**Vantaggi:**

- Distinzione tra messaggio tecnico (log) e user-friendly (UI)
- Factory methods per errori comuni con messaggi consistenti
- ErrorCode per telemetria/analytics futuri

---

## Flussi Operativi

### **Scenario A: Caricamento File con Warning**

1. User: File → Load Files → seleziona "data.xlsx"
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync(["data.xlsx"])`
3. Core: `IExcelReaderService.LoadFileAsync()` → ExcelFile
   - Parsing rileva 2 fogli con formato non standard
   - Crea 2 ExcelError con Level=Warning
   - LoadStatus = PartialSuccess
4. UI: `ProcessLoadedFileAsync()` riceve file
   - Switch su PartialSuccess → aggiunge file al collection
   - FileLoadResultViewModel espone `Errors` property (2 warning)
5. View: FileLoadResultView mostra:
   - Icona triangolo arancione ⚠️
   - Badge "(2)" in rosso
   - Expander "Errors and Warnings (2)" con lista
6. Log: ILogService scrive 2 warning in `app-2025-10-18.log`
7. User: può usare file normalmente, warning ignorabili

**Risultato:** File caricato, warning visibili ma non invasivi

---

### **Scenario B: Caricamento File Fallito (Critical)**

1. User: File → Load Files → seleziona "corrupt.xlsx"
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync(["corrupt.xlsx"])`
3. Core: `IExcelReaderService.LoadFileAsync()` → Exception
   - InvalidDataException: "File format is invalid"
   - Wrappata in ExcelFile con LoadStatus=Failed
   - ExcelError con Level=Critical
4. Manager: Catch generico nel try-catch
   - `FileLoadFailed` event raised
5. UI: Event handler mostra dialog
   - `IErrorNotificationService.ShowErrorAsync(excelError)`
   - Dialog modale: "⚠️ File Load Error" + messaggio
6. View: File aggiunto comunque al collection con:
   - Icona hexagon rosso 🔴
   - Badge "(1)"
   - Expander con critical error
   - Button "Try Again" visibile
7. Log: Critical error scritto in `app-2025-10-18.log` con stack trace

**Risultato:** User informato con dialog, file in lista con error visibile, retry disponibile

---

### **Scenario C: Caricamento Batch Multiplo (3 file, 2 falliti)**

1. User: File → Load Files → seleziona 3 file
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync([file1, file2, file3])`
3. Core: Loop su ogni file
   - file1.xlsx → Success
   - file2.xlsx → Failed (FileNotFoundException)
   - file3.xlsx → Failed (UnauthorizedAccessException)
4. Manager: Try-catch con **continue-on-error** logic
   - Non si ferma al primo errore
   - Accumula 2 eccezioni
   - Catch generico: `IErrorNotificationService.ShowExceptionAsync()`
5. UI: ErrorNotificationService raggruppa errori
   - `ShowErrorsAsync()` con lista di 2 ExcelError
   - Dialog modale con sezione per severity:

     ```text
     Failed to load 2 files:

     🔴 Critical Errors:
     • file2.xlsx: File not found
     • file3.xlsx: Access denied
     ```

6. View: 3 file in lista
   - file1: verde, nessun errore
   - file2: rosso, badge "(1)", expander con error
   - file3: rosso, badge "(1)", expander con error
7. Log: 2 errori scritti separatamente con context

**Risultato:** Operazione completata parzialmente, user informato con 1 dialog raggruppato

---

### **Scenario D: OutOfMemoryException (Special Case)**

1. User: carica file 500MB su sistema con poca RAM
2. `LoadFileCommand` → `LoadFilesAsync()`
3. Core: Durante parsing → OutOfMemoryException
4. Manager: Catch specifico per OutOfMemoryException

   ```csharp
   catch (OutOfMemoryException)
   {
       await _dialogService.ShowErrorAsync(
           "File too large. Close applications and retry.",
           "Memory Error"
       );
   }
   ```

5. UI: Dialog dedicato con suggerimento specifico
6. Log: Fatal error in log file
7. No file aggiunto al collection (operazione abortita)

**Risultato:** Messaggio chiaro con azione suggerita, no crash

---

## RelayCommand Global Safety Net

**Problema:** Qualsiasi exception non gestita in command handler crasha l'app.

**Soluzione:** RelayCommand con try-catch globale

```csharp
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Action<Exception>? _errorHandler;

    public void Execute(object? parameter)
    {
        try
        {
            _execute(parameter);
        }
        catch (OperationCanceledException)
        {
            // Silent - user cancellation is expected
            _logService.LogInfo("Operation cancelled by user");
        }
        catch (Exception ex)
        {
            _logService.LogError("Command execution failed", ex);

            // Custom error handler (opzionale)
            if (_errorHandler != null)
            {
                try
                {
                    _errorHandler(ex);
                }
                catch
                {
                    // Prevent error handler from crashing
                }
            }
        }
    }
}
```

**Caratteristiche:**

- ✅ Catch globale previene crash
- ✅ OperationCanceledException silent (UX pulita per cancellazioni)
- ✅ Log automatico di ogni errore
- ✅ Custom error handler opzionale (es. show dialog)
- ✅ Double-protection: error handler stesso è in try-catch

**Usage:**

```csharp
LoadFileCommand = new RelayCommand(
    execute: _ => LoadFiles(),
    errorHandler: ex => _errorNotificationService.ShowExceptionAsync(ex, "LoadFile")
);
```

---

## Cosa Funziona Bene

### **Architettura**

- ✅ Separation of concerns: Core non sa nulla di UI, usa ExcelError domain objects
- ✅ Dependency Injection: tutti i servizi iniettati via DI
- ✅ Testabile: ILogService, IExceptionHandler facilmente mockabili

### **User Experience**

- ✅ Non invasivo: errori visibili solo dove rilevanti (inline per file)
- ✅ Contestuale: chiaro quale file ha quale errore
- ✅ Actionable: "Try Again" permette recovery immediato
- ✅ Informativo: messaggi user-friendly con context
- ✅ Persistente: log file garantisce no data loss

### **Developer Experience**

- ✅ Consistente: tutti gli errori gestiti tramite stessi servizi
- ✅ Debuggable: stack trace completo in log file
- ✅ Safe: RelayCommand global catch previene crash
- ✅ Extensibile: facile aggiungere nuovi error types

---

## Note Implementative

### **Multi-Error Support**

Gli errori inline supportano già multi-errore per file tramite stack panel:

- `FileLoadResultViewModel.Errors` è `ObservableCollection<ExcelError>`
- ItemsControl in FileLoadResultView.axaml itera automaticamente
- Badge conteggio aggiornato via binding
- Expander gestisce visualizzazione collassabile

**Testing multi-error:**

- Al momento non testato in produzione (difficile generare più errori per singolo file)
- Architettura supporta N errori, solo manca test case reale

### **Performance**

- In-memory error list per file (max previsto: ~100 errori per file molto corrotto)
- Log file: async buffered I/O (no UI blocking)
- Dialog modali: raggruppamento evita spam UI

### **Accessibility**

- Keyboard navigation in Notification Center (Tab, Enter, Esc)
- High contrast theme support per dialog
- Focus management per dialog modali
- Screen reader: AutomationProperties su icone di stato

---

## Conclusioni

### **Punti di Forza**

1. **Robustezza**: RelayCommand safety net garantisce no crash
2. **Persistenza**: LogService garantisce tracciabilità completa
3. **UX Non Invasiva**: Inline errors + dialog = informazione contestuale senza distogliere dal lavoro
4. **Architettura Pulita**: Core/UI separation, DI, testabilità
5. **Recovery-Oriented**: Retry logic per operazioni fallite
6. **Professionalità**: Aspetto enterprise-grade senza essere fastidioso

### **Sistema Attuale**

Il sistema attuale è **funzionale** per le esigenze di SheetAtlas:

- Errori contestuali visibili inline accanto ai file
- Dialog per attenzione immediata quando necessario
- Log persistente per debugging e bug report
- Safety net globale che previene crash

**Cosa altro serve** - aggiungere [structured-file-logging-1](structured-file-logging-1.md) e integrarlo nella UI per avere un'esperienza di gestione dei file completa in ogni dettaglio.

---

**Ultima Modifica:** 2025-10-18
**Versione:** 1.1 (post-review, final)
