---
type: bug
priority: high
status: resolved
discovered: 2026-03-04
related: [canvas-pointer-events-coordinate-bugs]
related_decision: null
report: archive/reviews/canvas-coordinate-review.md
---

# SheetGridCanvas RenderPendingSelection: Double OriginRow Addition

## Problem

When DataEndRow is null in a DataRegion, RenderPendingSelection renders the selection border at the wrong vertical position by adding OriginRow twice.

**User Impact**:
- Pending selection (dashed border) appears offset below actual selected area
- Only manifests when creating a region and DataEndRow is null (region extends to sheet end)
- Confuses users about actual region boundary

## Analysis

### Root Cause

Line 471 in RenderPendingSelection:

```csharp
int startRow = (selection.HeaderStartRow ?? selection.DataStartRow) + sheet.OriginRow;
int endRow = (selection.DataEndRow ?? startRow) + sheet.OriginRow;
//                             ↑ startRow ALREADY HAS OriginRow ADDED!
```

When DataEndRow is null:
1. Defaults to `startRow` (which is already `localValue + OriginRow`)
2. Then adds `sheet.OriginRow` again
3. Result: `endRow = startRow + OriginRow = (local + OriginRow) + OriginRow`

### Example

**Scenario**: OriginRow=5, HeaderStartRow=0, DataStartRow=1, DataEndRow=null

**Buggy calculation**:
```
startRow = (0 ?? 1) + 5 = 0 + 5 = 5  (display row, correct)
endRow = (null ?? 5) + 5 = 5 + 5 = 10  (display row, WRONG!)
Actual last data row should be at display row ~24, not 10
```

**Expected calculation**:
```
localEndRow = null ?? (sheet.RowCount - 1)  // In local space
displayEndRow = localEndRow + sheet.OriginRow  // Convert once
```

### Affected Code Location

```
SheetGridCanvas.cs:464-484 (RenderPendingSelection method)
  Specifically: Line 471
```

### Side Note on RenderRegionOverlays

Line 446 has a similar pattern:
```csharp
int endRow = (activeRegion.DataEndRow ?? (sheet.RowCount - 1)) + sheet.OriginRow;
```

However, this one is CORRECT because `sheet.RowCount - 1` is already in local space. The default to `sheet.RowCount - 1` is the last row index (local), then it's converted to display by adding OriginRow once.

The difference: RenderPendingSelection defaults to `startRow` (which is display-space already), while RenderRegionOverlays defaults to `sheet.RowCount - 1` (which is local-space). This inconsistency is the root of the bug.

## Possible Solutions

### Option A: Clear Local/Display Separation
```csharp
int localStartRow = selection.HeaderStartRow ?? selection.DataStartRow;
int localEndRow = selection.DataEndRow ?? (sheet.RowCount - 1);
int displayStartRow = localStartRow + sheet.OriginRow;
int displayEndRow = localEndRow + sheet.OriginRow;
```
**Pros**: Crystal clear, easy to verify correctness, matches RenderRegionOverlays style
**Cons**: More lines of code

### Option B: Fix Ternary Expression
```csharp
int startRow = (selection.HeaderStartRow ?? selection.DataStartRow) + sheet.OriginRow;
int localEndRow = selection.DataEndRow ?? (sheet.RowCount - 1);
int endRow = localEndRow + sheet.OriginRow;
```
**Pros**: Minimal change, consistent with line 446 in RenderRegionOverlays
**Cons**: Still mixes patterns a bit

### Option C: Use Helper Method
```csharp
private int LocalToDisplay(int localRow, SASheetData sheet)
    => localRow + sheet.OriginRow;

// Usage:
int startRow = LocalToDisplay(
    selection.HeaderStartRow ?? selection.DataStartRow,
    sheet);
int endRow = LocalToDisplay(
    selection.DataEndRow ?? (sheet.RowCount - 1),
    sheet);
```
**Pros**: Explicit conversion, reusable, self-documenting
**Cons**: Additional method, might be overkill for 2 uses

## Recommended Approach

**Option A** (Clear separation): Best for code clarity and future maintenance.

**Implementation**:

```csharp
private void RenderPendingSelection(DrawingContext context, SASheetData sheet)
{
    var selection = SelectionRegion;
    if (selection == null || _isDragging) return;

    // All calculations in local space first
    int localStartRow = selection.HeaderStartRow ?? selection.DataStartRow;
    int localEndRow = selection.DataEndRow ?? (sheet.RowCount - 1);
    int localStartCol = selection.StartColumn ?? 0;
    int localEndCol = selection.EndColumn ?? (sheet.ColumnCount - 1);

    // Convert to display space once
    int displayStartRow = localStartRow + sheet.OriginRow;
    int displayEndRow = localEndRow + sheet.OriginRow;
    int displayStartCol = localStartCol + sheet.OriginColumn;
    int displayEndCol = localEndCol + sheet.OriginColumn;

    // Rest of rendering uses display coordinates
    double x = GetColumnX(displayStartCol);
    double y = ColumnHeaderHeight + displayStartRow * CellHeight;
    double width = GetColumnX(displayEndCol) + (displayEndCol < _columnWidths.Length ? _columnWidths[displayEndCol] : 0) - x;
    double height = (displayEndRow - displayStartRow + 1) * CellHeight;

    // ... draw rectangle ...
}
```

## Notes

- **Severity**: HIGH but only affects visual feedback, not data integrity
- **Regression risk**: LOW — fix is straightforward
- **Testing**: No existing unit tests for RenderPendingSelection
- **Related issue**: canvas-pointer-events-coordinate-bugs (broader coordinate space confusion)

## Related Documentation

- **Code Review Report**: `/data/repos/sheet-atlas/.development/archive/reviews/canvas-coordinate-review.md`
- **Architecture Decision**: ADR-009 (DataRegion Data Model)
- **Related Bug**: canvas-pointer-events-coordinate-bugs (coordinate space confusion throughout class)
- **File**: `src/SheetAtlas.UI.Avalonia/Controls/SheetGridCanvas.cs:464-484`

---

## Verification Steps

1. Create a region with null DataEndRow
2. Verify pending selection border aligns with actual selected area
3. Test with various origin values (0, 5, 10)
4. Verify column boundaries also render correctly

---

📍 **Investigation Note**: This is a symptom of broader coordinate space issues in SheetGridCanvas. Recommend fixing as part of comprehensive coordinate system review.
