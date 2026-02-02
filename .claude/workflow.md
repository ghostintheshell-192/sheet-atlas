# Git Workflow & Release Process

## Git Workflow

See ADR-006 for complete details.

**Quick reference:**

- `main`: releases only
- `develop`: default branch for development
- `feature/*`, `fix/*`, `docs/*`, `experiment/*`: task branches

**NEVER work on main directly.**

**Typical workflow:**

```bash
# Start new feature
git checkout develop
git pull origin develop
git checkout -b feature/task-name

# Work and commit
git add .
git commit -m "feat: descriptive message"

# Merge when complete
git checkout develop
git merge feature/task-name
git push origin develop
```

## Release Process

See `docs/RELEASE_PROCESS.md` for complete workflow and checklist.

**Creating a Release:**

When user requests a release (e.g., "create release v0.4.0"):

1. ✅ **Read `docs/RELEASE_PROCESS.md` FIRST** (mandatory)
2. ✅ Create TodoList from "Release Checklist"
3. ✅ Follow steps sequentially
4. ✅ Ask user confirmation before:
   - Creating/pushing tags
   - Merging to main
   - Manual website deployment

**NEVER skip reading RELEASE_PROCESS.md** - it contains critical steps and automation details.

## Investigation & Analysis Workflow

When analyzing tech-debt, bugs, or investigating issues:

1. **Read the tech-debt/issue description** - Understand the problem
2. **Read `.development/ARCHITECTURE.md`** - Find relevant files using:
   - Project Tree (file index with descriptions)
   - Layer Overview (understand dependencies)
   - Related ADRs (architectural context)
3. **Read files in logical order** - Follow layer structure (UI → Core ← Infrastructure)
4. **Report findings** - Summary of what you found and where

**Always read ARCHITECTURE.md before exploring code** - it's your navigation map.

## Quick Commands

```bash
# Build and run
dotnet build && dotnet run --project src/SheetAtlas.UI.Avalonia

# Run tests
dotnet test

# Format check
dotnet format --verify-no-changes
```
