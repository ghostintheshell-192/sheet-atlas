---
type: feature
priority: medium
status: open
discovered: 2025-11-27
related: ["row-comparison-sync-bug"]
---

# JSON Persistence for Searches and Comparisons

## Problem

When a file is unloaded, search and comparison data involving that file are lost (or cause memory leaks if not properly cleaned up). This creates a poor UX where users lose their work context.

## Analysis

Current implementation keeps all data in memory, tied to loaded files. When file unloads, data must be discarded or will leak memory.

## Possible Solutions

**Proposed: Save to local JSON files**

```text
~/.local/share/SheetAtlas/
├── searches/
│   ├── search_<hash>.json    # Query, results, timestamp
│   └── ...
├── comparisons/
│   ├── comparison_<hash>.json  # Compared rows, values
│   └── ...
└── index.json                # Index for quick access
```

### Benefits

- Resolves memory leak (data on disk, not in RAM)
- Persistence across sessions (reopen app → find searches again)
- Independence from original file (can be unloaded/modified)
- Historical "snapshot" of data at search time

### Implementation Details

- Searches: ~1-10 KB per search (query + results list)
- Comparisons: ~10-100 KB per comparison (rows × columns × values)
- Lazy loading for efficiency
- Periodic or manual cleanup

### UX when file no longer loaded

- Results visible but with indication "File not loaded"
- Option "Reload file" to navigate to results

## Alternatives Considered

- **Option A**: Keep everything in memory → Current, causes leak/loss
- **Option B**: JSON persistence → **Proposed**
- **Option C**: SQLite database → Overkill for this use case

## Recommended Approach

Implement JSON persistence as described. Start with searches, then extend to comparisons.

## Notes

### Related Issues

- Would resolve "Row Comparison Sync Bug" at architectural level
- Would simplify "Multiple Comparisons Merge" implementation
- Enables future features: search history export, comparison sharing

### Implementation Priority

Medium-High - improves UX significantly and resolves memory concerns

### Estimated Effort

- Searches: 1-2 days
- Comparisons: 1-2 days
- Total: 3-4 days

## Status Verification (2025-11-28)

**Status**: Proposal is **STILL VALID AND RELEVANT**.

**Current situation**:

- Searches and comparisons are still kept in memory
- Unloading files still causes data loss
- No persistence mechanism implemented yet

**Priority reassessment**: **MEDIUM-HIGH**

**Rationale**:

- Would resolve row-comparison-sync-bug at architectural level (confirmed by agent analysis)
- Would enable "session restore" functionality
- Improves UX significantly (users don't lose work context)
- Foundation for future features (search history, comparison export)

**Recommendation**: KEEP OPEN. Consider implementing after completing Foundation Layer integration, as this would be a good architectural enhancement for v0.4.x or v0.5.0.

**Implementation suggestion**: Start with searches (simpler), validate approach, then extend to comparisons.

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
