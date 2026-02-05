# Decision 012: DataRegion Cross-File Detection

**Date**: February 2026
**Status**: Proposed
**Impact**: high
**Related**: data-region-selection spec, ADR-009 (data model), ADR-010 (UI pattern)
**Summary**: When applying a DataRegion to similar files, use header-anchored detection to determine bounds per file, not fixed bounds from the source file. User confirms via preview.

## Context

When a user defines a DataRegion in File 1 and wants to apply it to similar files, the naive approach of copying the exact bounds (e.g., A1:D27) fails when files have different row counts for the same logical table.

### Problem Scenario

```
FILE 1 (Sheet A):                FILE 2 (Sheet A):
┌─────────────────────┐          ┌─────────────────────┐
│ Header: ID|Name|Amt │ row 1    │ Header: ID|Name|Amt │ row 1
│ data...             │          │ data...             │
│ data...             │          │ data...             │
│ data...             │ row 27   │ data...             │ row 21
├─────────────────────┤          │ Date|Desc|Total     │ row 22 ← DIFFERENT TABLE
│ (empty row)         │          │ data...             │
│ other table...      │          │ data...             │
└─────────────────────┘          └─────────────────────┘
```

Copying bounds A1:D27 to File 2 would capture 6 rows from a different table, corrupting the data.

### Additional Complexity

Detecting where a table ends is non-trivial:

- **Data errors**: A single cell with wrong type in a row is a data quality issue, not a table boundary
- **Homogeneous types**: When all columns are the same type (all text, all numeric), type changes can't signal boundaries
- **No gaps**: Some files have multiple tables with no empty rows between them

## Decision

### Header-Anchored Detection

When applying a region across files, **copy the identity (header pattern), not the bounds**. Each file determines its own bounds locally.

**Algorithm**:

1. **Find anchor**: Match the header row (same column names, case-insensitive, same column position)
2. **Determine extent**: Scan downward from the header, applying boundary heuristics
3. **Apply columns**: Same column range as source region
4. **Present preview**: Show detected bounds to user for confirmation/adjustment

### Boundary Detection (Three Tiers)

| Tier | Signal | Confidence | Phase |
| ---- | ------ | ---------- | ----- |
| **Definite break** | Empty row | 100% | 2 |
| **Definite break** | Row matching a known header pattern (text values matching column names from another region) | 100% | 2 |
| **Probable break** | Majority of columns change type simultaneously (>50% of columns) | High | 3 |
| **Probable break** | 3+ consecutive rows below type-match threshold | High | 3 |
| **Ambiguous** | All columns same type, no gaps, no header patterns | Low | Cannot auto-detect |

### Phase 2 Implementation (Simplified)

- Header match to find region start
- Scan down, stop at **first empty row** or **end of sheet**
- If no empty row: use source file's row count as maximum
- Show preview with detected bounds
- User confirms or adjusts on canvas

### Phase 3 Implementation (Enhanced)

- Add type-based boundary detection:
  - Per-row "compatibility score" = percentage of columns matching expected types
  - Single row below threshold (e.g., <50%) = potential boundary
  - 3+ consecutive rows below threshold = confirmed boundary
- Sliding window analysis for edge detection
- Better handling of mixed-type scenarios

### Ambiguous Cases (Not Auto-Detectable)

When all columns have the same type and no gaps exist between tables, automatic detection **cannot reliably determine the boundary**. In this case:

- System applies best-effort detection (empty rows, header patterns)
- Shows preview with uncertainty indicator
- User adjusts the boundary on the canvas (drag bottom edge)

**This is acceptable**: the canvas makes manual adjustment a single gesture. The system handles 90% of real-world files automatically; the user handles the remaining 10% with minimal effort.

## Alternatives Considered

| Approach | Pro | Contra |
| -------- | --- | ------ |
| Copy fixed bounds | Simplest | Fails when row counts differ (the core problem) |
| Full auto-detection | Handles everything | Unreliable for homogeneous data, over-engineered |
| Header-anchored + preview (chosen) | Reliable for common cases, user handles edge cases | Requires canvas for adjustment |
| Manual selection only | Always correct | Tedious with many files ("terrible UX") |

## Rationale

- **Header identity is stable**: Column names are consistent across similar files even when row counts vary
- **Progressive heuristics**: Simple rules handle common cases; complex rules added only when needed
- **User in the loop**: Preview + canvas adjustment is fast and handles all edge cases
- **Honest about limits**: We don't promise magic for genuinely ambiguous cases

## Consequences

- "Apply to similar files" becomes smarter: adapts bounds per file instead of copying blindly
- Canvas is essential (not just for defining regions, but for adjusting auto-detected ones)
- SimilarityComparisonService needs header-matching logic (Phase 2) and type-window analysis (Phase 3)
- UI must show preview of detected bounds before applying
- Phase 2 is conservative (stops at empty rows); Phase 3 handles more complex layouts
