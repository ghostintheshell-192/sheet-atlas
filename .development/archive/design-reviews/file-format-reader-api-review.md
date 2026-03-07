# API Design Review: IFileFormatReader Interface
**Date**: 2025-10-07
**Reviewer**: Claude Code (API Design Agent)
**Project**: SheetAtlas - Multi-Format File Reader Architecture
**Status**: APPROVED WITH MODIFICATIONS

---

## Executive Summary

**Overall Design Rating: 7.5/10**

The proposed `IFileFormatReader` interface with Strategy Pattern shows solid architectural thinking and aligns well with Clean Architecture principles. However, there are **critical gaps** in extensibility, error handling consistency, and resource management that must be addressed before implementation.

**Recommendation**: Proceed with implementation after incorporating the modifications outlined in this review. The Strategy Pattern is the correct choice, but the interface requires refinement to match the project's existing Result Pattern philosophy and extensibility requirements.

---

## 1. Interface Design Critique

### Current Proposed Interface

```csharp
namespace SheetAtlas.Core.Application.Interfaces
{
    public interface IFileFormatReader
    {
        IReadOnlyList<string> SupportedExtensions { get; }
        bool CanRead(string filePath);
        Task<ExcelFile> ReadAsync(string filePath);
    }
}
```

### Strengths ✅

1. **Clean separation of concerns**: Each reader handles one format family
2. **Discoverable**: `SupportedExtensions` makes capabilities explicit
3. **Async-first**: `ReadAsync` follows modern .NET patterns
4. **Minimal surface area**: Interface is small and focused (ISP compliant)
5. **Correct layer placement**: Interface in Core, implementations in Infrastructure

### Critical Issues ❌

#### 1.1 Missing CancellationToken Support

**Problem**: Long-running file operations cannot be cancelled by users.

```csharp
// Current - no cancellation support
Task<ExcelFile> ReadAsync(string filePath);

// Required for production
Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default);
```

**Impact**: Violates project requirement "Responsiveness as requirement: A blocked interface makes the application non-functional" (from `/data/repos/CLAUDE.md`).

**Severity**: CRITICAL - Must fix before implementation.

---

#### 1.2 Inconsistent Error Handling Pattern

**Problem**: `CanRead(string filePath)` creates ambiguity about what it validates.

**Current Issues**:
```csharp
// What does CanRead check?
bool CanRead(string filePath);

// Option A: Extension only (fast)
if (filePath.EndsWith(".xlsx")) return true;

// Option B: File existence (I/O operation)
if (!File.Exists(filePath)) return false;

// Option C: Content inspection (slow, opens file)
using var stream = File.OpenRead(filePath);
return IsValidXlsxMagicNumber(stream);
```

**Analysis**: Each implementation could interpret this differently, leading to:
- Inconsistent behavior across readers
- Performance unpredictability (some fast, some slow)
- Error handling confusion (does it throw or return false?)

**Project Philosophy Violation**: "Fail Fast vs. Silent Failure - Avoid masking errors" (from `general-principles.md`).

---

#### 1.3 Stream-Based Reading Missing

**Problem**: No support for reading from streams (required for large files, network sources, testing).

**Use Cases**:
- Large file streaming to reduce memory footprint
- Reading from network streams (future cloud integration)
- Unit testing with in-memory streams (no file I/O)
- Reading from compressed archives without extraction

**Missing Method**:
```csharp
Task<ExcelFile> ReadAsync(Stream stream, string fileHint, CancellationToken cancellationToken = default);
```

**Impact**: Limits testability and future extensibility.

---

#### 1.4 Configuration/Options Missing

**Problem**: CSV readers need delimiter configuration, but no mechanism exists.

**Examples of Missing Configurability**:
- CSV delimiter (`,` vs `;` vs `\t`)
- Encoding detection (UTF-8, Windows-1252, etc.)
- Culture-specific number formats
- Date format parsing rules
- Empty row handling strategies

**Current Workaround**: Would require creating separate implementations for each variant (`CsvCommaReader`, `CsvSemicolonReader`) - violates DRY principle.

---

### 1.5 Resource Management Unclear

**Problem**: Interface doesn't indicate if implementations should be stateless or can hold state.

**Questions**:
```csharp
// Should readers be registered as Singleton, Scoped, or Transient?
services.AddSingleton<IFileFormatReader, OpenXmlFileReader>();

// Can readers cache SharedStringTable between reads?
private SharedStringTable? _cachedStringTable; // Allowed?

// Do readers implement IDisposable?
public class XlsFileReader : IFileFormatReader, IDisposable
```

**Required Clarification**: Document lifecycle expectations in XML comments.

---

## 2. SOLID Principles Validation

### Single Responsibility Principle ✅ PASS

Each reader has one clear responsibility: read a specific format family and convert to `ExcelFile`.

**Evidence**:
- `OpenXmlFileReader`: .xlsx family (OpenXML formats)
- `XlsFileReader`: .xls family (BIFF formats)
- `CsvFileReader`: .csv (plain text)

**Verdict**: Well-defined boundaries, no mixing of concerns.

---

### Open/Closed Principle ✅ PASS

New formats can be added without modifying existing code.

**Example**:
```csharp
// Add ODS support without touching existing readers
public class OdsFileReader : IFileFormatReader
{
    public IReadOnlyList<string> SupportedExtensions =>
        new[] { ".ods", ".fods" }.AsReadOnly();

    // Implementation...
}

// Registration in DI container
services.AddSingleton<IFileFormatReader, OdsFileReader>();
```

**Verdict**: Extensible design, follows OCP correctly.

---

### Liskov Substitution Principle ⚠️ CONDITIONAL PASS

Implementations are substitutable IF interface is clarified.

**Current Risk**:
```csharp
// If CanRead implementations behave differently:
var reader1 = new OpenXmlFileReader();
reader1.CanRead("file.xlsx"); // Returns true instantly (extension check)

var reader2 = new CsvFileReader();
reader2.CanRead("file.csv"); // Opens file, reads first line (slow)

// Violates LSP: Callers can't treat readers uniformly
```

**Required Fix**: Specify `CanRead` contract explicitly in XML docs.

**Verdict**: PASS after clarification.

---

### Interface Segregation Principle ✅ PASS

Interface is minimal (3 members) and cohesive.

**Analysis**:
- No "fat interface" - all members essential
- Clients don't depend on unused methods
- Single purpose: format reading capability

**Verdict**: Excellent ISP compliance.

---

### Dependency Inversion Principle ✅ PASS

High-level code depends on abstraction, not concrete readers.

**Evidence**:
```csharp
// ExcelReaderService depends on IFileFormatReader (abstraction)
public class ExcelReaderService : IExcelReaderService
{
    private readonly IEnumerable<IFileFormatReader> _readers;

    public ExcelReaderService(IEnumerable<IFileFormatReader> readers)
    {
        _readers = readers; // Dependency injection
    }
}
```

**Verdict**: Perfect DIP application, abstractions at correct layer (Core).

---

## 3. Extensibility Concerns & Solutions

### 3.1 Large File Streaming

**Requirement**: Handle 100MB+ Excel files without excessive memory usage.

**Current Gap**: No streaming API in interface.

**Recommended Addition**:
```csharp
public interface IFileFormatReader
{
    // Existing members...

    /// <summary>
    /// Determines if this reader supports streaming for large files
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Reads file from stream (for large files and testing)
    /// </summary>
    /// <param name="stream">Input stream (must support seeking for some formats)</param>
    /// <param name="fileHint">Original filename for extension detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ExcelFile> ReadAsync(
        Stream stream,
        string fileHint,
        CancellationToken cancellationToken = default);
}
```

**Implementation Strategy**:
```csharp
public class OpenXmlFileReader : IFileFormatReader
{
    public bool SupportsStreaming => true; // OpenXML supports streams

    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        return await ReadAsync(stream, filePath, ct);
    }

    public async Task<ExcelFile> ReadAsync(Stream stream, string fileHint, CancellationToken ct)
    {
        // Actual implementation uses stream
        using var doc = SpreadsheetDocument.Open(stream, false);
        // ...
    }
}
```

---

### 3.2 Reader Configuration

**Problem**: CSV format requires runtime configuration.

**Solution 1: Configuration Object Pattern** (RECOMMENDED)

```csharp
// New interface for configurable readers
public interface IConfigurableFileReader : IFileFormatReader
{
    /// <summary>
    /// Configure reader with format-specific options
    /// </summary>
    void Configure(IReaderOptions options);
}

// Options abstraction
public interface IReaderOptions { }

public class CsvReaderOptions : IReaderOptions
{
    public char Delimiter { get; set; } = ',';
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool HasHeaderRow { get; set; } = true;
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
}

// Implementation
public class CsvFileReader : IConfigurableFileReader
{
    private CsvReaderOptions _options = new();

    public void Configure(IReaderOptions options)
    {
        if (options is CsvReaderOptions csvOptions)
            _options = csvOptions;
    }

    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        // Use _options.Delimiter, etc.
    }
}
```

**Solution 2: Factory Pattern** (Alternative)

```csharp
public interface IFileReaderFactory
{
    IFileFormatReader CreateReader(string extension, IReaderOptions? options = null);
}

// Usage
var csvReader = factory.CreateReader(".csv", new CsvReaderOptions
{
    Delimiter = ';'
});
```

**Recommendation**: Use Solution 1 (interface segregation) - readers without configuration don't implement `IConfigurableFileReader`.

---

### 3.3 Stateless vs Stateful Readers

**Decision**: Readers SHOULD be stateless services.

**Rationale**:
- Thread-safety: Singleton registration requires stateless design
- Testability: Easier to test pure functions
- Performance: No synchronization overhead
- Simplicity: Configuration passed per-call, not stored

**Recommended Pattern**:
```csharp
// Stateless - configuration per call
public interface IFileFormatReader
{
    Task<ExcelFile> ReadAsync(
        string filePath,
        IReaderOptions? options = null,
        CancellationToken cancellationToken = default);
}

// DI registration
services.AddSingleton<IFileFormatReader, OpenXmlFileReader>();
services.AddSingleton<IFileFormatReader, CsvFileReader>();
```

**Exception**: Readers CAN cache immutable data (e.g., format detection regex).

---

### 3.4 Dependency Management

**Problem**: Different readers need different dependencies.

**Example**:
```csharp
// OpenXmlFileReader depends on OpenXML helpers
public class OpenXmlFileReader : IFileFormatReader
{
    private readonly ICellReferenceParser _cellParser;
    private readonly IMergedCellProcessor _mergedCellProcessor;
    private readonly ICellValueReader _cellValueReader;

    public OpenXmlFileReader(
        ICellReferenceParser cellParser,
        IMergedCellProcessor mergedCellProcessor,
        ICellValueReader cellValueReader)
    {
        // Dependencies specific to OpenXML format
    }
}

// XlsFileReader has different dependencies
public class XlsFileReader : IFileFormatReader
{
    private readonly ILogger<XlsFileReader> _logger;

    public XlsFileReader(ILogger<XlsFileReader> logger)
    {
        // ExcelDataReader library handles most work
    }
}
```

**Solution**: This is fine! DI container resolves dependencies automatically.

**DI Registration**:
```csharp
// Register reader-specific dependencies
services.AddSingleton<ICellReferenceParser, CellReferenceParser>();
services.AddSingleton<IMergedCellProcessor, MergedCellProcessor>();
services.AddSingleton<ICellValueReader, CellValueReader>();

// Register readers (dependencies auto-resolved)
services.AddSingleton<IFileFormatReader, OpenXmlFileReader>();
services.AddSingleton<IFileFormatReader, XlsFileReader>();
services.AddSingleton<IFileFormatReader, CsvFileReader>();
```

**Verdict**: No changes needed - current DI architecture handles this correctly.

---

## 4. Error Handling Strategy

### Current Project Pattern: Result Objects (Not Exceptions)

**Project Philosophy** (from CLAUDE.md):
> "Fail Fast for bugs, Never Throw for business errors"
> - Core/Domain: Never Throw → Return Result objects
> - Infrastructure: Fail Fast → Validate preconditions only

**Current `ExcelFile` Entity**:
```csharp
public class ExcelFile
{
    public LoadStatus Status { get; } // Success, PartialSuccess, Failed
    public IReadOnlyList<ExcelError> Errors { get; }
}
```

### Recommended Error Handling Contract

#### ✅ DO: Return ExcelFile with Failed Status

```csharp
public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
{
    var errors = new List<ExcelError>();
    var sheets = new Dictionary<string, DataTable>();

    // Business errors → Result object
    try
    {
        // File reading logic...
    }
    catch (FileNotFoundException ex)
    {
        errors.Add(ExcelError.Critical("File", "File not found", ex));
        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
    }
    catch (IOException ex)
    {
        errors.Add(ExcelError.Critical("File", "Cannot access file", ex));
        return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
    }

    return new ExcelFile(filePath, LoadStatus.Success, sheets, errors);
}
```

#### ❌ DON'T: Throw for Business Errors

```csharp
// WRONG - business errors should not throw
if (!File.Exists(filePath))
    throw new FileNotFoundException("File not found");
```

#### ✅ DO: Throw for Programming Bugs

```csharp
public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
{
    // Precondition validation - this is a programming bug
    if (string.IsNullOrWhiteSpace(filePath))
        throw new ArgumentNullException(nameof(filePath));

    // Business logic - returns Result object
    // ...
}
```

### Interface Contract Documentation

**Required XML Documentation**:
```csharp
/// <summary>
/// Reads an Excel file from the specified path and converts to domain entity
/// </summary>
/// <param name="filePath">Absolute path to the file (must not be null/empty)</param>
/// <param name="cancellationToken">Cancellation token for long operations</param>
/// <returns>
/// ExcelFile with Status indicating outcome:
/// - Success: File read completely
/// - PartialSuccess: Some sheets failed, others succeeded
/// - Failed: File could not be read
/// Check Errors property for details of any failures.
/// </returns>
/// <exception cref="ArgumentNullException">If filePath is null or empty (programming bug)</exception>
/// <exception cref="OperationCanceledException">If cancellation requested</exception>
/// <remarks>
/// This method NEVER throws for business errors (file not found, corrupted, etc).
/// Business errors are returned in the ExcelFile.Errors collection.
/// </remarks>
Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default);
```

---

### CanRead Method Specification

**Problem**: Current signature is ambiguous.

**Recommended Approach**: Make it FAST and DETERMINISTIC.

```csharp
/// <summary>
/// Determines if this reader can handle files with the given extension
/// </summary>
/// <param name="filePath">File path (only extension is checked)</param>
/// <returns>True if file extension is supported, false otherwise</returns>
/// <remarks>
/// This method performs ONLY extension checking (fast, no I/O).
/// It does NOT validate:
/// - File existence
/// - File accessibility
/// - File content validity
/// Use ReadAsync to get actual read results with detailed errors.
/// </remarks>
bool CanRead(string filePath);
```

**Implementation Example**:
```csharp
public bool CanRead(string filePath)
{
    var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
    return SupportedExtensions.Contains(extension);
}
```

**Rationale**:
- Fast: No I/O operations
- Predictable: Same result every time for same extension
- Consistent: All implementations behave identically
- Fail-fast: Actual errors surface in `ReadAsync`

---

## 5. Alternative Designs Considered

### Alternative 1: Abstract Base Class

```csharp
public abstract class FileFormatReaderBase
{
    public abstract IReadOnlyList<string> SupportedExtensions { get; }

    public virtual bool CanRead(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public abstract Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct);
}
```

**Pros**:
- Default `CanRead` implementation (DRY)
- Shared utility methods possible
- Clearer template for implementers

**Cons**:
- Forces single inheritance (limits composition)
- Couples implementations to base class
- Harder to mock in tests
- Violates "prefer composition over inheritance"

**Verdict**: ❌ Interface is better for this use case.

---

### Alternative 2: Factory Pattern Only

```csharp
public interface IFileReaderFactory
{
    IFileFormatReader? GetReader(string filePath);
    IEnumerable<string> GetSupportedExtensions();
}

// No IFileFormatReader interface - factory returns concrete types
```

**Pros**:
- Single entry point
- Simpler dependency graph
- Easier to add metadata (priority, features)

**Cons**:
- Violates Open/Closed: Factory must know all readers
- Harder to test individual readers
- Loses type safety (returns base type or dynamic)
- Cannot inject specific reader implementations

**Verdict**: ❌ Strategy Pattern is superior for this architecture.

---

### Alternative 3: Metadata-Driven Registration

```csharp
// Readers register with metadata instead of implementing interface
[FileFormatReader(Extensions = new[] { ".xlsx", ".xlsm" })]
public class OpenXmlFileReader
{
    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        // Implementation
    }
}

// Discovery via reflection
var readers = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => t.GetCustomAttribute<FileFormatReaderAttribute>() != null);
```

**Pros**:
- Declarative registration
- Easy to add metadata (priority, features, etc.)
- No explicit DI registration needed

**Cons**:
- Reflection overhead
- Harder to debug
- Implicit dependencies (magic behavior)
- Breaks compile-time safety
- Not compatible with .NET trimming/AOT

**Verdict**: ❌ Too complex for current needs, violates "Effective simplicity" principle.

---

### Recommended Approach: Enhanced Strategy Pattern

**Use Strategy Pattern with these enhancements**:

1. ✅ Keep `IFileFormatReader` interface (current design)
2. ✅ Add `CancellationToken` parameter to `ReadAsync`
3. ✅ Add optional stream-based `ReadAsync` overload
4. ✅ Add `IConfigurableFileReader` for CSV/configurable formats
5. ✅ Document error handling contract in XML comments
6. ✅ Clarify `CanRead` as extension-only check

**Why Strategy is Correct**:
- Aligns with existing architecture (see `IRowComparisonService`)
- Clean dependency injection (current DI container setup)
- Easy to test (mock individual readers)
- Open/Closed compliant (add readers without changing orchestrator)
- Follows project's "Effective simplicity" principle

---

## 6. Recommended Interface (Final Version)

```csharp
namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Represents a reader capable of loading specific Excel file formats
    /// </summary>
    /// <remarks>
    /// Implementations must be STATELESS and thread-safe for Singleton registration.
    /// All file reading errors are returned as ExcelFile.Errors, not thrown.
    /// Only programming bugs (null parameters) should throw exceptions.
    /// </remarks>
    public interface IFileFormatReader
    {
        /// <summary>
        /// File extensions supported by this reader (e.g., [".xlsx", ".xlsm"])
        /// </summary>
        /// <remarks>
        /// Extensions should be lowercase with leading dot.
        /// This list is used for fast format detection before reading.
        /// </remarks>
        IReadOnlyList<string> SupportedExtensions { get; }

        /// <summary>
        /// Determines if this reader can handle files with the given extension
        /// </summary>
        /// <param name="filePath">File path (only extension is checked, not content)</param>
        /// <returns>True if file extension is in SupportedExtensions, false otherwise</returns>
        /// <remarks>
        /// This method performs ONLY extension checking (fast, no I/O operations).
        /// It does NOT validate file existence, accessibility, or content.
        /// Use ReadAsync to get actual read results with detailed error reporting.
        /// </remarks>
        bool CanRead(string filePath);

        /// <summary>
        /// Reads an Excel file from the specified path and converts to domain entity
        /// </summary>
        /// <param name="filePath">Absolute path to the file (must not be null/empty)</param>
        /// <param name="cancellationToken">Cancellation token for long-running operations</param>
        /// <returns>
        /// ExcelFile with Status indicating outcome:
        /// - Success: File read completely without errors
        /// - PartialSuccess: Some sheets failed but others succeeded
        /// - Failed: File could not be read at all
        /// Check Errors property for details of any failures.
        /// </returns>
        /// <exception cref="ArgumentNullException">If filePath is null or whitespace</exception>
        /// <exception cref="OperationCanceledException">If cancellation was requested</exception>
        /// <remarks>
        /// This method follows the Result Pattern - business errors (file not found,
        /// corrupted data, unsupported format) are returned in ExcelFile.Errors.
        /// Only programming bugs throw exceptions.
        /// </remarks>
        Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extended interface for readers that support stream-based reading
    /// </summary>
    /// <remarks>
    /// Implement this interface if your reader can efficiently process streams
    /// (useful for large files, network sources, and testing scenarios).
    /// </remarks>
    public interface IStreamableFileReader : IFileFormatReader
    {
        /// <summary>
        /// Reads an Excel file from a stream
        /// </summary>
        /// <param name="stream">Input stream (position will be reset if seekable)</param>
        /// <param name="fileHint">Original filename for extension/format detection</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>ExcelFile with read results (see ReadAsync documentation)</returns>
        /// <exception cref="ArgumentNullException">If stream or fileHint is null</exception>
        /// <exception cref="NotSupportedException">If stream is not seekable and format requires seeking</exception>
        Task<ExcelFile> ReadAsync(Stream stream, string fileHint, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extended interface for readers that require runtime configuration
    /// </summary>
    /// <remarks>
    /// Use this for formats like CSV that have variable delimiters, encodings, etc.
    /// Readers should have sensible defaults and be usable without configuration.
    /// </remarks>
    public interface IConfigurableFileReader : IFileFormatReader
    {
        /// <summary>
        /// Applies configuration options to this reader
        /// </summary>
        /// <param name="options">Format-specific options (e.g., CsvReaderOptions)</param>
        /// <exception cref="ArgumentNullException">If options is null</exception>
        /// <exception cref="ArgumentException">If options type is not supported</exception>
        void Configure(IReaderOptions options);
    }

    /// <summary>
    /// Base interface for reader configuration options
    /// </summary>
    public interface IReaderOptions { }
}
```

---

## 7. Orchestrator Refactoring Recommendations

### Current Proposed Implementation

```csharp
public class ExcelReaderService : IExcelReaderService
{
    private readonly IEnumerable<IFileFormatReader> _readers;

    public async Task<ExcelFile> LoadFileAsync(string filePath)
    {
        var reader = _readers.FirstOrDefault(r => r.CanRead(filePath));

        if (reader == null)
            return ExcelFile.CreateFailed(filePath, new ExcelError("Unsupported format"));

        return await reader.ReadAsync(filePath);
    }
}
```

### Issues with Current Design

1. **Missing CreateFailed Factory**: `ExcelFile` doesn't have a `CreateFailed` method (based on code review)
2. **No cancellation support**: Doesn't accept `CancellationToken`
3. **No reader priority**: What if multiple readers match?
4. **No logging**: Lost diagnostic information from current implementation

---

### Recommended Orchestrator Implementation

```csharp
namespace SheetAtlas.Infrastructure.External
{
    public class ExcelReaderService : IExcelReaderService
    {
        private readonly IEnumerable<IFileFormatReader> _readers;
        private readonly ILogger<ExcelReaderService> _logger;

        public ExcelReaderService(
            IEnumerable<IFileFormatReader> readers,
            ILogger<ExcelReaderService> logger)
        {
            _readers = readers ?? throw new ArgumentNullException(nameof(readers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExcelFile> LoadFileAsync(string filePath)
        {
            return await LoadFileAsync(filePath, CancellationToken.None);
        }

        public async Task<ExcelFile> LoadFileAsync(string filePath, CancellationToken cancellationToken)
        {
            // Precondition validation (programming bug)
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            _logger.LogInformation("Loading file {FilePath} with extension {Extension}",
                filePath, extension);

            // Find compatible reader
            var reader = _readers.FirstOrDefault(r => r.CanRead(filePath));

            if (reader == null)
            {
                _logger.LogWarning("No reader found for extension {Extension}", extension);

                var errors = new List<ExcelError>
                {
                    ExcelError.Critical("File",
                        $"Unsupported file format: {extension}. " +
                        $"Supported formats: {GetSupportedFormatsString()}")
                };

                return new ExcelFile(filePath, LoadStatus.Failed,
                    new Dictionary<string, DataTable>(), errors);
            }

            _logger.LogDebug("Using {ReaderType} for {Extension}",
                reader.GetType().Name, extension);

            // Delegate to format-specific reader
            return await reader.ReadAsync(filePath, cancellationToken);
        }

        public async Task<List<ExcelFile>> LoadFilesAsync(IEnumerable<string> filePaths)
        {
            var results = new List<ExcelFile>();

            foreach (var filePath in filePaths)
            {
                var file = await LoadFileAsync(filePath);
                results.Add(file);
            }

            return results;
        }

        private string GetSupportedFormatsString()
        {
            var extensions = _readers
                .SelectMany(r => r.SupportedExtensions)
                .Distinct()
                .OrderBy(e => e);

            return string.Join(", ", extensions);
        }
    }
}
```

### Key Improvements

1. ✅ **Maintains existing interface**: `IExcelReaderService` unchanged (backward compatible)
2. ✅ **Adds cancellation support**: New overload with `CancellationToken`
3. ✅ **Proper error messages**: Lists supported formats in error
4. ✅ **Logging preserved**: Diagnostic information maintained
5. ✅ **Fail-fast validation**: Null check throws immediately
6. ✅ **Result pattern**: Returns `ExcelFile` with `Failed` status, never throws for business errors

---

## 8. Implementation Guidance

### Phase 1: Interface Definition (Week 1)

**Tasks**:
1. Create `IFileFormatReader` interface in `SheetAtlas.Core/Application/Interfaces/`
2. Add XML documentation with error handling contract
3. Create `IStreamableFileReader` and `IConfigurableFileReader` extensions
4. Define `IReaderOptions` and `CsvReaderOptions` in Core layer
5. Update unit tests for interface contracts

**Deliverables**:
- `/src/SheetAtlas.Core/Application/Interfaces/IFileFormatReader.cs`
- `/src/SheetAtlas.Core/Application/Interfaces/IReaderOptions.cs`
- `/tests/SheetAtlas.Tests/Interfaces/IFileFormatReaderContractTests.cs`

---

### Phase 2: Refactor Existing OpenXmlFileReader (Week 1-2)

**Tasks**:
1. Extract current `ExcelReaderService` logic into `OpenXmlFileReader`
2. Implement `IFileFormatReader` interface
3. Keep existing dependencies (`ICellReferenceParser`, etc.)
4. Add `CancellationToken` support to async operations
5. Verify backward compatibility with existing tests

**Migration Strategy**:
```csharp
// Before: Monolithic service
public class ExcelReaderService : IExcelReaderService
{
    public async Task<ExcelFile> LoadFileAsync(string filePath)
    {
        // All logic here (OpenXML-specific)
    }
}

// After: Strategy-based
public class OpenXmlFileReader : IFileFormatReader
{
    // Extracted logic from ExcelReaderService
    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        // Same logic, now isolated
    }
}

public class ExcelReaderService : IExcelReaderService
{
    // Now just orchestrates readers
}
```

**Testing**:
- Run full test suite after refactoring
- No behavior changes - pure refactoring
- Performance benchmarks should match pre-refactor

---

### Phase 3: Add XLS Support (Week 2-3)

**Library Evaluation**:

**Option 1: ExcelDataReader** (RECOMMENDED)
```xml
<PackageReference Include="ExcelDataReader" Version="3.7.0" />
<PackageReference Include="ExcelDataReader.DataSet" Version="3.7.0" />
```

**Pros**:
- ✅ Mature library (10+ years, 4M+ downloads)
- ✅ Supports .xls (BIFF5/BIFF8) and .xlsx
- ✅ .NET 8 compatible
- ✅ MIT license (commercial use OK)
- ✅ Active maintenance (last update 2024)

**Cons**:
- ⚠️ Requires `System.Text.Encoding.CodePages` for legacy encodings
- ⚠️ Less feature-rich than OpenXML (e.g., no formula support)

**Option 2: NPOI**
```xml
<PackageReference Include="NPOI" Version="2.7.1" />
```

**Pros**:
- ✅ Supports both .xls and .xlsx
- ✅ More features (cell styles, formulas)

**Cons**:
- ❌ Heavier library (larger binary size)
- ❌ More complex API (steeper learning curve)
- ⚠️ Apache 2.0 license (commercial use OK but requires attribution)

**Recommendation**: Use **ExcelDataReader** for simplicity and compatibility.

**Implementation**:
```csharp
public class XlsFileReader : IFileFormatReader
{
    private readonly ILogger<XlsFileReader> _logger;

    public IReadOnlyList<string> SupportedExtensions =>
        new[] { ".xls", ".xlt" }.AsReadOnly();

    public XlsFileReader(ILogger<XlsFileReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Required for legacy Excel encodings
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public bool CanRead(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        var errors = new List<ExcelError>();
        var sheets = new Dictionary<string, DataTable>();

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        try
        {
            return await Task.Run(() =>
            {
                using var stream = File.OpenRead(filePath);
                using var reader = ExcelReaderFactory.CreateBinaryReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                foreach (DataTable table in dataSet.Tables)
                {
                    ct.ThrowIfCancellationRequested();
                    sheets[table.TableName] = table;
                }

                return new ExcelFile(filePath, LoadStatus.Success, sheets, errors);
            }, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation bubble up
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error reading XLS file: {Path}", filePath);
            errors.Add(ExcelError.Critical("File", $"Cannot access file: {ex.Message}", ex));
            return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading XLS file: {Path}", filePath);
            errors.Add(ExcelError.Critical("File", $"Invalid or corrupted XLS file: {ex.Message}", ex));
            return new ExcelFile(filePath, LoadStatus.Failed, sheets, errors);
        }
    }
}
```

**DI Registration**:
```csharp
// In Startup/Program.cs
services.AddSingleton<IFileFormatReader, OpenXmlFileReader>();
services.AddSingleton<IFileFormatReader, XlsFileReader>();
```

---

### Phase 4: Add CSV Support (Week 3-4)

**Library Evaluation**:

**Option 1: CsvHelper** (RECOMMENDED)
```xml
<PackageReference Include="CsvHelper" Version="33.0.1" />
```

**Pros**:
- ✅ Industry standard (100M+ downloads)
- ✅ Highly configurable (delimiters, quotes, culture)
- ✅ Excellent performance
- ✅ .NET 8 compatible
- ✅ MS-PL/Apache 2.0 license

**Option 2: Built-in TextFieldParser**
```csharp
using Microsoft.VisualBasic.FileIO;
```

**Pros**:
- ✅ No external dependency
- ✅ Simple API

**Cons**:
- ❌ Less flexible
- ❌ Requires `Microsoft.VisualBasic` reference (legacy)
- ❌ Limited configuration options

**Recommendation**: Use **CsvHelper** for production-grade CSV handling.

**Implementation**:
```csharp
public class CsvFileReader : IConfigurableFileReader, IStreamableFileReader
{
    private readonly ILogger<CsvFileReader> _logger;
    private CsvReaderOptions _options = new();

    public IReadOnlyList<string> SupportedExtensions =>
        new[] { ".csv", ".tsv", ".txt" }.AsReadOnly();

    public CsvFileReader(ILogger<CsvFileReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Configure(IReaderOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (options is CsvReaderOptions csvOptions)
            _options = csvOptions;
        else
            throw new ArgumentException($"Expected CsvReaderOptions, got {options.GetType().Name}");
    }

    public bool CanRead(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        using var stream = File.OpenRead(filePath);
        return await ReadAsync(stream, filePath, ct);
    }

    public async Task<ExcelFile> ReadAsync(Stream stream, string fileHint, CancellationToken ct)
    {
        var errors = new List<ExcelError>();
        var sheets = new Dictionary<string, DataTable>();

        try
        {
            return await Task.Run(() =>
            {
                var dataTable = new DataTable(Path.GetFileNameWithoutExtension(fileHint));

                using var reader = new StreamReader(stream, _options.Encoding);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = _options.Delimiter.ToString(),
                    HasHeaderRecord = _options.HasHeaderRow,
                    DetectDelimiter = _options.AutoDetectDelimiter
                });

                using var dataReader = new CsvDataReader(csv);
                dataTable.Load(dataReader);

                sheets["Sheet1"] = dataTable; // CSV has only one "sheet"

                return new ExcelFile(fileHint, LoadStatus.Success, sheets, errors);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading CSV file: {Path}", fileHint);
            errors.Add(ExcelError.Critical("File", $"Invalid CSV file: {ex.Message}", ex));
            return new ExcelFile(fileHint, LoadStatus.Failed, sheets, errors);
        }
    }
}

// Configuration options
public class CsvReaderOptions : IReaderOptions
{
    public char Delimiter { get; set; } = ',';
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool HasHeaderRow { get; set; } = true;
    public bool AutoDetectDelimiter { get; set; } = false;
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
}
```

---

### Phase 5: Testing Strategy

**Unit Tests**:
```csharp
// Test reader selection
[Fact]
public void LoadFileAsync_XlsxFile_UsesOpenXmlReader()
{
    // Arrange
    var mockOpenXml = new Mock<IFileFormatReader>();
    mockOpenXml.Setup(r => r.SupportedExtensions).Returns(new[] { ".xlsx" }.AsReadOnly());
    mockOpenXml.Setup(r => r.CanRead(It.IsAny<string>())).Returns(true);

    var service = new ExcelReaderService(new[] { mockOpenXml.Object }, logger);

    // Act
    var result = await service.LoadFileAsync("test.xlsx");

    // Assert
    mockOpenXml.Verify(r => r.ReadAsync("test.xlsx", It.IsAny<CancellationToken>()), Times.Once);
}

// Test error handling
[Fact]
public void LoadFileAsync_UnsupportedExtension_ReturnsFailedStatus()
{
    // Arrange
    var service = new ExcelReaderService(new IFileFormatReader[] { }, logger);

    // Act
    var result = await service.LoadFileAsync("test.unknown");

    // Assert
    result.Status.Should().Be(LoadStatus.Failed);
    result.Errors.Should().ContainSingle()
        .Which.Message.Should().Contain("Unsupported file format");
}
```

**Integration Tests**:
```csharp
[Theory]
[InlineData("test.xlsx", typeof(OpenXmlFileReader))]
[InlineData("test.xls", typeof(XlsFileReader))]
[InlineData("test.csv", typeof(CsvFileReader))]
public async Task LoadFileAsync_RealFiles_SelectsCorrectReader(string fileName, Type readerType)
{
    // Arrange
    var filePath = Path.Combine(TestDataPath, fileName);

    // Act
    var result = await _service.LoadFileAsync(filePath);

    // Assert
    result.Status.Should().Be(LoadStatus.Success);
    result.Sheets.Should().NotBeEmpty();
}
```

---

## 9. Potential Pitfalls & Mitigation

### Pitfall 1: Extension Collision

**Problem**: `.xlsx` files can be read by both OpenXML and ExcelDataReader.

**Current Risk**:
```csharp
// Both readers claim .xlsx
var xlsxReaders = _readers.Where(r => r.CanRead("file.xlsx")).ToList();
// Which one gets selected?
```

**Mitigation Strategy**:

**Option A: Explicit Priority** (RECOMMENDED)
```csharp
public interface IFileFormatReader
{
    // Add priority property (higher = preferred)
    int Priority { get; }
}

// In orchestrator
var reader = _readers
    .Where(r => r.CanRead(filePath))
    .OrderByDescending(r => r.Priority)
    .FirstOrDefault();
```

**Option B: Strict Extension Ownership**
```csharp
// Document that extensions should be exclusive
// OpenXmlFileReader: .xlsx, .xlsm, .xltx, .xltm
// XlsFileReader: .xls, .xlt (never .xlsx)
```

**Recommendation**: Use Option B (strict ownership) - simpler and clearer. Document in XML comments.

---

### Pitfall 2: Memory Leaks from DataTable

**Problem**: `DataTable` is not disposed automatically.

**Risk**:
```csharp
var file = await LoadFileAsync("large.xlsx");
// If file.Sheets contains large DataTables and file is never disposed...
// Memory leak!
```

**Mitigation**:

**Option 1: Make ExcelFile Disposable** (BREAKING CHANGE)
```csharp
public class ExcelFile : IDisposable
{
    public void Dispose()
    {
        foreach (var sheet in Sheets.Values)
        {
            sheet.Dispose();
        }
    }
}
```

**Option 2: Document Disposal Responsibility**
```csharp
/// <remarks>
/// WARNING: ExcelFile.Sheets contains DataTable instances that hold
/// significant memory. Ensure proper scope management or consider
/// explicit disposal after use:
/// <code>
/// var file = await LoadFileAsync(path);
/// try
/// {
///     // Use file
/// }
/// finally
/// {
///     foreach (var sheet in file.Sheets.Values)
///         sheet.Dispose();
/// }
/// </code>
/// </remarks>
```

**Recommendation**: For now, use Option 2 (documentation). Consider Option 1 for v2.0 (breaking change).

---

### Pitfall 3: Cancellation Not Propagated

**Problem**: Long operations inside readers might not check cancellation token.

**Risk**:
```csharp
public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
{
    // Long loop without cancellation checks
    for (int i = 0; i < 1000000; i++)
    {
        ProcessRow(i); // User clicked Cancel but nothing happens!
    }
}
```

**Mitigation**:
```csharp
public async Task<ExcelFile> ReadAsync(string filePath, CancellationToken ct)
{
    foreach (var row in rows)
    {
        ct.ThrowIfCancellationRequested(); // Check every N iterations
        ProcessRow(row);
    }
}
```

**Testing**:
```csharp
[Fact]
public async Task ReadAsync_CancellationRequested_ThrowsOperationCanceledException()
{
    var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => reader.ReadAsync("large.xlsx", cts.Token));
}
```

---

### Pitfall 4: CSV Encoding Detection

**Problem**: CSV files don't declare encoding - detection is guesswork.

**Risk**:
```csharp
// File is Windows-1252, reader assumes UTF-8
// Result: Corrupted special characters (é → Ã©)
```

**Mitigation**:

**Option 1: User Selection**
```csharp
// Let user choose encoding in UI
var options = new CsvReaderOptions
{
    Encoding = Encoding.GetEncoding("Windows-1252")
};
csvReader.Configure(options);
```

**Option 2: Automatic Detection Library**
```xml
<PackageReference Include="Ude.NetStandard" Version="1.2.0" />
```

**Recommendation**: Start with UTF-8 default (Option 1), add auto-detection later if needed.

---

### Pitfall 5: Thread Safety Violations

**Problem**: Readers registered as Singleton but use mutable state.

**Risk**:
```csharp
// BAD - mutable state in singleton
public class CsvFileReader : IFileFormatReader
{
    private CsvReaderOptions _options; // Shared across threads!

    public void Configure(IReaderOptions options)
    {
        _options = (CsvReaderOptions)options; // Race condition!
    }
}
```

**Mitigation**:

**Option 1: Immutable Configuration**
```csharp
public async Task<ExcelFile> ReadAsync(
    string filePath,
    IReaderOptions? options = null,
    CancellationToken ct = default)
{
    var csvOptions = options as CsvReaderOptions ?? DefaultOptions;
    // Use local variable, not instance field
}
```

**Option 2: Scoped Registration**
```csharp
// Change DI registration
services.AddScoped<IFileFormatReader, CsvFileReader>();
// New instance per scope - can have mutable state
```

**Recommendation**: Use Option 1 (stateless) - safer and more efficient.

---

### Pitfall 6: File Locking on Windows

**Problem**: File remains locked after reading, preventing deletion.

**Risk**:
```csharp
using var doc = SpreadsheetDocument.Open(filePath, false);
// Even with isEditable=false, file might be locked on Windows
```

**Mitigation**:
```csharp
// Copy to memory stream first (for small/medium files)
using var fileStream = File.OpenRead(filePath);
using var memoryStream = new MemoryStream();
await fileStream.CopyToAsync(memoryStream, ct);
memoryStream.Position = 0;

using var doc = SpreadsheetDocument.Open(memoryStream, false);
// File handle released, no lock
```

**Trade-off**: Uses more memory but prevents file locking.

**Recommendation**: Make this configurable - streaming for large files, memory for small files.

---

## 10. Success Metrics

### Definition of Done

**Interface Implementation** ✅:
- [ ] `IFileFormatReader` interface defined with XML documentation
- [ ] `IStreamableFileReader` extension interface created
- [ ] `IConfigurableFileReader` extension interface created
- [ ] All interfaces include cancellation token support
- [ ] Error handling contract documented (Result Pattern)

**Backward Compatibility** ✅:
- [ ] Existing `IExcelReaderService` interface unchanged
- [ ] All existing unit tests pass without modification
- [ ] Performance benchmarks within 5% of baseline
- [ ] UI functionality unchanged

**New Format Support** ✅:
- [ ] `.xls` files load successfully (ExcelDataReader)
- [ ] `.csv` files load successfully (CsvHelper)
- [ ] Configurable CSV delimiter/encoding works
- [ ] Edge cases handled (corrupted files, empty files, large files)

**Testing Coverage** ✅:
- [ ] Unit tests for each reader (90%+ coverage)
- [ ] Integration tests with real files (all formats)
- [ ] Performance tests (10MB+ files)
- [ ] Cancellation token tests (all readers)
- [ ] Error handling tests (invalid files)

**Documentation** ✅:
- [ ] XML documentation for all public interfaces
- [ ] README updated with supported formats
- [ ] Architecture diagram updated
- [ ] Usage examples added to docs

---

### Performance Benchmarks

**Target Metrics**:
| File Type | Size | Load Time | Memory Usage |
|-----------|------|-----------|--------------|
| .xlsx     | 10MB | <2s       | <100MB       |
| .xls      | 10MB | <3s       | <120MB       |
| .csv      | 10MB | <1s       | <80MB        |

**Regression Tests**:
```csharp
[Benchmark]
public async Task LoadXlsxFile_10MB()
{
    await _service.LoadFileAsync(_testFilePath);
}
```

---

## 11. Final Recommendations Summary

### DO ✅

1. **Implement the proposed Strategy Pattern** - correct architectural choice
2. **Add `CancellationToken` parameter** to all async methods - critical for UX
3. **Add XML documentation** with error handling contract - prevents misuse
4. **Keep readers stateless** - simplifies threading, testing, DI
5. **Use extension-only checking in `CanRead`** - fast, deterministic, predictable
6. **Follow Result Pattern** - return `ExcelFile` with errors, don't throw
7. **Add `IStreamableFileReader`** interface - future-proofs for large files
8. **Use ExcelDataReader for .xls** - mature, compatible, MIT licensed
9. **Use CsvHelper for .csv** - industry standard, highly configurable
10. **Maintain backward compatibility** - existing code continues working

### DON'T ❌

1. **Don't use abstract base class** - interface is more flexible
2. **Don't use Factory Pattern exclusively** - loses DI benefits
3. **Don't use metadata/reflection** - too complex, breaks trimming
4. **Don't throw for business errors** - violates project philosophy
5. **Don't make `CanRead` do I/O** - should be fast extension check only
6. **Don't forget cancellation checks** in long loops - UX requirement
7. **Don't use mutable state in readers** - thread-safety issues
8. **Don't break existing tests** - backward compatibility requirement
9. **Don't skip documentation** - critical for correct usage
10. **Don't over-engineer for hypothetical needs** - follow "Functional minimalism"

---

## 12. Approval & Next Steps

### Design Approval: ✅ APPROVED WITH MODIFICATIONS

**Approved Aspects**:
- Strategy Pattern architecture
- Interface-based abstraction
- Clean Architecture layer separation
- Reader implementations (OpenXML, XLS, CSV)
- DI-based orchestration

**Required Modifications Before Implementation**:
1. Add `CancellationToken cancellationToken = default` to `ReadAsync`
2. Add XML documentation specifying error handling contract
3. Clarify `CanRead` as extension-only check (no I/O)
4. Add `IStreamableFileReader` interface for future streaming support
5. Add `IConfigurableFileReader` for CSV configuration
6. Update orchestrator to return proper error messages

**Optional Enhancements** (consider for v2.0):
- Make `ExcelFile` implement `IDisposable` for DataTable cleanup
- Add reader priority system for extension collision handling
- Add progress reporting interface for large file operations
- Implement automatic encoding detection for CSV files

---

### Implementation Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| Phase 1 | Week 1 | Interfaces defined, documented, tested |
| Phase 2 | Week 1-2 | OpenXmlFileReader extracted, backward compatible |
| Phase 3 | Week 2-3 | XlsFileReader implemented, tested |
| Phase 4 | Week 3-4 | CsvFileReader implemented, configured |
| Phase 5 | Week 4 | Integration tests, performance benchmarks |

**Total Estimated Effort**: 4 weeks (1 developer)

---

### Review Sign-Off

**Reviewed By**: Claude Code (API Design Agent)
**Date**: 2025-10-07
**Decision**: APPROVED WITH MODIFICATIONS
**Next Reviewer**: Lead Developer / Architect

**Action Items**:
1. Share this document with development team
2. Discuss modifications in technical meeting
3. Create feature branch: `feature/multi-format-readers`
4. Begin Phase 1 implementation after approval
5. Schedule design review checkpoint after Phase 2

---

*End of Design Review Document*

---

## Appendix A: Complete Code Examples

### A.1 Recommended Interface (Final)

```csharp
namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Represents a reader capable of loading specific Excel file formats
    /// </summary>
    public interface IFileFormatReader
    {
        /// <summary>
        /// File extensions supported by this reader (lowercase with leading dot)
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }

        /// <summary>
        /// Determines if this reader can handle the file extension (NO I/O)
        /// </summary>
        bool CanRead(string filePath);

        /// <summary>
        /// Reads Excel file and returns domain entity with Result Pattern
        /// </summary>
        /// <exception cref="ArgumentNullException">If filePath is null</exception>
        /// <exception cref="OperationCanceledException">If cancelled</exception>
        Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extended interface for stream-based reading (large files, testing)
    /// </summary>
    public interface IStreamableFileReader : IFileFormatReader
    {
        Task<ExcelFile> ReadAsync(Stream stream, string fileHint, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extended interface for runtime-configurable readers (e.g., CSV)
    /// </summary>
    public interface IConfigurableFileReader : IFileFormatReader
    {
        void Configure(IReaderOptions options);
    }

    /// <summary>
    /// Base interface for reader configuration
    /// </summary>
    public interface IReaderOptions { }
}
```

### A.2 CSV Configuration Options

```csharp
namespace SheetAtlas.Core.Application.DTOs
{
    public class CsvReaderOptions : IReaderOptions
    {
        public char Delimiter { get; set; } = ',';
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        public bool HasHeaderRow { get; set; } = true;
        public bool AutoDetectDelimiter { get; set; } = false;
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

        public static CsvReaderOptions Default => new();

        public static CsvReaderOptions Semicolon => new() { Delimiter = ';' };

        public static CsvReaderOptions Tab => new() { Delimiter = '\t' };
    }
}
```

---

## Appendix B: Migration Checklist

### Pre-Implementation Checklist

- [ ] Design review approved by team lead
- [ ] Breaking changes identified and documented
- [ ] Backward compatibility strategy confirmed
- [ ] Test data prepared (sample .xls, .csv files)
- [ ] Library licenses verified for commercial use
- [ ] Performance baseline established

### Implementation Checklist

- [ ] Branch created: `feature/multi-format-readers`
- [ ] Interfaces defined with XML docs
- [ ] OpenXmlFileReader refactored (no behavior change)
- [ ] XlsFileReader implemented and tested
- [ ] CsvFileReader implemented and tested
- [ ] Orchestrator refactored with reader selection
- [ ] Unit tests added (90%+ coverage)
- [ ] Integration tests added (real files)
- [ ] Performance benchmarks run
- [ ] Documentation updated

### Pre-Merge Checklist

- [ ] All tests passing
- [ ] Code review completed
- [ ] Performance regression check passed
- [ ] Documentation updated (README, technical specs)
- [ ] CLAUDE.md updated if needed
- [ ] Changelog entry added
- [ ] Demo prepared for stakeholders

---

**Document Version**: 1.0
**Last Updated**: 2025-10-07
**Next Review**: After Phase 2 completion
