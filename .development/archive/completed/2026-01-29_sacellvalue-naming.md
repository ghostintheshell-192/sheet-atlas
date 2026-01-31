---
type: code-quality
priority: low
status: open
discovered: 2025-11-08
related: []
---

# SACellValue Ambiguous Property Names

## Problem

`SACellValue` distinguishes `IsNumber` (floating-point double) from `IsInteger` (long) for memory optimization, but the naming is confusing:

- `IsNumber` → suggests "any numeric type" but actually means "floating-point only"
- `IsInteger` → clear meaning
- Developers naturally check only `IsNumber` and miss integers

**Impact:**
- Bug in ColumnAnalysisService: cells created with `FromInteger()` returned `Unknown` type because only `IsNumber` was checked
- Confusion for any code doing type detection

## Analysis

### Current (ambiguous)
```csharp
public bool IsNumber => _type == CellType.Number;
public bool IsInteger => _type == CellType.Integer;
```

### Proposed (clear)
```csharp
public bool IsFloatingPoint => _type == CellType.Number;
public bool IsInteger => _type == CellType.Integer;
```

## Possible Solutions

- **Option A**: Rename `IsNumber` → `IsFloatingPoint`
  - Add `[Obsolete]` for `IsNumber` with forwarding
  - Update all callsites (~20-30 locations)
  - Update documentation/comments
  - Pro: Clear naming
  - Con: Breaking change (mitigated by Obsolete)

- **Option B**: Add helper property `IsNumeric`
  - `public bool IsNumeric => IsNumber || IsInteger;`
  - Keep existing properties
  - Pro: Non-breaking
  - Con: Doesn't fix ambiguity

- **Option C**: Keep current with better docs
  - Document distinction clearly
  - Pro: No code change
  - Con: Confusion persists

## Recommended Approach

**Option A** after Foundation Layer completion, as part of general refactoring pass.

## Workaround (current)

Check both: `if (cell.IsInteger || cell.IsNumber)` for any numeric detection

## Notes

- Affects type detection code
- Foundation Layer context
- Not urgent but improves code clarity

## Status Verification (2025-11-28)

**Status**: Issue is **STILL VALID**.

**Evidence**:
- Current code (SACellValue.cs:135-136):
  ```csharp
  public bool IsNumber => _type == CellType.Number;
  public bool IsInteger => _type == CellType.Integer;
  ```

- Workaround usage (ColumnAnalysisService.cs:126):
  ```csharp
  // NOTE: SACellValue distinguishes Integer (long) from Number (double)
  //       but for column analysis, both are treated as numeric data types
  if (cell.IsInteger || cell.IsNumber) { ... }
  ```

**Confirmation**: Ambiguous naming persists. Developers must remember to check BOTH properties for numeric detection. Renaming `IsNumber` → `IsFloatingPoint` would improve clarity.

**Recommendation**: KEEP OPEN. Valid code quality issue, defer to next refactoring pass.
