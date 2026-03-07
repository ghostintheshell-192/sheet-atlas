# ADR-007: Unified Data Flow for Export

**Status**: Active
**Date**: 2025-11-28 (Proposed) → 2026-01-31 (Implemented)
**Context**: Template Management System (Week 3)
**Impact**: important
**Summary**: Single unified data flow where CleanedValue is canonical representation. Both internal operations (search, compare) and export use same normalized data. ExcelWriterService writes typed cells preserving number formats and data types.

## Context

When implementing the "Normalize & Copy to Clipboard" feature, we discovered a fundamental architectural problem:

1. **DataNormalizationService** normalizes data for INTERNAL use (comparisons, search, type detection)
2. When copying TSV to clipboard and pasting in Excel, Excel RE-INTERPRETS the data
3. Example: A date stored as serial number (45292) becomes "2024-01-15" in CleanedValue, but when pasted as text, Excel may interpret it as something else entirely

This causes DATA CORRUPTION in the export flow.

## Decision

Implement a SINGLE unified data flow where:

```
Excel File → Reader → SASheetData (raw values)
                           ↓
              SheetAnalysisOrchestrator (normalizes data)
                           ↓
              SASheetData + NormalizationResults (preserved)
                           ↓
                    ┌──────┴──────┐
                    ↓             ↓
           INTERNAL USE      EXPORT USE
           (search, compare) (ExcelWriterService)
                              writes typed cells
                              directly to Excel
```

Key principle: **CleanedValue is the canonical representation.** Both internal operations AND export use the same normalized data.

## Implementation Plan

### Phase 1: Preserve NormalizationResult in SASheetData

Currently `SASheetData` stores only the raw value. We need to either:
- Option A: Store both OriginalValue and CleanedValue in each cell
- Option B: Create a parallel data structure for normalized data
- **Option C (Recommended)**: Extend `SACellData` to include `NormalizationResult?`

### Phase 2: Create ExcelWriterService

```csharp
public interface IExcelWriterService
{
    /// <summary>
    /// Writes normalized data to Excel file with proper typing.
    /// Uses CleanedValue.Type to write cells as Number, Date, Text, Boolean.
    /// </summary>
    Task<ExportResult> WriteToExcelAsync(
        SASheetData data,
        string outputPath,
        ExportOptions options);

    /// <summary>
    /// Writes to CSV (text format, properly escaped).
    /// </summary>
    Task<ExportResult> WriteToCsvAsync(
        SASheetData data,
        string outputPath,
        CsvOptions options);
}
```

### Phase 3: UI Integration

- Remove TSV clipboard copy (causes corruption)
- Add "Export as Excel" button → uses ExcelWriterService
- Add "Export as CSV" button → uses ExcelWriterService
- Show diff between OriginalValue and CleanedValue in UI (optional)

## Consequences

### Positive
- Single source of truth for normalized data
- Export produces correct Excel types (dates as dates, numbers as numbers)
- Users can trust the exported data
- Foundation for future features (data cleaning, transformation)

### Negative
- Increased memory usage (storing both original and cleaned values)
- More complexity in SASheetData structure
- Migration path needed for existing code

### Risks
- Memory pressure for very large files (mitigated by lazy loading)
- Performance impact during enrichment (mitigated by parallel processing)

## Alternatives Considered

1. **Keep TSV clipboard**: Rejected - fundamental data corruption issue
2. **Format-specific export**: Rejected - duplicates normalization logic
3. **Re-normalize on export**: Rejected - violates "single source of truth"

## Notes

The current DataNormalizationService is well-designed for this use case:
- `NormalizationResult.CleanedValue` is already an `SACellValue` with proper type
- `SACellValue.Type` (Number, DateTime, Boolean, Text) maps directly to Excel cell types
- We just need to preserve the result and use it in export

## Related

- ADR-004: Foundation Layer First
- DataNormalizationService implementation
- SheetAnalysisOrchestrator (current usage)
