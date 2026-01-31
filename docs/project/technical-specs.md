# SheetAtlas - Technical Specifications

Detailed technical specifications for performance, security, and configuration.

For architecture overview and diagrams, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Project Structure

```
SheetAtlas/
├── src/
│   ├── SheetAtlas.Core/                 # Business logic (platform agnostic)
│   │   ├── Application/
│   │   │   ├── DTOs/                    # Data transfer objects
│   │   │   ├── Interfaces/              # Service contracts (19 interfaces)
│   │   │   ├── Services/                # Application services
│   │   │   │   ├── Foundation/          # Foundation services (analysis, normalization)
│   │   │   │   └── HeaderResolvers/     # Header resolution strategies
│   │   │   └── Utilities/               # Helper utilities
│   │   ├── Domain/
│   │   │   ├── Entities/                # Domain entities (ExcelFile, SASheetData, etc.)
│   │   │   ├── Exceptions/              # Domain exceptions
│   │   │   └── ValueObjects/            # Value objects (SACellData, DataType, etc.)
│   │   └── Shared/Helpers/              # Shared utilities
│   │
│   ├── SheetAtlas.Infrastructure/       # External integrations
│   │   └── External/
│   │       ├── Readers/                 # File format readers + FileReaderContext
│   │       └── Writers/                 # Export writers (Excel, comparison)
│   │
│   ├── SheetAtlas.Logging/              # Cross-cutting logging
│   │   ├── Models/                      # Log models
│   │   └── Services/                    # ILogService implementation
│   │
│   └── SheetAtlas.UI.Avalonia/          # Presentation layer (MVVM)
│       ├── Views/                       # XAML views
│       ├── ViewModels/                  # View models (23)
│       ├── Managers/                    # UI coordinators (9)
│       ├── Controls/                    # Custom controls
│       ├── Converters/                  # Value converters
│       ├── Services/                    # UI services
│       └── Styles/                      # Themes and styles
│
├── tests/SheetAtlas.Tests/              # Unit and integration tests
├── docs/                                # Documentation
└── assets/                              # Resources and icons
```

## Technology Stack

### Core Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 LTS | Framework |
| C# | 12 | Language |
| Avalonia UI | 11.0.10 | Cross-platform UI |
| DocumentFormat.OpenXml | 3.2.0 | XLSX processing |
| ExcelDataReader | 3.7.0 | XLS support |
| CsvHelper | 33.0.1 | CSV parsing |

### Dependencies

```xml
<!-- File Processing -->
<PackageReference Include="DocumentFormat.OpenXml" Version="3.2.0" />
<PackageReference Include="ExcelDataReader" Version="3.7.0" />
<PackageReference Include="CsvHelper" Version="33.0.1" />

<!-- DI & Configuration -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.2" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.2" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.2" />

<!-- UI -->
<PackageReference Include="Avalonia" Version="11.0.10" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />

<!-- Testing -->
<PackageReference Include="xUnit" Version="2.6.1" />
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

## Core Interfaces

### File Reading

```csharp
public interface IExcelReaderService
{
    Task<ExcelFile> LoadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<List<ExcelFile>> LoadFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
}

public interface IFileFormatReader
{
    IReadOnlyList<string> SupportedExtensions { get; }
    Task<ExcelFile> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
```

### Export

```csharp
public interface IExcelWriterService
{
    Task<ExportResult> WriteToExcelAsync(SASheetData sheetData, string outputPath,
        ExcelExportOptions? options = null, CancellationToken cancellationToken = default);
    Task<ExportResult> WriteToCsvAsync(SASheetData sheetData, string outputPath,
        CsvExportOptions? options = null, CancellationToken cancellationToken = default);
}
```

### Row Comparison

```csharp
public interface IRowComparisonService
{
    RowComparison CreateRowComparison(RowComparisonRequest request);
    ExcelRow ExtractRowFromSearchResult(SearchResult searchResult);
    IReadOnlyList<string> GetColumnHeaders(ExcelFile file, string sheetName);
}
```

## Domain Entities

### ExcelFile

```csharp
public class ExcelFile : IDisposable
{
    public string FilePath { get; }
    public string FileName { get; }
    public LoadStatus Status { get; }
    public DateTime LoadedAt { get; }
    public IReadOnlyDictionary<string, SASheetData> Sheets { get; }
    public IReadOnlyList<ExcelError> Errors { get; }
    public DateSystem DateSystem { get; }

    public SASheetData? GetSheet(string sheetName);
    public bool HasErrors { get; }
    public bool HasWarnings { get; }
}
```

### SearchResult

```csharp
public class SearchResult
{
    public ExcelFile SourceFile { get; }
    public string SheetName { get; }
    public int Row { get; }           // 0-based absolute index
    public int Column { get; }
    public string Value { get; }
    public string CellAddress { get; } // e.g., "B5"
}
```

## Performance Requirements

### File Processing

| File Size | Target Load Time |
|-----------|------------------|
| < 1 MB | < 500 ms |
| 1-10 MB | < 2 seconds |
| 10-100 MB | < 10 seconds |

### Memory

- **Target**: < 500 MB for largest supported files
- **Optimization**: SASheetData uses flat array (2-3x overhead vs 10-14x for DataTable)
- **Concurrency**: Configurable `MaxConcurrentFileLoads` (default: 5)

### UI Responsiveness

- **File loading**: Progress indication with cancellation
- **Operations**: Background processing with async/await
- **Rendering**: < 100 ms response time

## Security Requirements

### Data Protection

- **Local processing only**: No network communication
- **Memory management**: IDisposable pattern with finalizers
- **File access**: Read-only for source files
- **Temp files**: Secure deletion after processing

### Configuration

Security settings in `appsettings.json`:

```json
{
  "AppSettings": {
    "Security": {
      "MaxFileSizeMB": 100,
      "AllowedExtensions": [".xlsx", ".xls", ".csv", ".xlsm"]
    },
    "Performance": {
      "MaxConcurrentFileLoads": 5
    }
  }
}
```

## Testing Strategy

### Coverage

| Layer | Target | Current |
|-------|--------|---------|
| Core Services | 90%+ | ~85% |
| Foundation | 90%+ | ~90% |
| Integration | Key flows | 1 test file |
| UI | Critical paths | Limited |

### Test Organization

```
tests/SheetAtlas.Tests/
├── Foundation/Services/     # Foundation layer tests
├── Services/                # Application service tests
├── Integration/             # File loading integration
├── Models/                  # Domain model tests
└── Helpers/                 # Utility tests
```

## Configuration Reference

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SheetAtlas": "Debug"
    }
  },
  "AppSettings": {
    "Analysis": {
      "MinRowsForTypeInference": 5,
      "MaxRowsForTypeInference": 1000,
      "MergedCellStrategy": "UseFirstValue"
    },
    "Performance": {
      "MaxConcurrentFileLoads": 5
    },
    "Security": {
      "MaxFileSizeMB": 100,
      "AllowedExtensions": [".xlsx", ".xls", ".csv", ".xlsm"]
    }
  }
}
```

### User Settings (persisted)

- Theme (Light/Dark/System)
- Default header row count
- Default export format
- Output folder path

## Row Indexing Convention

**All internal indices are 0-based absolute.**

| Context | Format | Example |
|---------|--------|---------|
| Internal (SASheetData) | 0-based | Row 0 = first row |
| Display (UI) | 1-based | "R1" = first row |
| Search results | 0-based | `SearchResult.Row = 0` for first row |

Header detection: `row < HeaderRowCount` (default: 1).

---

*Last updated: January 2026*

See also:
- [ARCHITECTURE.md](ARCHITECTURE.md) — Architecture overview with diagrams
- [RELEASE_PROCESS.md](../RELEASE_PROCESS.md) — Release workflow
- [CLAUDE.md](../../CLAUDE.md) — Development standards
