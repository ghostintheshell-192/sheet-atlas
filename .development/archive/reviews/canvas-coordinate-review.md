# SheetGridCanvas Coordinate System Review

**Date**: 2026-03-04
**Reviewer**: Code Review Agent
**Component**: SheetGridCanvas.cs
**Scope**: Coordinate system correctness (display ↔ local conversion, Origin handling)

---

## Executive Summary

The SheetGridCanvas coordinate system has **4 critical bugs** and **1 architectural concern** that will cause:
- Data selection in wrong cells when origin > 0
- Region boundary calculations failing
- User selection persisting incorrectly
- Resize operations pointing to wrong rows

**Overall Assessment**: HIGH-SEVERITY coordinate system errors. The canvas attempts to handle origins but has fundamental conversion bugs in mouse input handling and region boundary calculations.

---

## Coordinate System Design (Reference)

### Definitions

From code review and ADR-002/ADR-009:

| Term | Meaning | Example |
|------|---------|---------|
| **Local space** | 0-based indices in SASheetData flat array | `GetCellValue(0, 0)` = first cell |
| **Display space** | 0-based column = Excel column (0=A, 1=B), row = Excel row 0-based (0=row1) | Cell at display (3, 5) = E4 in Excel |
| **OriginRow** | Excel row of local row 0 | OriginRow=2 means data starts at Excel row 3 |
| **OriginColumn** | Excel column of local col 0 | OriginColumn=1 means data starts at column B |

### Conversion Formulas

**Display → Local**:
```csharp
localRow = displayRow - sheet.OriginRow
localCol = displayCol - sheet.OriginColumn
```

**Local → Display**:
```csharp
displayRow = localRow + sheet.OriginRow
displayCol = localCol + sheet.OriginColumn
```

---

## Critical Issues

### CRITICAL BUG #1: OnPointerMoved Line 584-585 — Display-Space Coords Clamped to Local Bounds

**Location**: `SheetGridCanvas.cs:584-585`

**Severity**: CRITICAL

**Problem**:

```csharp
if (_isDragging && SheetData != null)
{
    HitTestCell(pos, out int row, out int col);

    // ❌ BUG: row, col are in DISPLAY space from HitTestCell
    // But we clamp to LOCAL space bounds!
    row = Math.Clamp(row, 0, SheetData.RowCount - 1);          // LOCAL bound
    col = Math.Clamp(col, 0, SheetData.ColumnCount - 1);       // LOCAL bound

    // ...
    _dragCurrentRow = row;  // Stored as DISPLAY space
    _dragCurrentCol = col;
}
```

**Root Cause**: `HitTestCell()` returns display-space coordinates (pixel position → row/col in display system). But the clamp uses local-space bounds (`0..RowCount-1`), mixing two coordinate systems.

**Impact**:
- When `OriginRow > 0`, clamping fails. A mouse drag starting at display row 10 gets clamped to `0..RowCount-1`, which is wrong.
- Example: OriginRow=5, RowCount=10. Display rows are 5-14. User drags in display row 12 → HitTestCell returns 12 → Clamped to `0..9` (local bounds) → becomes local row 9 → stored as display row 9. Wrong!
- Drag coordinates become corrupted, selecting wrong rows.

**Expected Behavior**:
```csharp
// HitTestCell returns display-space row/col
HitTestCell(pos, out int displayRow, out int displayCol);

// Clamp to DISPLAY space bounds
int minDisplayRow = sheet.OriginRow;
int maxDisplayRow = sheet.OriginRow + sheet.RowCount - 1;
displayRow = Math.Clamp(displayRow, minDisplayRow, maxDisplayRow);

int minDisplayCol = sheet.OriginColumn;
int maxDisplayCol = sheet.OriginColumn + sheet.ColumnCount - 1;
displayCol = Math.Clamp(displayCol, minDisplayCol, maxDisplayCol);

_dragCurrentRow = displayRow;  // Store display coords
_dragCurrentCol = displayCol;
```

**Recommended Fix**: Rename variables in OnPointerMoved to clearly separate display vs. local spaces:
```csharp
if (_isDragging && SheetData != null)
{
    HitTestCell(pos, out int displayRow, out int displayCol);  // Returns DISPLAY space

    // Clamp to data area in DISPLAY space
    displayRow = Math.Clamp(displayRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
    displayCol = Math.Clamp(displayCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);

    if (displayRow != _dragCurrentRow || displayCol != _dragCurrentCol)
    {
        _dragCurrentRow = displayRow;
        _dragCurrentCol = displayCol;
        InvalidateVisual();
    }
}
```

---

### CRITICAL BUG #2: OnPointerPressed Line 546-548 — Same Issue

**Location**: `SheetGridCanvas.cs:546-548`

**Severity**: CRITICAL

**Problem**:

```csharp
if (!HitTestCell(pos, out int row, out int col)) return;

_isDragging = true;
_dragStartRow = row;     // ❌ row is DISPLAY space, no origin offset
_dragStartCol = col;     // ❌ col is DISPLAY space
```

**Root Cause**: Same as #1. `HitTestCell()` returns display-space coords, but they're stored directly without checking whether they're already in display space.

**Impact**:
- Drag selection starts at wrong cell when origin > 0.
- When user clicks at display position (OriginColumn + 5), HitTestCell returns display column OriginColumn+5, but this is already adjusted for origin.

**Note**: This might work accidentally when OriginRow/OriginColumn = 0, but fails when they're non-zero.

**Recommended Fix**: Similar to #1 — ensure display-space coordinates are handled consistently.

---

### CRITICAL BUG #3: RenderPendingSelection Line 471 — Double-Adding OriginRow

**Location**: `SheetGridCanvas.cs:471`

**Severity**: CRITICAL

**Problem**:

```csharp
private void RenderPendingSelection(DrawingContext context, SASheetData sheet)
{
    var selection = SelectionRegion;
    if (selection == null || _isDragging) return;

    // DataRegion coordinates are LOCAL (ADR-009)
    int startRow = (selection.HeaderStartRow ?? selection.DataStartRow) + sheet.OriginRow;
    int endRow = (selection.DataEndRow ?? startRow) + sheet.OriginRow;  // ❌ BUG HERE
    //                                      ↑ startRow ALREADY HAS OriginRow ADDED
```

**Root Cause**: Line 470 calculates `startRow = ... + sheet.OriginRow` (converting local to display). Line 471 then uses `startRow` as the default for `endRow`, and adds `sheet.OriginRow` again.

**Impact**:
- When `DataEndRow` is null and defaults to `startRow`, endRow gets `OriginRow` added twice.
- Pending selection renders at the wrong vertical position.
- Example: OriginRow=5, HeaderStartRow=0, DataStartRow=1
  - startRow = 0 + 5 = 5 (display row)
  - endRow = (null → 5) + 5 = 10 ❌ Should be 5, not 10

**Expected Behavior**:
```csharp
int startRow = (selection.HeaderStartRow ?? selection.DataStartRow) + sheet.OriginRow;
int endRow = (selection.DataEndRow ?? (selection.DataStartRow)) + sheet.OriginRow;
//                                   ↑ Default to DataStartRow in LOCAL space, then convert once
```

Or cleaner:

```csharp
int localStartRow = selection.HeaderStartRow ?? selection.DataStartRow;
int localEndRow = selection.DataEndRow ?? localStartRow;  // Both in LOCAL space
int displayStartRow = localStartRow + sheet.OriginRow;
int displayEndRow = localEndRow + sheet.OriginRow;
```

---

### CRITICAL BUG #4: OnPointerReleased Lines 632-635 — Inconsistent Coordinate Spaces

**Location**: `SheetGridCanvas.cs:632-635`

**Severity**: CRITICAL

**Problem**:

```csharp
// Drag coords are in display space; clamp to data area then convert to local
int minDisplayRow = Math.Clamp(Math.Min(_dragStartRow, _dragCurrentRow),
    sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
int maxDisplayRow = Math.Clamp(Math.Max(_dragStartRow, _dragCurrentRow),
    sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
```

**Issue**: The comment says "Drag coords are in display space", which is CORRECT. But looking back at lines 584-585 (BUG #1), `_dragCurrentRow` is NOT always in display space — it's clamped using local bounds, making it corrupted.

This function tries to recover, but because the drag coords are already corrupted from #1, the fix here can't work correctly.

**Root Cause**: Cascading from BUG #1. The drag state variables store corrupted values.

**Impact**:
- Even if the fix here is correct, it's operating on bad data.
- Selection region boundaries will be off.

**Recommended Fix**: Fix BUG #1 first so `_dragCurrentRow/Col` are consistently in display space, then this logic should work.

---

## High-Priority Issues

### HIGH BUG #5: HitTestCell Return Value Semantics Unclear

**Location**: `SheetGridCanvas.cs:669-694`

**Severity**: HIGH

**Problem**:

```csharp
private bool HitTestCell(Point pos, out int row, out int col)
{
    row = (int)((pos.Y - ColumnHeaderHeight) / CellHeight);
    col = 0;

    // ... search for column index ...

    col = c;
    return true;  // ✅ Clearly returns display space
}
```

**Root Cause**: The function correctly computes display-space row/col, but:
1. Variable names don't indicate this (should be `displayRow`, `displayCol`)
2. Callers don't always treat them as display space (see BUG #1)
3. The comment "Convert pixel position to (row, col)" doesn't specify which coordinate space

**Impact**:
- Callers confused about coordinate system
- Errors propagate through pointer event handlers
- Same bug likely to recur on future modifications

**Recommended Fix**:

```csharp
/// <summary>
/// Convert pixel position to display-space row/col (0-based).
/// Display space: row 0 = first Excel row regardless of origin, col 0 = column A.
/// Returns false if in header area, but still computes row/col for clamping.
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

    // ... search for column index ...
    displayCol = c;
    return true;
}
```

---

### HIGH BUG #6: RenderDragSelection Clamping is Correct but Confusing

**Location**: `SheetGridCanvas.cs:488-513`

**Severity**: HIGH (Code smell, not a correctness bug)

**Problem**:

```csharp
private void RenderDragSelection(DrawingContext context, SASheetData sheet)
{
    if (!_isDragging) return;

    int minRow = Math.Min(_dragStartRow, _dragCurrentRow);
    int maxRow = Math.Max(_dragStartRow, _dragCurrentRow);
    int minCol = Math.Min(_dragStartCol, _dragCurrentCol);
    int maxCol = Math.Max(_dragStartCol, _dragCurrentCol);

    // Clamp to data area (drag cannot go into empty origin area)
    minRow = Math.Clamp(minRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
    maxRow = Math.Clamp(maxRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
    minCol = Math.Clamp(minCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
    maxCol = Math.Clamp(maxCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
```

**Issue**: The code assumes `_dragCurrentRow/Col` are in display space (line 498 clamps to `sheet.OriginRow..sheet.OriginRow+sheet.RowCount-1`). But due to BUG #1, they might not be!

However, if we ignore BUG #1, this logic is CORRECT. It's treating them as display-space coordinates and clamping to the data bounds.

**Root Cause**: Inconsistent coordinate space handling across the class.

**Impact**: Works by accident if OriginRow/OriginColumn happen to be 0, but fails otherwise.

---

## Medium-Priority Issues

### MEDIUM #7: RenderRegionOverlays and RenderPendingSelection Have Same Pattern as Bug #3

**Location**: `SheetGridCanvas.cs:439-458, 464-484`

**Severity**: MEDIUM

**Problem**:

```csharp
private void RenderRegionOverlays(DrawingContext context, SASheetData sheet)
{
    var activeRegion = ActiveRegion;
    if (activeRegion == null) return;

    // DataRegion coordinates are local (SASheetData-space); add origin for display position
    int startRow = (activeRegion.HeaderStartRow ?? activeRegion.DataStartRow) + sheet.OriginRow;
    int endRow = (activeRegion.DataEndRow ?? (sheet.RowCount - 1)) + sheet.OriginRow;
    //                                      ↑ Using sheet.RowCount (LOCAL bound) without origin
```

**Issue**: Line 446 uses `sheet.RowCount - 1` as default, which is a LOCAL space index. Should convert to local first, then add origin.

**Root Cause**: Similar to BUG #3 — mixing local and display spaces in ternary expressions.

**Impact**:
- Region overlay renders at wrong Y position when `DataEndRow` is null and origin > 0
- Example: OriginRow=5, RowCount=20, DataEndRow=null
  - endRow = (null → 19) + 5 = 24 ❌
  - Should be: endRow = 19 + 5 = 24... wait, this IS correct!

**Wait, let me re-analyze**:
- `sheet.RowCount` is the LOCAL count (includes header rows)
- If we want "till end of data", we should use `sheet.RowCount - 1` as LOCAL index
- Then convert: `(sheet.RowCount - 1) + sheet.OriginRow` = display end row ✅

Actually, this IS correct! The formula is right.

But for consistency with RenderPendingSelection:

```csharp
// Better to be explicit:
int localEndRow = activeRegion.DataEndRow ?? (sheet.RowCount - 1);
int displayEndRow = localEndRow + sheet.OriginRow;
```

**Recommendation**: Not a bug, but improve readability by splitting local/display calculations.

---

## Architectural Observations

### Finding #1: No Validation that Drag Coordinates Start in Data Area

**Location**: `OnPointerPressed` lines 536-543

**Issue**: User can click in empty origin area and drag. Should validate that initial click is within data bounds.

```csharp
if (!HitTestCell(pos, out int row, out int col)) return;
// HitTestCell returns false for headers, but doesn't check against OriginRow/OriginColumn!
```

**Current state**: HitTestCell returns false only for header area (Y < ColumnHeaderHeight, X < RowHeaderWidth), not for cells before origin.

**Impact**: Might allow drag selection to start in origin cells, creating regions with invalid coordinates.

---

### Finding #2: Resize Logic Appears Correct But Underdocumented

**Location**: `OnPointerMoved` lines 562-577

**Assessment**: ✅ The resize logic correctly:
1. Computes `newDisplayRow` from pixel position (display space)
2. Clamps to `minDisplayRow..maxDisplayRow` (display space)
3. Converts to local via `newRow = newDisplayRow - sheet.OriginRow`
4. Updates region with local `DataEndRow`

This is one of the few correct coordinate transformations in the file.

---

## Summary Table

| Issue | Type | Location | Severity | Impact | Status |
|-------|------|----------|----------|--------|--------|
| Display coords clamped to local bounds | Bug | OnPointerMoved:584-585 | CRITICAL | Wrong drag selection with origin > 0 | Not fixed |
| Same issue in OnPointerPressed | Bug | OnPointerPressed:546-548 | CRITICAL | Drag selection starts at wrong cell | Not fixed |
| Double-adding OriginRow in pending selection | Bug | RenderPendingSelection:471 | CRITICAL | Selection border renders wrong | Not fixed |
| Corrupted drag coords passed to OnPointerReleased | Bug | OnPointerReleased:632-635 | CRITICAL | Selection region boundaries off | Not fixed |
| HitTestCell return value semantics unclear | Bug | HitTestCell:669-694 | HIGH | Confusion in caller responsibilities | Not fixed |
| RenderDragSelection depends on buggy drag coords | Bug | RenderDragSelection:488-513 | HIGH | Visible feedback wrong | Not fixed |
| RenderRegionOverlays clarity issue | Code smell | RenderRegionOverlays:446 | MEDIUM | Maintenance risk | Minor issue |
| No validation of origin boundaries | Enhancement | OnPointerPressed:536 | MEDIUM | Might allow invalid regions | Not implemented |

---

## Testing Recommendations

### Test Case 1: Non-Zero Origin Selection

```csharp
[Fact]
public void DragSelection_WithOriginOffset_SelectsCorrectCells()
{
    // Arrange
    var sheet = new SASheetData("Test", new[] { "A", "B", "C" });
    sheet.SetOrigin(originRow: 5, originColumn: 2);  // Data starts at R6C, col C

    // Add some rows
    for (int i = 0; i < 20; i++)
        sheet.AddRow(new[] { new SACellData(...), ... });

    // Act: Drag from display cell (7, 3) to (10, 5)
    // That's local cells (2, 1) to (5, 3)
    var selection = canvas.SimulateDrag(
        startDisplayRow: 7, startDisplayCol: 3,
        endDisplayRow: 10, endDisplayCol: 5);

    // Assert
    Assert.Equal(2, selection.DataStartRow);  // Local
    Assert.Equal(5, selection.DataEndRow);     // Local
    Assert.Equal(1, selection.StartColumn);    // Local
    Assert.Equal(3, selection.EndColumn);      // Local
}
```

### Test Case 2: Pending Selection Renders at Correct Position

```csharp
[Fact]
public void PendingSelectionRendering_WithOriginOffset_PositionCorrect()
{
    // Arrange
    var sheet = CreateSheetWithOrigin(originRow: 3, originColumn: 1);
    var selection = new DataRegion
    {
        Name = "Test",
        HeaderStartRow = 0,
        DataStartRow = 1,
        DataEndRow = null  // Till end
    };

    // Act: Render with null DataEndRow
    var bounds = canvas.GetPendingSelectionBounds(sheet, selection);

    // Assert: Should render from display row 3 (local 0 + origin 3)
    Assert.Equal(3, bounds.Top);
    // Should render till display row (19 + 3) = 22, not (19 + 3 + 3) = 25
    Assert.Equal(22, bounds.Bottom);
}
```

---

## Recommended Fix Priority

**Phase 1 (CRITICAL — Day 1)**:
1. Fix BUG #1: OnPointerMoved display/local clamping
2. Fix BUG #2: OnPointerPressed display/local consistency
3. Fix BUG #3: RenderPendingSelection double-add OriginRow

**Phase 2 (HIGH — Day 2)**:
4. Fix BUG #4: OnPointerReleased coordinate space documentation
5. Rename HitTestCell parameters to clarify display space
6. Add test cases for origin-based scenarios

**Phase 3 (MEDIUM — Week 1)**:
7. Improve RenderRegionOverlays/RenderPendingSelection clarity
8. Add origin boundary validation
9. Comprehensive coordinate system documentation

---

## Questions for Discussion

1. **Intended Behavior with Origin**: Is the origin feature meant to show empty cells before the data starts? Or should empty cells be hidden?
   - Current: Canvas always renders from display row 0 (empty before origin)
   - This is correct per code comments, but worth confirming

2. **Coordinate Space Naming**: Should we rename internal variables to be explicit?
   - `row` → `displayRow` in HitTestCell
   - `_dragStartRow` → `_dragStartDisplayRow` for clarity

3. **Testing Strategy**: Should we add unit tests for SheetGridCanvas coordinate transformations?
   - No tests currently exist
   - Recommend at least 3-4 tests for origin-based scenarios

---

## References

- ADR-002: Row Indexing Semantics
- ADR-009: DataRegion Data Model
- SASheetData.cs: OriginRow/OriginColumn definitions
- DataRegion.cs: Local coordinate space semantics

---

*End of review*
