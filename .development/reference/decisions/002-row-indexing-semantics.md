# Decision 002: Row Indexing Semantics

**Date**: October 2025
**Status**: Active

## Context

Needed consistent row numbering across all components (internal data structures, search results, UI display).

## Decision

**ABSOLUTE 0-based indexing** internally, **1-based** for display.

| Context | Format | Example |
|---------|--------|---------|
| Internal (SASheetData, SearchResult.Row) | 0-based absolute | Row 0 = first row of sheet |
| Display (UI) | 1-based absolute | "R1" = first row (matches Excel) |
| Header detection | `row < HeaderRowCount` | Default: row 0 is header |

## Key Rules

- `SearchResult.Row` = absolute 0-based index (same as SASheetData.GetRow())
- Search skips header rows (starts from `HeaderRowCount`)
- Row comparison validates `Row >= HeaderRowCount` (only data rows)
- Display conversion: `displayRow = internalRow + 1`

## Header Support

- Currently: single header row (`HeaderRowCount = 1`)
- Architecture supports multi-row headers (`HeaderRowCount` can be 2, 3, etc.)
- All readers (XLSX, XLS, CSV) use the same semantics via SASheetData

## Rationale

- 0-based internally = natural for C# arrays/lists
- 1-based display = matches Excel UI expectations
- Centralized conversion avoids off-by-one errors

## Consequences

- All internal code uses 0-based
- Only UI layer converts to 1-based
- Must be careful when comparing internal indexes with user-visible row numbers
