# Architecture

SheetAtlas follows Clean Architecture with four layers: UI, Core, Infrastructure, and Logging.

## System Context

```mermaid
graph TB
    User([User])
    SA[SheetAtlas]
    Files[(Excel/CSV Files)]
    Templates[(Templates)]
    Settings[(User Settings)]

    User -->|loads, searches, compares| SA
    SA -->|reads| Files
    SA -->|validates against| Templates
    SA -->|persists| Settings
```

## Layer Architecture

```mermaid
graph TB
    subgraph UI["UI Layer (Avalonia)"]
        Views[Views]
        VM[ViewModels]
        Mgr[Managers]
    end

    subgraph Core["Core Layer"]
        App[Application Services]
        Found[Foundation Services]
        Domain[Domain]
    end

    subgraph Infra["Infrastructure"]
        Readers[File Readers]
        Writers[File Writers]
    end

    Log[Logging]

    Views --> VM
    VM --> Mgr
    Mgr --> App
    App --> Found
    App --> Domain
    Found --> Domain
    Infra --> App

    UI -.-> Log
    Core -.-> Log
    Infra -.-> Log
```

**Dependency Rule**: All dependencies flow downward. Core has no knowledge of UI or Infrastructure.

## Layers

### UI (SheetAtlas.UI.Avalonia)

| Component | Purpose |
|-----------|---------|
| **Views** | XAML, no code-behind logic |
| **ViewModels** | MVVM state and commands |
| **Managers** | UI coordination (Files, Search, Theme, Navigation) |
| **Controls** | `SheetGridCanvas` — custom-rendered spreadsheet grid with drag selection for DataRegion editing (ADR-010) |
| **Services** | UI-specific (dialogs, file picker) |

### Core (SheetAtlas.Core)

**Application Services**:
- `SearchService` — full-text search across sheets (region-scoped when a DataRegion is active)
- `RowComparisonService` — row-by-row diff
- `SheetAnalysisOrchestrator` — analysis pipeline
- `ColumnLinkingService` — cross-file column linking
- `RegionDetectionService` — header-anchored DataRegion detection across files (ADR-012)
- `DataRegionPersistenceService` — JSON persistence of DataRegion definitions (ADR-011)
- `SettingsService` — user preferences

**Foundation Services**:
- `ColumnAnalysisService` — data type detection
- `DataNormalizationService` — value normalization
- `CurrencyDetector` — currency parsing
- `MergedCellResolver` — merged cell strategies
- `TemplateValidationService` — column validation, region-scoped (ADR-014)

**Domain**:
- `ExcelFile`, `SASheetData` — file/sheet representation, coordinate-preserving (ADR-013)
- `SACellData`, `SACellValue` — cell data
- `DataRegion` — named rectangular scope within a sheet (ADR-009)
- `SearchResult`, `RowComparison` — operation results

### Infrastructure (SheetAtlas.Infrastructure)

| Reader | Format | Library |
|--------|--------|---------|
| `OpenXmlFileReader` | .xlsx | DocumentFormat.OpenXml |
| `XlsFileReader` | .xls | ExcelDataReader |
| `CsvFileReader` | .csv | CsvHelper |

`FileReaderContext` facade groups common reader dependencies (see ADR-008).

Writers: `ExcelWriterService` (including `NormalizeToExcelAsync` for
in-place column-type correction based on dominant type), `ComparisonExportService`.

### Logging (SheetAtlas.Logging)

Cross-cutting concern. `ILogService` abstraction injected into all layers.

## Key Patterns

| Pattern | Usage |
|---------|-------|
| **Strategy** | `IFileFormatReader` — pluggable file readers |
| **Facade** | `FileReaderContext` — groups reader dependencies |
| **MVVM** | ViewModels + Managers for UI |
| **Result Object** | Business errors returned, not thrown |

## Data Flow

### File Loading

```mermaid
sequenceDiagram
    participant UI as ViewModel
    participant ERS as ExcelReaderService
    participant Reader as IFileFormatReader
    participant SAO as SheetAnalysisOrchestrator

    UI->>ERS: LoadFileAsync(path)
    ERS->>Reader: ReadAsync(path)
    Reader-->>ERS: SASheetData (raw)
    ERS->>SAO: EnrichSheetData()
    SAO-->>ERS: SASheetData (normalized)
    ERS-->>UI: ExcelFile
```

### Search

```mermaid
sequenceDiagram
    participant UI as SearchViewModel
    participant SS as SearchService
    participant Data as SASheetData

    UI->>SS: SearchAsync(query)
    SS->>Data: Iterate rows
    SS-->>UI: SearchResult[]
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| DocumentFormat.OpenXml | 3.2.0 | XLSX read/write |
| ExcelDataReader | 3.7.0 | XLS support |
| CsvHelper | 33.0.1 | CSV parsing |
| Avalonia | 11.0.10 | Cross-platform UI |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM utilities |
| Microsoft.Extensions.DI | 9.0.2 | Dependency injection |

## Project Structure

```
src/
├── SheetAtlas.Core/
│   ├── Application/
│   │   ├── Interfaces/      # Service contracts
│   │   ├── Services/        # Application services
│   │   │   └── Foundation/  # Foundation services
│   │   └── DTOs/
│   └── Domain/
│       ├── Entities/
│       └── ValueObjects/
├── SheetAtlas.Infrastructure/
│   └── External/
│       ├── Readers/         # File format readers
│       └── Writers/         # Export writers
├── SheetAtlas.Logging/
└── SheetAtlas.UI.Avalonia/
    ├── Views/
    ├── ViewModels/
    └── Managers/
```

## Related

- [ADR-007: Unified Data Flow](../../.development/reference/decisions/007-unified-data-flow-for-export.md)
- [ADR-008: Facade Pattern](../../.development/reference/decisions/008-facade-pattern-for-dependency-injection.md)
- [ADR-009 / 010 / 011 / 012: DataRegion](../../.development/reference/decisions/) — data model, UI pattern, persistence, cross-file detection
- [ADR-013: Coordinate-Preserving Storage](../../.development/reference/decisions/013-coordinate-preserving-storage.md)
- [ADR-014: Region-Scoped Template Validation](../../.development/reference/decisions/014-region-scoped-template-validation.md)
- [ADR-015: Non-Destructive File Removal](../../.development/reference/decisions/015-non-destructive-file-removal.md)
- [Technical Specs](technical-specs.md) — performance, security, config details

---

*Last updated: April 2026*
