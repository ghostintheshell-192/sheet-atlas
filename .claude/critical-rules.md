# Critical Architecture Rules

⚠️ **These rules apply across the entire codebase and must not be violated.**

## Error Handling Philosophy

Never throw for business errors. Use exceptions only for programming bugs (ArgumentNull, InvalidOperation), Result objects for expected failures (file not found, corrupted data). Core/Domain returns Result, UI checks Status.

## Row Indexing Semantics

0-based absolute indexing internally, 1-based for display. SearchResult.Row and SASheetData use row 0 as first row. UI converts with displayRow = internalRow + 1. Search/comparison skip header rows (HeaderRowCount).

---

📖 **For detailed context, read the complete ADR:**

- [Decision 001: Error Handling Philosophy](../.development/reference/decisions/001-error-handling-philosophy.md)
- [Decision 002: Row Indexing Semantics](../.development/reference/decisions/002-row-indexing-semantics.md)
- [All ADRs](../.development/reference/decisions/)

---

*Auto-generated from ADRs with Impact=critical. Run `.development/scripts/generate-claude-config.sh` to update.*
