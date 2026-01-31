# Error Notification System - SheetAtlas

> This document describes the error handling and display system implemented in SheetAtlas.

## Philosophy

- **Fail Gracefully**: Errors don't crash the application, they are handled and shown to the user
- **Contextuality**: Errors shown inline next to the file that generated them
- **Non-Invasive**: User is not distracted from work, can ignore non-critical warnings
- **User-Friendly Messages**: Automatic conversion of technical exceptions → understandable messages
- **Guaranteed Persistence**: All errors >= Warning saved to daily log file
- **Recovery-First**: Retry capability for failed operations

---

## Architecture: 3 Levels

### **Level 1: Inline File Errors**

```drawing
┌────────────────────────────────────────────────┐
│  📄 report-2024.xlsx                    🔴 (3) │  ← Error count badge
│  ┌──────────────────────────────────────────┐  │
│  │ ⚠️ Errors and Warnings (3)               │  │
│  │ ──────────────────────────────────────── │  │
│  │ 🔴 Critical: Corrupted file format       │  │
│  │ ⚠️ Warning: Sheet 'Data' ignored         │  │
│  │ ⚠️ Warning: Cells B2:B10 mixed format    │  │
│  └──────────────────────────────────────────┘  │
│  [Try Again] [Remove from List]                │
└────────────────────────────────────────────────┘
```

**Features:**

- Each loaded file shows inline errors encountered
- Collapsible expander to hide/show details
- Status icons for LoadStatus:
  - 🔴 Red hexagon = Failed
  - ⚠️ Orange triangle = PartialSuccess (warning)
  - ✅ Green circle = Success
- Badge with error count (red background #FFEBEE)
- Colors by severity: Critical=#DC2626, Error=#EF4444, Warning=#F59E0B, Info=#3B82F6
- Contextual actions: "Try Again" for Failed, "Remove from List" for Success

**Implementation:**

- `FileLoadResultView.axaml` - View with error expander
- `FileLoadResultViewModel` - Exposes filtered `Errors` property (Critical/Error/Warning only)
- `LogSeverityToColorConverter` - Maps severity → colors
- Location-aware: If ExcelError has CellReference, shows Excel coordinates (e.g., "Sheet1!B2")

**Benefits:**

- ✅ Non-invasive: errors visible only for files with problems
- ✅ Contextual: errors next to the file that generated them
- ✅ Actionable: "Try Again" allows immediate recovery
- ✅ Scalable: Supports multi-error per file (expandable stack panel)

---

### **Level 2: Modal Dialogs**

```drawing
┌─────────────────────────────────────────┐
│          ⚠️ File Load Error             │
│                                         │
│  Failed to load 3 files:                │
│                                         │
│  🔴 Critical Errors:                    │
│  • report.xlsx: File corrupted          │
│  • data.xlsx: Access denied             │
│                                         │
│  ⚠️ Warnings:                           │
│  • sales.xlsx: 2 sheets ignored         │
│                                         │
│             [OK]                        │
└─────────────────────────────────────────┘
```

**Features:**

- Shown for errors requiring immediate attention
- Automatic grouping by severity (Critical/Error/Warning)
- Multi-error formatting with emoji for categories
- Modal dialog blocks interaction until dismissed
- Light/dark theme support with DynamicResource

**Implementation:**

- `IErrorNotificationService` + `ErrorNotificationService`
  - `ShowExceptionAsync(Exception, string context)` - Converts exception via IExceptionHandler
  - `ShowErrorAsync(ExcelError)` - Shows single error
  - `ShowErrorsAsync(IEnumerable<ExcelError>, string title)` - Groups and shows
- `IDialogService` + `AvaloniaDialogService`
  - `ShowErrorAsync()`, `ShowWarningAsync()`, `ShowInformationAsync()`, `ShowConfirmationAsync()`
  - Creates Window programmatically (no MessageBox framework)

**Use Cases:**

- Multiple file load failed (batch operation)
- Critical errors from OutOfMemoryException
- Operations that fail without specific file

**Benefits:**

- ✅ Guaranteed attention: user must dismiss to continue
- ✅ Smart grouping: avoids spam of N dialogs for N errors

---

### **Level 3: Persistent File Log**

```text
📁 %LocalApplicationData%/SheetAtlas/Logs/
  ├── app-2025-10-18.log
  ├── app-2025-10-17.log
  └── app-2025-10-16.log
```

**File Format:**

```text
[2025-10-18 14:23:15] [ERROR] File Load Failed
Context: LoadFileCommand
Message: File 'report.xlsx' could not be loaded: file is corrupted
Exception: System.IO.InvalidDataException: The file format is invalid
   at SheetAtlas.Core.Services.ExcelReaderService.LoadFileAsync(...)
   at SheetAtlas.Core.Services.LoadedFilesManager.LoadFilesAsync(...)
```

**Features:**

- Automatic daily rotation (file per day)
- Only Level >= Warning written (Info excluded for performance)
- Rich formatting with timestamp, level, context, message, full stack trace
- Thread-safe with lock on file I/O
- Cross-platform path: `Environment.GetFolderPath(ApplicationData)`

**Implementation:**

- `ILogService` + `LogService`
- Methods: `AddLogMessage()`, `GetLogMessages()`, `GetLogFilePath()`
- In-memory list + synchronous file persistence

**Accessibility:**

- Menu: Help → View Error Log
- Command `ViewErrorLogCommand` in MainWindowViewModel
- Opens file with system editor (default .log handler)

**Benefits:**

- ✅ Guaranteed persistence even in case of crash
- ✅ Complete stack trace for debugging
- ✅ Easily shareable for bug reports

---

## Core Components

### **ExcelError** (Value Object)

```csharp
public class ExcelError
{
    public LogSeverity Level { get; }           // Info, Warning, Error, Critical
    public string Message { get; }              // User-friendly message
    public string? Context { get; }             // Operation context (e.g., "LoadFile")
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

**Integration:**

- ExcelFile contains `List<ExcelError> Errors` property
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

**Exception → User Message Mapping:**

| Exception | User Message |
|-----------|--------------|
| `FileNotFoundException` | "File not found. Please verify the path." |
| `UnauthorizedAccessException` | "Access denied. Check file permissions." |
| `IOException` | "File is locked or in use by another program." |
| `OutOfMemoryException` | "File too large. Close other applications." |
| `ComparisonException` | Uses exception's `UserMessage` property |
| Others | Exception.Message (fallback) |

**IsRecoverable Logic:**

- `true`: FileNotFound, UnauthorizedAccess, IOException → user can fix
- `false`: OutOfMemory, InvalidOperation → programming bug

---

### **Custom Exceptions**

#### **SheetAtlasException** (Abstract Base)

```csharp
public abstract class SheetAtlasException : Exception
{
    public string UserMessage { get; }    // Message for UI
    public string ErrorCode { get; }      // Error code (e.g., "FILE_001")
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

**Benefits:**

- Distinction between technical message (log) and user-friendly (UI)
- Factory methods for common errors with consistent messages
- ErrorCode for future telemetry/analytics

---

## Operational Flows

### **Scenario A: File Load with Warning**

1. User: File → Load Files → selects "data.xlsx"
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync(["data.xlsx"])`
3. Core: `IExcelReaderService.LoadFileAsync()` → ExcelFile
   - Parsing detects 2 sheets with non-standard format
   - Creates 2 ExcelError with Level=Warning
   - LoadStatus = PartialSuccess
4. UI: `ProcessLoadedFileAsync()` receives file
   - Switch on PartialSuccess → adds file to collection
   - FileLoadResultViewModel exposes `Errors` property (2 warnings)
5. View: FileLoadResultView shows:
   - Orange triangle icon ⚠️
   - Red badge "(2)"
   - Expander "Errors and Warnings (2)" with list
6. Log: ILogService writes 2 warnings to `app-2025-10-18.log`
7. User: can use file normally, warnings are ignorable

**Result:** File loaded, warnings visible but not invasive

---

### **Scenario B: Failed File Load (Critical)**

1. User: File → Load Files → selects "corrupt.xlsx"
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync(["corrupt.xlsx"])`
3. Core: `IExcelReaderService.LoadFileAsync()` → Exception
   - InvalidDataException: "File format is invalid"
   - Wrapped in ExcelFile with LoadStatus=Failed
   - ExcelError with Level=Critical
4. Manager: Generic catch in try-catch
   - `FileLoadFailed` event raised
5. UI: Event handler shows dialog
   - `IErrorNotificationService.ShowErrorAsync(excelError)`
   - Modal dialog: "⚠️ File Load Error" + message
6. View: File added to collection with:
   - Red hexagon icon 🔴
   - Badge "(1)"
   - Expander with critical error
   - "Try Again" button visible
7. Log: Critical error written to `app-2025-10-18.log` with stack trace

**Result:** User informed with dialog, file in list with visible error, retry available

---

### **Scenario C: Batch Multiple Load (3 files, 2 failed)**

1. User: File → Load Files → selects 3 files
2. `LoadFileCommand` → `ILoadedFilesManager.LoadFilesAsync([file1, file2, file3])`
3. Core: Loop on each file
   - file1.xlsx → Success
   - file2.xlsx → Failed (FileNotFoundException)
   - file3.xlsx → Failed (UnauthorizedAccessException)
4. Manager: Try-catch with **continue-on-error** logic
   - Doesn't stop at first error
   - Accumulates 2 exceptions
   - Generic catch: `IErrorNotificationService.ShowExceptionAsync()`
5. UI: ErrorNotificationService groups errors
   - `ShowErrorsAsync()` with list of 2 ExcelError
   - Modal dialog with section by severity:

     ```text
     Failed to load 2 files:

     🔴 Critical Errors:
     • file2.xlsx: File not found
     • file3.xlsx: Access denied
     ```

6. View: 3 files in list
   - file1: green, no errors
   - file2: red, badge "(1)", expander with error
   - file3: red, badge "(1)", expander with error
7. Log: 2 errors written separately with context

**Result:** Operation partially completed, user informed with 1 grouped dialog

---

### **Scenario D: OutOfMemoryException (Special Case)**

1. User: loads 500MB file on system with low RAM
2. `LoadFileCommand` → `LoadFilesAsync()`
3. Core: During parsing → OutOfMemoryException
4. Manager: Specific catch for OutOfMemoryException

   ```csharp
   catch (OutOfMemoryException)
   {
       await _dialogService.ShowErrorAsync(
           "File too large. Close applications and retry.",
           "Memory Error"
       );
   }
   ```

5. UI: Dedicated dialog with specific suggestion
6. Log: Fatal error in log file
7. No file added to collection (operation aborted)

**Result:** Clear message with suggested action, no crash

---

## RelayCommand Global Safety Net

**Problem:** Any unhandled exception in command handler crashes the app.

**Solution:** RelayCommand with global try-catch

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

            // Custom error handler (optional)
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

**Features:**

- ✅ Global catch prevents crash
- ✅ OperationCanceledException silent (clean UX for cancellations)
- ✅ Automatic logging of every error
- ✅ Optional custom error handler (e.g., show dialog)
- ✅ Double-protection: error handler itself is in try-catch

**Usage:**

```csharp
LoadFileCommand = new RelayCommand(
    execute: _ => LoadFiles(),
    errorHandler: ex => _errorNotificationService.ShowExceptionAsync(ex, "LoadFile")
);
```

---

## What Works Well

### **Architecture**

- ✅ Separation of concerns: Core knows nothing about UI, uses ExcelError domain objects
- ✅ Dependency Injection: all services injected via DI
- ✅ Testable: ILogService, IExceptionHandler easily mockable

### **User Experience**

- ✅ Non-invasive: errors visible only where relevant (inline for files)
- ✅ Contextual: clear which file has which error
- ✅ Actionable: "Try Again" allows immediate recovery
- ✅ Informative: user-friendly messages with context
- ✅ Persistent: log file guarantees no data loss

### **Developer Experience**

- ✅ Consistent: all errors handled through same services
- ✅ Debuggable: complete stack trace in log file
- ✅ Safe: RelayCommand global catch prevents crash
- ✅ Extensible: easy to add new error types

---

## Implementation Notes

### **Multi-Error Support**

Inline errors already support multi-error per file via stack panel:

- `FileLoadResultViewModel.Errors` is `ObservableCollection<ExcelError>`
- ItemsControl in FileLoadResultView.axaml iterates automatically
- Badge count updated via binding
- Expander handles collapsible display

**Testing multi-error:**

- Currently not tested in production (difficult to generate multiple errors for single file)
- Architecture supports N errors, only missing real test case

### **Performance**

- In-memory error list per file (max expected: ~100 errors for very corrupted file)
- Log file: async buffered I/O (no UI blocking)
- Modal dialogs: grouping avoids UI spam

### **Accessibility**

- Keyboard navigation in Notification Center (Tab, Enter, Esc)
- High contrast theme support for dialogs
- Focus management for modal dialogs
- Screen reader: AutomationProperties on status icons

---

## Conclusions

### **Strengths**

1. **Robustness**: RelayCommand safety net guarantees no crash
2. **Persistence**: LogService guarantees complete traceability
3. **Non-Invasive UX**: Inline errors + dialogs = contextual information without distracting from work
4. **Clean Architecture**: Core/UI separation, DI, testability
5. **Recovery-Oriented**: Retry logic for failed operations
6. **Professionalism**: Enterprise-grade appearance without being annoying

### **Current System**

The current system is **functional** for SheetAtlas needs:

- Contextual errors visible inline next to files
- Dialogs for immediate attention when necessary
- Persistent log for debugging and bug reports
- Global safety net preventing crashes

**What else is needed** - add [structured-file-logging](structured-file-logging.md) and integrate it into the UI for a complete file management experience in every detail.

---

**Last Modified:** 2025-10-18
**Version:** 1.1 (post-review, final)
