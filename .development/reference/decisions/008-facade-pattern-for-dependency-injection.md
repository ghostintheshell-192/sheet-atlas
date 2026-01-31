# ADR-008: Facade Pattern for File Reader Dependency Injection

**Status**: Active
**Date**: 2026-01-31

## Context

All file format readers (OpenXmlFileReader, XlsFileReader, CsvFileReader) shared identical dependencies:
- ILogService (logging)
- ISheetAnalysisOrchestrator (sheet enrichment)
- ISettingsService (user settings)
- IOptions<AppSettings> (security settings)

This created constructor over-injection (OpenXmlFileReader: 7 parameters, CsvFileReader: 5, XlsFileReader: 4) and maintenance burden when adding common dependencies.

Pattern inspired by legacy WPF "PanelsIO" facade used in cockpit simulators, adapted for modern type-safe dependency injection.

## Decision

Introduce `FileReaderContext` facade to group the 4 common dependencies:

```csharp
public class FileReaderContext
{
    public ILogService Logger { get; }
    public ISheetAnalysisOrchestrator AnalysisOrchestrator { get; }
    public ISettingsService Settings { get; }
    public SecuritySettings SecuritySettings { get; }
}
```

Readers now receive context plus format-specific dependencies:
- XlsFileReader: 4 params → 1 param (context only)
- CsvFileReader: 5 params → 2 params (context + format inference)
- OpenXmlFileReader: 7 params → 4 params (context + 3 format-specific)

Average reduction: 57% fewer constructor parameters.

## Rationale

- **Semantic grouping**: The 4 dependencies represent common reader infrastructure
- **Scalability**: New readers automatically receive common infrastructure
- **Maintainability**: Adding/removing common dependency = single change point
- **Type safety**: Compile-time dependency resolution (no Service Locator magic)

## Consequences

### Positive
- Improved readability (shorter constructors)
- Better maintainability (single change point for common dependencies)
- Reduced boilerplate (less repetitive null-checking)
- Pattern extends naturally to future readers

### Negative
- One extra level of indirection (_context.Logger vs _logger)
- Less granular visibility (constructor doesn't show all 4 dependencies explicitly)
- Tests must create FileReaderContext instead of individual mocks

## Alternatives Considered

1. **Auto-registration (Scrutor)**: Rejected - too "magical", hard to debug, requires additional library
2. **Service Locator**: Rejected - anti-pattern, hides dependencies, runtime errors
3. **Property Injection**: Rejected - breaks immutability, no compile-time guarantees
4. **Status quo**: Rejected - constructor over-injection is recognized code smell

## Related

- ADR-003: Technology Stack (.NET 8 + modern DI)
- ADR-001: Error Handling Philosophy (fail-fast constructor validation)
- Legacy inspiration: WPF "PanelsIO" pattern (cockpit simulators)
