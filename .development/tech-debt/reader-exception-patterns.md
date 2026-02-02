---
type: code-quality
priority: low
status: open
discovered: 2025-10-22
related: []
related_decision: 001-error-handling-philosophy.md
report: null
---

# Inconsistent Exception Handling at Sheet Level in File Readers

## Problem

The three file readers have **consistent** exception handling at file level, but **inconsistent** patterns at sheet/record level.

## Current State

### File Level (CONSISTENT - OK)

All three readers follow the same pattern:

- `OperationCanceledException` → throw (propagate)
- `IOException`, `UnauthorizedAccessException` → Return Failed
- Generic `Exception` → Return Failed with error

### Sheet Level (INCONSISTENT)

| Reader | Sheet-level catch |
| ------ | ----------------- |
| `OpenXmlFileReader` | `InvalidCastException`, `XmlException` (specific) |
| `XlsFileReader` | `Exception` (generic catch-all) |
| `CsvFileReader` | `Exception` (generic catch-all) |

## Impact

- Maintenance overhead: different patterns to understand
- Potential missed specific error handling in XLS/CSV readers
- Minor: all paths ultimately result in error being added to sheet

## Proposed Fix

Standardize sheet-level handling:

1. Define expected exceptions per reader type
2. Catch specific exceptions for known failure modes
3. Use generic catch-all only as final safety net

## Notes

- Low priority: current code works correctly
- File-level consistency (the important part) is already achieved
- Consider addressing when refactoring readers for other reasons

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
