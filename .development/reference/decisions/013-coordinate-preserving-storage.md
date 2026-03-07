# Decision 013: Coordinate-Preserving Sheet Storage

**Date**: March 2026
**Status**: Proposed
**Impact**: critical
**Related**: ADR-002 (row indexing), ADR-009 (DataRegion data model)
**Summary**: SASheetData stores OriginRow/OriginColumn to preserve original Excel coordinates. Flat array remains 0-based sequential; coordinates are always reconstructible via offset addition.

## Context

SASheetData stores cell data in a flat row-major array with 0-based sequential indexing. During file reading, the reader normalizes coordinates: if Excel data starts at row 3, column B, SASheetData maps it to [0,0]. The original position information (`firstRowOffset`, `firstCol`) is used during parsing but discarded afterward.

This causes concrete problems:

1. **Canvas display mismatch**: SheetGridCanvas shows column "A" and row "1" for data that actually starts at column B, row 3 in Excel. Users see different coordinates than what their spreadsheet application shows.
2. **DataRegion misalignment**: When users define regions by selecting areas in the canvas, the coordinates don't match the actual Excel positions, leading to shifted headers and misaligned data.
3. **Information loss**: Once loaded, there is no way to reconstruct which Excel cell a given SASheetData cell originally came from.

## Decision

### Add OriginRow and OriginColumn to SASheetData

Two integer properties record the absolute Excel position (0-based) of the cell stored at local index [0,0]:

```csharp
public int OriginRow { get; private set; }
public int OriginColumn { get; private set; }

public void SetOrigin(int originRow, int originColumn);
```

### Coordinate Translation Methods

```csharp
// Local (0-based array) → Excel absolute (0-based)
public int ToExcelRow(int localRow) => localRow + OriginRow;
public int ToExcelColumn(int localCol) => localCol + OriginColumn;

// Excel absolute (0-based) → Local (0-based array)
public int ToLocalRow(int excelRow) => excelRow - OriginRow;
public int ToLocalColumn(int excelCol) => excelCol - OriginColumn;

// Human-readable Excel reference (e.g. "B5")
public string GetCellReference(int localRow, int localCol);
```

### What Does NOT Change

- **Flat array storage**: Same row-major `SACellData[]` with `index = row * ColumnCount + column`
- **0-based local indexing**: All internal code continues using local indices
- **HeaderRowCount semantics**: Still relative to local row 0
- **DataRegion coordinates**: Still use local indices (relative to SASheetData)
- **Existing service code**: SearchService, RowComparisonService, etc. operate on local indices — no changes needed

### Reader Responsibilities

Each reader sets origin coordinates after constructing SASheetData:

| Reader | OriginRow | OriginColumn |
| ------ | --------- | ------------ |
| OpenXmlFileReader | `firstRowOffset` (already calculated) | `headerColumns.Keys.Min()` (already calculated) |
| XlsFileReader | First non-empty row index | First non-empty column index |
| CsvFileReader | 0 | 0 |

## Alternatives Considered

| Approach | Pro | Contra |
| -------- | --- | ------ |
| **Flat array + origin offset** (chosen) | Zero overhead, additive change, preserves all performance | Requires translation for display |
| **Dictionary<string, SACellData>** keyed by cell reference | No offset math, natural Excel addressing | ~40 bytes overhead per cell, poor iteration locality, O(n log n) row enumeration |
| **CSR (Compressed Sparse Row)** | Industry standard for sparse matrices, excellent iteration + lookup | Over-engineered for dense rectangular Excel data; complex insert/modify |
| **Absolute indexing everywhere** | No translation needed | Massive refactor touching ~40 files, breaks all existing index assumptions |

## Rationale

The flat array is already an excellent structure for our workload (sequential iteration, row comparison, memory locality). The only missing piece is the coordinate offset — 8 bytes per sheet. This is the minimum viable change that solves the problem completely.

## Consequences

- Excel coordinates are always reconstructible from any SASheetData cell
- Canvas can display correct column letters and row numbers
- DataRegions can be visually validated against the original spreadsheet
- No performance impact (two integer additions per coordinate translation)
- Readers must set origin during construction (enforced by convention, not type system)
- Future: if DataRegion coordinates need to be stored as absolute Excel coordinates (e.g. in persistence), the translation methods provide the conversion
