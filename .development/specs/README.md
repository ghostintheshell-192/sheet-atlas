# Feature Specifications

This folder contains individual specifications for all SheetAtlas features.

## Structure

```text
specs/
├── implemented/     # Features already working in production
├── in-progress/     # Features currently being developed
├── planned/         # Features confirmed for upcoming releases
└── backlog/         # Validated ideas not yet scheduled
```

## Spec File Format

Each spec follows this template:

```markdown
# Feature Name

**Status**: implemented | in-progress | planned | backlog
**Release**: v0.X.0 (or "unassigned")
**Priority**: must-have | should-have | nice-to-have
**Depends on**: [list of spec files]

## Summary
One sentence: what it does and why it matters.

## User Stories
- As a [user type], I want [action] so that [benefit]

## Requirements

### Functional
- [ ] Requirement 1
- [ ] Requirement 2

### Non-Functional
- Performance: ...
- Security: ...

## Technical Notes
Implementation decisions, dependencies, links to ADRs.

## Acceptance Criteria
- [ ] Verifiable criterion 1
- [ ] Verifiable criterion 2

## Open Questions
- Question that needs resolution before implementation
```

## Workflow

### Adding a New Feature

1. Create spec in `backlog/` with basic info
2. Discuss and refine requirements
3. Assign to release → move to `planned/`
4. Start development → move to `in-progress/`
5. Complete → move to `implemented/`

### Updating Releases

Edit `/releases.md` to map releases to specs.

## Naming Convention

- Use kebab-case: `column-filtering.md`, `template-validation.md`
- Be descriptive but concise
- Include the main noun: `export-excel.md` not just `excel.md`

## Cross-References

- Link to ADRs: `reference/decisions/NNN-name.md`
- Link to tech-debt: `active/tech-debt/issue-name.md`
- Link to other specs: `../planned/other-feature.md`

---

*Last updated: 2025-11-30*
