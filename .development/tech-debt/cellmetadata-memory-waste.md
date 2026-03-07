---
type: performance
priority: medium
status: open
discovered: 2025-11-08
related: []
---

# CellMetadata Memory Waste for NumberFormat Storage

## Problem

Currently allocating entire `CellMetadata` object (~96 bytes) just to store `NumberFormat` string for foundation services analysis.

**Memory Impact:**

- `CellMetadata` is a class with ~10 reference fields (OriginalValue, CleanedValue, Formula, Style, Validation, etc.)
- Object header: 16 bytes + 10 fields × 8 bytes = 96 bytes total
- Formatted cells in typical sheet: 30-70%
- **Example**: 100K formatted cells → ~9.6 MB wasted on mostly-empty objects

## Analysis

### Current Implementation

```csharp
// In CreateRowData()
if (numberFormat != null)
{
    metadata = new CellMetadata { NumberFormat = numberFormat };
    // Only 1 property used, but entire object allocated!
}
```

### Design Issue

`CellMetadata` was intended for **exceptional cases** (5-10% of cells: formulas, validation errors, quality issues), not for structural data present in majority of cells.

`NumberFormat` is needed only:

- During `EnrichSheetWithColumnAnalysis` (immediately after file load)
- Never again (unless re-analysis)

## Possible Solutions

- **Option 1**: Temporary structure (Best memory efficiency)
  - Extract numberFormats during `CreateRowData` into temporary `Dictionary<int, List<string?>>`
  - Pass to `EnrichSheetWithColumnAnalysis` as parameter
  - GC deallocates after enrichment completes
  - **Impact**: ~24 bytes/entry vs 96, deallocated after use

- **Option 2**: Separate dictionary in SASheetData
  - `private Dictionary<(int row, int col), string>? _numberFormats`
  - Lazy-loaded, queryable, clearable after enrichment
  - **Impact**: ~24 bytes/entry, persistent but optional

- **Option 3**: Keep current (Pragmatic)
  - Memory waste is real but not critical for typical file sizes
  - Working implementation, defer optimization until profiling shows bottleneck

## Recommended Approach

Evaluate memory usage with real-world files. If profiling shows >50MB waste on typical workloads, refactor to Option 1 (temporary structure). Otherwise, document and defer.

**Requires**: Memory profiling first to quantify actual impact

## Notes

- Similar pattern might apply to other temporary analysis data
- Foundation Layer integration context
- Not urgent - needs data-driven decision (profiling)

## Status Verification (2025-11-28)

**Verified by performance-profiler agent**: Issue is **STILL PRESENT**.

**Current code** (OpenXmlFileReader.cs:418-423):

```csharp
if (numberFormat != null) {
    metadata = new CellMetadata { NumberFormat = numberFormat };
}
```

**Key findings**:

- Memory waste confirmed: ~96 bytes per formatted cell
- Typical impact: 4-8MB on 100K-cell sheet with 50% formatted cells
- NumberFormat only used during enrichment phase, then discarded
- Issue is REAL but LOWER priority than originally assessed

**Recommendation**: Keep OPEN as optimization opportunity, but low priority. Memory deallocates after enrichment completes (GC eligible). Optimize only if profiling shows >50MB waste in production workloads.

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
