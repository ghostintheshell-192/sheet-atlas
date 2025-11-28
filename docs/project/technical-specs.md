# SheetAtlas - Technical Specifications

## Architecture Overview

### High-Level Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                      UI Layer (Avalonia)                    │
│  ┌───────────┐  ┌────────────┐  ┌───────────┐  ┌─────────┐ │
│  │  Views    │  │ ViewModels │  │ Managers  │  │Controls │ │
│  │  (XAML)   │  │  (MVVM)    │  │           │  │         │ │
│  └───────────┘  └────────────┘  └───────────┘  └─────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Core Layer                              │
│  ┌─────────────────────────┐  ┌───────────────────────────┐│
│  │     Application         │  │         Domain            ││
│  │  ┌─────────┐ ┌───────┐  │  │  ┌──────────┐ ┌────────┐  ││
│  │  │Services │ │  DTOs │  │  │  │ Entities │ │ Values │  ││
│  │  └─────────┘ └───────┘  │  │  └──────────┘ └────────┘  ││
│  └─────────────────────────┘  └───────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│Infrastructure │    │    Logging    │    │   External    │
│  ┌─────────┐  │    │  ┌─────────┐  │    │   Services    │
│  │ Readers │  │    │  │Providers│  │    │   (future)    │
│  └─────────┘  │    │  └─────────┘  │    │               │
└───────────────┘    └───────────────┘    └───────────────┘
```

### Project Structure
```
SheetAtlas/
├── src/
│   ├── SheetAtlas.Core/              # Business logic layer
│   │   ├── Application/              # Application services
│   │   │   ├── DTOs/                 # Data transfer objects
│   │   │   ├── Interfaces/           # Service abstractions
│   │   │   ├── Services/             # Business services
│   │   │   └── Utilities/            # Helper utilities
│   │   ├── Domain/                   # Domain layer
│   │   │   ├── Entities/             # Domain entities
│   │   │   ├── Exceptions/           # Domain exceptions
│   │   │   └── ValueObjects/         # Value objects
│   │   └── Shared/                   # Shared helpers
│   │
│   ├── SheetAtlas.Infrastructure/    # External integrations
│   │   └── External/Readers/         # File format readers
│   │
│   ├── SheetAtlas.Logging/           # Logging infrastructure
│   │   ├── Models/                   # Log models
│   │   └── Services/                 # Logging services
│   │
│   └── SheetAtlas.UI.Avalonia/       # Presentation layer
│       ├── Views/                    # XAML views
│       ├── ViewModels/               # View models
│       ├── Managers/                 # UI managers (Files, Search, Theme, etc.)
│       ├── Controls/                 # Custom controls (CollapsibleSection)
│       ├── Converters/               # Value converters
│       ├── Commands/                 # UI commands
│       ├── Services/                 # UI services
│       └── Styles/                   # Themes and styles
│
├── tests/
│   └── SheetAtlas.Tests/             # Unit and integration tests
│
├── docs/                             # Documentation
└── assets/                           # Resources and assets
```

## Technology Stack

### Core Technologies
- **.NET 8**: Latest LTS framework
- **C# 12**: Modern language features
- **Avalonia UI 11.x**: Cross-platform UI framework
- **DocumentFormat.OpenXml**: Excel file processing

### Dependencies
```xml
<!-- Core Dependencies -->
<PackageReference Include="DocumentFormat.OpenXml" Version="3.2.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />

<!-- UI Dependencies -->
<PackageReference Include="Avalonia" Version="11.0.x" />
<PackageReference Include="Avalonia.Desktop" Version="11.0.x" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.x" />

<!-- Testing Dependencies -->
<PackageReference Include="xUnit" Version="2.6.x" />
<PackageReference Include="Moq" Version="4.20.x" />
<PackageReference Include="FluentAssertions" Version="6.12.x" />
```

## Core Components

### Data Models

#### ExcelFile Entity
```csharp
public class ExcelFile
{
    public string FilePath { get; }
    public string FileName { get; }
    public LoadStatus Status { get; }
    public Dictionary<string, DataTable> Sheets { get; }
    public List<FileError> Errors { get; }
    public DateTime LoadedAt { get; }
}
```

#### Comparison Result
```csharp
public class ComparisonResult
{
    public ExcelFile LeftFile { get; }
    public ExcelFile RightFile { get; }
    public List<SheetComparison> SheetComparisons { get; }
    public ComparisonSummary Summary { get; }
    public DateTime ComparedAt { get; }
}
```

### Services

#### IExcelReaderService
```csharp
public interface IExcelReaderService
{
    Task<ExcelFile> LoadFileAsync(string filePath);
    Task<List<ExcelFile>> LoadFilesAsync(IEnumerable<string> filePaths);
    bool ValidateFile(string filePath);
}
```

#### IComparisonService
```csharp
public interface IComparisonService
{
    Task<ComparisonResult> CompareFilesAsync(ExcelFile left, ExcelFile right);
    Task<ComparisonResult> CompareFilesAsync(string leftPath, string rightPath);
    ComparisonOptions Options { get; set; }
}
```

#### IExportService
```csharp
public interface IExportService
{
    Task ExportToHtmlAsync(ComparisonResult result, string outputPath);
    Task ExportToPdfAsync(ComparisonResult result, string outputPath);
    Task ExportToExcelAsync(ComparisonResult result, string outputPath);
}
```

## Performance Requirements

### File Processing
- **Small files** (<1MB): <500ms loading time
- **Medium files** (1-10MB): <2 seconds loading time
- **Large files** (10-100MB): <10 seconds loading time
- **Memory usage**: <500MB for largest supported files

### Comparison Performance
- **Sheet comparison**: <1 second for 1000x100 cells
- **Full file comparison**: <5 seconds for medium files
- **Real-time updates**: <100ms response time

### UI Responsiveness
- **File loading**: Progress indication with cancellation
- **Comparison**: Background processing with updates
- **Rendering**: Smooth scrolling for large data sets

## Security Requirements

### Data Protection
- **Local processing only**: No network communication
- **Memory management**: Secure cleanup of sensitive data
- **File access**: Read-only access to source files
- **Temp files**: Secure deletion after processing

### Licensing & Protection
- **License validation**: Local license file verification
- **Anti-tampering**: Basic code obfuscation
- **Audit trail**: Usage logging for enterprise versions

## Testing Strategy

### Unit Testing
- **Core services**: 90%+ code coverage
- **Business logic**: Comprehensive test cases
- **Error handling**: Exception scenarios
- **Performance**: Benchmark tests

### Integration Testing
- **File processing**: Real Excel files
- **Cross-platform**: Automated testing on all platforms
- **UI integration**: View model interactions

### Manual Testing
- **User experience**: Usability testing
- **Performance**: Large file handling
- **Edge cases**: Corrupted/unusual files

## Configuration

### Application Settings
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SheetAtlas": "Debug"
    }
  },
  "Comparison": {
    "DefaultIgnoreCase": true,
    "DefaultIgnoreWhitespace": false,
    "MaxFileSize": "100MB"
  },
  "UI": {
    "Theme": "Auto",
    "Language": "en-US"
  }
}
```

### License Configuration
```json
{
  "License": {
    "Type": "Commercial",
    "ExpiryDate": "2025-12-31",
    "Features": ["Comparison", "Export", "Batch"]
  }
}
```

---

*Last updated: November 2025*

For build commands and platform support, see [README.md](../../README.md).
For release process, see [RELEASE_PROCESS.md](../RELEASE_PROCESS.md).