---
type: performance
priority: medium
status: open
discovered: 2025-10-22
related: []
related_decision: null
report: null
---

# Converter Creates New Brushes on Every Conversion

## Problem

`ComparisonTypeToBackgroundConverter` creates **new** `SolidColorBrush` instances for each conversion call instead of caching or using theme resources.

**File**: `src/SheetAtlas.UI.Avalonia/Converters/ComparisonTypeToBackgroundConverter.cs`

## Current Code (lines 42-64, 102)

```csharp
private static SolidColorBrush GetMatchBackground(bool isDarkMode)
{
    return isDarkMode
        ? new SolidColorBrush(Color.Parse("#0D1117"))  // NEW instance every time
        : new SolidColorBrush(Color.Parse("#FFFFFF"));
}
// Same pattern in GetNewBackground(), GetMissingBackground(), CreateGradientBrush()
```

## Impact

- Memory pressure from frequent allocations during UI rendering
- Potential GC pauses during comparison view scrolling
- Not following Avalonia best practices for brush resources

## Proposed Fix

Option A: **Cache brush instances** (simple)
```csharp
private static readonly SolidColorBrush DarkMatchBrush = new(Color.Parse("#0D1117"));
private static readonly SolidColorBrush LightMatchBrush = new(Color.Parse("#FFFFFF"));
// etc.
```

Option B: **Use theme resources** (preferred for theme switching)
```csharp
return Application.Current.FindResource("ComparisonMatchBackgroundDark") as IBrush;
```

## Notes

- Low priority since comparison view is not rendered frequently
- Would become higher priority if adding real-time diff view or large dataset scrolling
