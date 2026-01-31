# Decision 001: Error Handling Philosophy

**Date**: October 2025
**Status**: Active

## Context

Needed a consistent approach to error handling across the entire codebase to avoid confusion about when to throw exceptions vs return result objects.

## Decision

"Fail Fast for bugs, Never Throw for business errors"

- **Exceptions = Programming Bugs** (ArgumentNullException, InvalidOperationException)
  - Use for precondition violations that should never happen
  - Constructor validation, invalid state transitions

- **Result Objects = Business Errors** (ExcelFile with LoadStatus.Failed)
  - Use for expected domain outcomes (file not found, corrupted data)
  - Core/Domain layer returns Result objects, never throws for business logic
  - UI layer checks Status property instead of catching exceptions

## Layered Strategy

| Layer | Strategy |
|-------|----------|
| Core/Domain | Never Throw - Return Result objects |
| Infrastructure | Fail Fast - Validate preconditions only |
| UI/Application | Minimal Catch - Safety net only (OutOfMemoryException, generic Exception) |

## Rationale

- Makes code more predictable and testable
- Avoids redundant try-catch blocks
- Clear separation between "something went wrong" (bug) and "user did something invalid" (expected)

## Consequences

- Need to consistently use LoadStatus/Result patterns in domain layer
- UI must check Status properties before accessing data
- Cleaner exception handling, fewer catch blocks
