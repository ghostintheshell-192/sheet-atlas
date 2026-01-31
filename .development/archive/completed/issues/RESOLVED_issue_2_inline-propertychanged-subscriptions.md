# Issue #2: Inline PropertyChanged Subscriptions Without Unsubscribe

**Priority**: HIGH
**Files**: MainWindowViewModel.EventHandlers.cs (line 128)
**Status**: ✅ RESOLVED (2025-10-22)

## Problem
Inline lambda in SetSearchViewModel subscribes to PropertyChanged without unsubscribe mechanism.

## Current Code
```csharp
SearchViewModel.PropertyChanged += (s, e) => { /* handler */ };
```

## Fix Applied
Stored handler as field and added explicit unsubscribe in UnsubscribeFromEvents.

## Implementation Details
- Added `_searchViewModelPropertyChangedHandler` field to MainWindowViewModel
- Moved inline lambda to stored field in SetSearchViewModel
- Added unsubscribe logic in UnsubscribeFromEvents with null check
- Added `using System.ComponentModel` for PropertyChangedEventHandler type
- All 193 tests passing

## Memory Leak Prevention
- Before: Lambda created closure capturing `this`, preventing GC of MainWindowViewModel
- After: Stored handler allows explicit cleanup, breaking circular reference
- SearchViewModel can now be garbage collected properly when MainWindowViewModel is disposed
