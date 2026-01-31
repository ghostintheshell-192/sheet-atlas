# Tech Debt Issues

This folder contains individual technical debt issues for SheetAtlas.

## Structure

Each issue is a separate markdown file with standardized frontmatter:

```yaml
---
type: [bug|feature|refactor|performance|testing|code-quality|security]
priority: [high|medium|low]
status: [open|in-progress|resolved|closed|rejected]
discovered: YYYY-MM-DD
related: []  # List of related issue filenames
related_decision: null  # Optional: link to reference/decisions/NNN-name.md
report: null  # Optional: link to archive/analysis/YYYY-MM-DD_report_agent-name.md
---
```

**New fields** (2025-11-28):
- `related_decision`: Links issue to Architecture Decision Record if relevant
- `report`: Links to agent report if issue was auto-generated from agent analysis
- `status`: Extended with `closed` (completed without fix) and `rejected` (decided not to fix)

## Workflow

### Creating New Issues

1. Copy `_TEMPLATE.md`
2. Rename to descriptive slug: `issue-name.md` (NO DATE PREFIX)
3. Fill in frontmatter and content
4. Status starts as `open`

### Working on Issues

1. Update status to `in-progress`
2. Work on fix/implementation
3. When complete, add resolution sections:
   - Solution Implemented
   - Testing
   - Impact

### Archiving Completed Issues

**Automatic** (recommended):
1. Change status to `resolved`, `closed`, or `rejected` in frontmatter
2. Run: `../scripts/archive-resolved-issues.sh`
3. Script automatically moves to `archive/completed/` with date prefix

**Manual**:
1. Add date prefix: `YYYY-MM-DD_issue-name.md`
2. Move to `.personal/archive/completed/`
3. Delete from `tech-debt/`

## Current Issues by Priority

*Auto-updated: 2026-01-29 20:28*

**High Priority:** None currently

**Medium Priority:**
- `cellmetadata-memory-waste.md` - CellMetadata Memory Waste for NumberFormat Storage
- `comparison-export-ignores-column-filter.md` - Comparison Export Ignores Column Filtering
- `converter-creates-brushes.md` - Converter Creates New Brushes on Every Conversion
- `json-persistence-proposal.md` - JSON Persistence for Searches and Comparisons
- `row-comparison-sync-bug.md` - Row Comparison Sync Bug
- `search-results-collapsed-not-obvious.md` - Search Results Tree Not Obviously Expandable

**Low Priority:**
- `code-coverage-measurement.md` - Code Coverage Measurement
- `load-duration-not-measured.md` - Load Duration Not Measured
- `log-cleanup-not-called.md` - Log Cleanup Never Called
- `pre-commit-grep-regex-bug.md` - Pre-commit hook grep regex incompatibility
- `reader-exception-patterns.md` - Inconsistent Exception Handling at Sheet Level in File Readers
- `theme-change-bug.md` - Theme Change Bug in Search/Comparison Views

## Integration with Reference Documentation

### Linking to Architecture Decisions

If an issue relates to an architectural decision:

```yaml
---
related_decision: 001-error-handling-philosophy.md
---
```

This helps understand context: "Why was this pattern chosen? What were the trade-offs?"

### Agent-Generated Issues

When agents (code-reviewer, security-auditor, etc.) find issues:
- Issue created automatically in `tech-debt/`
- Full report in `archive/analysis/YYYY-MM-DD_report_agent-name.md`
- Issue links to report via `report:` field

### Creating Architecture Decisions

If resolving an issue requires a significant architectural choice:
1. Document decision in `../reference/decisions/NNN-name.md`
2. Link from issue: `related_decision: NNN-name.md`
3. Update CLAUDE.md if decision impacts coding standards

## Tips

- Use descriptive slugs for filenames
- Keep frontmatter up to date
- Link related issues in `related` field
- Link to architectural decisions in `related_decision` if applicable
- Date prefix ONLY when moving to archive (automatically handled by script)
- Check `_TEMPLATE.md` for structure
