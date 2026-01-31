# Decision 006: Git Workflow

**Date**: October 2025
**Status**: Active

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

1. NEVER work directly on main
2. develop is the default working branch
3. Create feature branch for each task
4. Merge to develop when feature complete
5. Merge develop to main only for releases

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

## Consequences

- Must remember to create feature branch before starting work
- develop may contain unreleased features
- main always reflects latest release
