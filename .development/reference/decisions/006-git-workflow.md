# Decision 006: Git Workflow

**Date**: October 2025
**Status**: Active
**Impact**: important
**Summary**: Two-branch model: main (releases only), develop (default for development), feature/* branches. Never work on main directly. Create feature branch per task, merge to develop when complete, merge develop to main for releases only.

## Context

Needed consistent branching and release strategy for solo/small team development.

## Decision

**Two-branch model** with feature branches:

- **main**: Production-ready code only (releases)
- **develop**: Integration branch (default for development)
- **feature/***: Individual feature development
- **fix/***: Bug fixes
- **experiment/***: Testing and probes

## Branch Rules

1. NEVER work directly on `main` or `develop` — both are branch-protected
2. Create a task branch for each piece of work; commit there
3. Merge task branch into `develop` (use `--no-ff` to preserve branch history)
4. Merge `develop` into `main` only for releases
5. The user pushes to `origin`; direct commits are refused by the pre-commit hook

Enforcement is automated: `.githooks/pre-commit.d/00-branch-protection` blocks
direct commits on `main`/`develop` while explicitly allowing merge commits
(detected via `$GIT_DIR/MERGE_HEAD`). This codifies rules 1 and 3 so they do
not rely on discipline alone.

## Naming Conventions

- `feature/task-description` - New functionality
- `fix/description` - Bug fixes
- `refactor/description` - Code improvements
- `docs/description` - Documentation updates
- `chore/description` - Maintenance tasks

## Release Process

1. Tag release on develop: `git tag v0.x.x`
2. Push tag: `git push origin v0.x.x`
3. GitHub Actions builds artifacts automatically
4. Merge develop → main after successful release
5. GitHub Pages website auto-updates

## Rationale

- Simple enough for solo development
- Clear separation between WIP and released code
- Automated release pipeline reduces manual errors
- Feature branches enable parallel work

## Spec Lifecycle Automation

Task branches double as spec-status signals. Two hooks keep `.development/specs/`
in sync with git activity:

- **`post-checkout`**: when a branch with a known prefix
  (`feature|fix|docs|refactor|experiment/<name>`) is checked out, the matching
  `specs/planned/<name>.md` is moved to `specs/in-progress/` and its frontmatter
  `**Status**` is rewritten.
- **`pre-commit.d/05-spec-workflow`**: on merge commits into `develop`, the
  branch name from `MERGE_MSG` is parsed and the matching spec is moved to
  `specs/implemented/` and staged with the merge.

The shared backend is `.development/scripts/spec-workflow.py`. Missing specs,
unknown branch prefixes, non-merge commits, and wrong branches all exit silently
— the automation is opportunistic, never blocking.

## Consequences

- Must remember to create a task branch before starting work (but the hooks
  enforce it rather than relying on memory)
- develop may contain unreleased features
- main always reflects latest release
- Activation requires `git config core.hooksPath .githooks` once per clone;
  without it, the protections silently do not apply
