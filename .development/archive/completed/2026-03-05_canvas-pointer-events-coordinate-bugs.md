---
type: bug
priority: high
status: resolved
discovered: 2026-03-04
related: []
related_decision: null
report: archive/reviews/canvas-coordinate-review.md
---

# SheetGridCanvas Pointer Events: Coordinate System Bugs

## Problem

When OriginRow or OriginColumn > 0, drag selection targets wrong cells because pointer event handlers mix display-space and local-space coordinates.

**User Impact**:
- Selecting cells via drag puts selection boundaries at wrong rows/columns
- Happens only when data starts at non-zero Excel position (origin > 0)
- Most visible with sheets that skip rows/columns

## Analysis

### Root Causes

1. **OnPointerMoved (lines 584-585)**: `HitTestCell()` returns display-space row/col, but code clamps to local bounds (`0..RowCount-1`), corrupting the coordinate space.

2. **OnPointerPressed (lines 546-548)**: Same issue — drag start coords are display-space but treated as local, causing incorrect initial selection position.

3. **Coordinate Space Confusion**: Throughout the class, coordinate spaces aren't clearly named, leading to mixing without realizing it.

### Affected Code Locations

```
SheetGridCanvas.cs:
  Line 546-548: OnPointerPressed drag initialization
  Line 584-585: OnPointerMoved drag update
  Line 632-635: OnPointerReleased boundary conversion
  Line 669-694: HitTestCell (returns display space but not clearly documented)
```

### Example Bug Scenario

**Setup**: Sheet with OriginRow=5 (data starts at Excel row 6), RowCount=10
- Display rows: 5-14 represent data rows 0-9
- User drags: display row 12 → display row 14

**Current buggy behavior**:
1. HitTestCell returns displayRow=12
2. OnPointerMoved clamps: `Math.Clamp(12, 0, 9)` → becomes local index 3 or 9 (wrong!)
3. _dragCurrentRow = 3 (corrupted)
4. Stored as display row instead of local

**Expected behavior**:
1. HitTestCell returns displayRow=12 ✓
2. Clamp in display space: `Math.Clamp(12, 5, 14)` → stays 12 ✓
3. _dragCurrentRow = 12 (display space, correct)
4. Only convert to local when creating SelectionRegion

## Possible Solutions

### Option A: Separate Display/Local Coordinate Spaces Fully
- Rename variables: `row` → `displayRow`, `_dragStartRow` → `_dragStartDisplayRow`, etc.
- Always store display space internally, convert to local only at boundaries
- Add clear documentation to HitTestCell return value
- **Pros**: Clear semantics, no confusion
- **Cons**: Larger refactor, rename across multiple methods

### Option B: Consistent Clamping in Display Space
- Keep display space in drag state vars
- Always clamp against `[OriginRow, OriginRow+RowCount-1]` bounds
- Convert to local only in OnPointerReleased
- **Pros**: Minimal changes, fixes the bug
- **Cons**: Requires careful review of all clamping sites

### Option C: Document Current System Clearly
- Keep code as-is but add extensive comments
- Document which variables are display vs. local at each location
- Add comments explaining the coordinate conversion pattern
- **Pros**: Minimal code changes
- **Cons**: Bug remains, documentation alone doesn't fix it

## Recommended Approach

**Option A** (Separate coordinate spaces): Best long-term maintainability.

**Short-term (Week 1)**:
1. Rename HitTestCell to accept `out int displayRow, out int displayCol`
2. Add method documentation: "Returns display-space coordinates (not local)"
3. Update OnPointerMoved line 584-585:
   ```csharp
   HitTestCell(pos, out int displayRow, out int displayCol);
   displayRow = Math.Clamp(displayRow, sheet.OriginRow, sheet.OriginRow + sheet.RowCount - 1);
   displayCol = Math.Clamp(displayCol, sheet.OriginColumn, sheet.OriginColumn + sheet.ColumnCount - 1);
   _dragCurrentRow = displayRow;  // Store display space
   ```
4. Verify OnPointerReleased handles display → local conversion correctly

**Medium-term (Week 2)**:
5. Add unit tests for origin-based drag scenarios
6. Document coordinate system in SheetGridCanvas class header

## Notes

- **Regression risk**: LOW — Bug only manifests when OriginRow > 0
- **Workaround**: Currently works for sheets starting at row 0, column 0
- **Related**: RenderPendingSelection also has coordinate issues (double-add OriginRow at line 471)
- **Test gap**: No unit tests exist for SheetGridCanvas with non-zero origin

## Related Documentation

- **Code Review Report**: `/data/repos/sheet-atlas/.development/archive/reviews/canvas-coordinate-review.md`
- **Architecture Decision**: ADR-002 (Row Indexing Semantics), ADR-009 (DataRegion Data Model)
- **Related Bug**: `canvas-pending-selection-double-offset` (issue #471)
- **File**: `src/SheetAtlas.UI.Avalonia/Controls/SheetGridCanvas.cs`

---

## Test Cases Needed

```csharp
[Theory]
[InlineData(0, 0)]      // No origin
[InlineData(5, 0)]      // Origin row offset
[InlineData(0, 3)]      // Origin column offset
[InlineData(5, 3)]      // Both offsets
public void DragSelection_WithOrigin_SelectsCorrectCells(int originRow, int originCol)
{
    // Drag from display row 10, col 5 to display row 15, col 8
    // Verify SelectionRegion boundaries are correct in local space
}
```

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) for SheetGridCanvas location and coordinate system context in ADR-002/ADR-009.
