---
type: bug
priority: high
status: resolved
discovered: 2026-03-04
related: [canvas-pointer-events-coordinate-bugs, canvas-pending-selection-double-offset]
related_decision: null
report: archive/reviews/canvas-coordinate-review.md
---

# SheetGridCanvas HitTestCell: Unclear Return Value Semantics

## Problem

HitTestCell returns display-space coordinates (row/col as they appear visually on canvas), but the method name and documentation don't make this clear. Callers are confused about coordinate spaces, leading to bugs.

**User Impact**:
- Pointer event handlers misuse return values
- Errors propagate through drag selection logic
- Same coordinate-space bugs will recur on future modifications

## Analysis

### Current Implementation

```csharp
/// <summary>
/// Convert pixel position to (row, col). Returns false if outside the data area.
/// </summary>
private bool HitTestCell(Point pos, out int row, out int col)
{
    row = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);  // Display row!
    col = 0;

    if (pos.X < RowHeaderWidth || pos.Y < ColumnHeaderHeight)
    {
        row = Math.Max(0, row);
        return false;
    }

    double x = RowHeaderWidth;
    for (int c = 0; c < _columnWidths.Length; c++)
    {
        if (pos.X < x + _columnWidths[c])
        {
            col = c;  // Display column!
            return true;
        }
        x += _columnWidths[c];
    }

    col = _columnWidths.Length - 1;
    return true;
}
```

### Semantic Issues

1. **Output parameter names too generic**: `out int row, out int col` don't indicate display-space
2. **Documentation vague**: "Convert pixel position to (row, col)" doesn't specify coordinate space
3. **Callers confused**: OnPointerMoved treats results as local-space and clamps to `[0, RowCount-1]`
4. **Inconsistent with coordinate system**: Should clearly state display-space semantics

### Example: How Callers Get Confused

```csharp
// In OnPointerMoved:
HitTestCell(pos, out int row, out int col);

// Caller thinks: "This is a local row index"
// But actually: "This is a display row index"

// Result: Wrong clamping
row = Math.Clamp(row, 0, SheetData.RowCount - 1);  // ❌ Clamps to local bounds!
```

## Possible Solutions

### Option A: Rename to Clarify Semantics
```csharp
/// <summary>
/// Convert pixel position to display-space coordinates.
/// Display space: row 0 = first Excel row (regardless of origin), col 0 = column A.
/// Returns false if in header area, but still computes displayRow/displayCol for clamping.
/// </summary>
private bool HitTestCell(Point pos, out int displayRow, out int displayCol)
{
    displayRow = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
    displayCol = 0;
    // ...
}
```
**Pros**: Clear intent, self-documenting, forces callers to be aware of coordinate space
**Cons**: Requires updates to all callers (3-4 places)

### Option B: Add Helper Method with Clear Name
```csharp
private bool HitTestCellDisplaySpace(Point pos, out int displayRow, out int displayCol)
{
    // Same implementation as before
    return HitTestCell(pos, out displayRow, out displayCol);
}

private bool HitTestCell(Point pos, out int row, out int col) // Keep old name for compatibility
{
    return HitTestCellDisplaySpace(pos, out row, out col);
}
```
**Pros**: Gradual migration, backward compatible
**Cons**: Duplication, confusion with two methods

### Option C: Add Conversion Methods
```csharp
private (int displayRow, int displayCol) HitTestCell(Point pos)
{
    int displayRow = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
    int displayCol = FindColumnAt(pos.X);
    return (displayRow, displayCol);
}

private int DisplayRowToLocal(int displayRow) => displayRow - SheetData.OriginRow;
private int LocalRowToDisplay(int localRow) => localRow + SheetData.OriginRow;
```
**Pros**: Explicit conversion methods, return tuple makes semantics clear
**Cons**: Larger refactor, multiple new methods

## Recommended Approach

**Option A** (Rename for clarity): Best balance of simplicity and clarity.

**Implementation**:

1. **Update method signature and documentation**:
```csharp
/// <summary>
/// Convert pixel position to display-space coordinates.
///
/// Display space: Uses visual coordinates where row 0 = first Excel row (regardless of OriginRow),
/// and col 0 = column A (regardless of OriginColumn).
///
/// Returns false if pixel is in header area (row headers or column headers), but still
/// computes displayRow/displayCol for use in clamping or boundary calculations.
/// </summary>
private bool HitTestCell(Point pos, out int displayRow, out int displayCol)
{
    displayRow = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
    displayCol = 0;

    if (pos.X < RowHeaderWidth || pos.Y < ColumnHeaderHeight)
    {
        displayRow = Math.Max(0, displayRow);
        return false;
    }

    double x = RowHeaderWidth;
    for (int c = 0; c < _columnWidths.Length; c++)
    {
        if (pos.X < x + _columnWidths[c])
        {
            displayCol = c;
            return true;
        }
        x += _columnWidths[c];
    }

    displayCol = _columnWidths.Length - 1;
    return true;
}
```

2. **Update all callers**:
```csharp
// OnPointerPressed:
if (!HitTestCell(pos, out int displayRow, out int displayCol)) return;
_dragStartRow = displayRow;      // Now clear that it's display space
_dragStartCol = displayCol;

// OnPointerMoved:
HitTestCell(pos, out int displayRow, out int displayCol);
displayRow = Math.Clamp(displayRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
displayCol = Math.Clamp(displayCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
// ... rest of logic treats these as display-space
```

3. **Add class-level documentation**:
```csharp
/// <summary>
/// Custom-rendered spreadsheet grid for visualizing sheet data and selecting regions.
///
/// **Coordinate Systems**:
/// - **Display Space**: Visual row/col as rendered on canvas. Row 0 = first Excel row.
///   Column 0 = column A. This is what HitTestCell returns.
/// - **Local Space**: 0-based indices in SASheetData flat array. Accounts for origin offsets.
///   row 0 = SASheetData[0], not necessarily Excel row 0.
///
/// **Key Conversions**:
/// - `displayRow = localRow + sheet.OriginRow`
/// - `localRow = displayRow - sheet.OriginRow`
///
/// Pointer events (HitTestCell, drag handlers) work in display space.
/// DataRegion boundaries are in local space (per ADR-009).
/// </summary>
public class SheetGridCanvas : Control
{
    // ... rest of class ...
}
```

## Notes

- **Scope**: Relatively small change, affects 3-4 callers
- **Risk**: LOW — renaming improves clarity without changing logic
- **Testing**: Should verify coordinate conversions with origin-based scenarios
- **Long-term**: Consider adding explicit conversion helper methods

## Related Documentation

- **Code Review Report**: `/data/repos/sheet-atlas/.development/archive/reviews/canvas-coordinate-review.md`
- **Architecture Decision**: ADR-002 (Row Indexing Semantics), ADR-009 (DataRegion Data Model)
- **Related Bugs**:
  - canvas-pointer-events-coordinate-bugs (root cause of coordinate confusion)
  - canvas-pending-selection-double-offset (related coordinate space issues)
- **File**: `src/SheetAtlas.UI.Avalonia/Controls/SheetGridCanvas.cs:669-694`

---

## Verification Steps

1. Rename HitTestCell parameters to displayRow, displayCol
2. Update all callers to use new parameter names
3. Add class-level coordinate system documentation
4. Run existing tests (if any) to verify no regression
5. Add new test cases for coordinate conversion correctness

---

📍 **Investigation Note**: This is a critical clarity issue that prevents future developers from understanding the code correctly. Recommend addressing as part of coordinate system review.
