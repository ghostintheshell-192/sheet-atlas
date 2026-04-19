# Workflow

## Session Start

At the beginning of every session, before starting any work:

1. **Read the latest handoff** in `.memory-bank/` (the most recent `.md` file by date in the filename)
2. **Read any linked files** referenced in the handoff (e.g., progress specs, related notes)
3. **Cross-reference** with `memory/MEMORY.md` for stable project facts

This ensures continuity between sessions. The `.memory-bank/` contains detailed session diaries (what was done, why, what's next), while `memory/MEMORY.md` holds a compact index of stable facts.

---

## Git Workflow

See ADR-006 for complete details.

**Quick reference:**

- `main`: releases only
- `develop`: default branch for development
- `feature/*`, `fix/*`, `docs/*`, `refactor/*`, `experiment/*`, `chore/*`: task branches

**NEVER work on `main` or `develop` directly.** Both are branch-protected: the
pre-commit hook `.githooks/pre-commit.d/00-branch-protection` blocks direct
commits. Merge commits (`git merge --no-ff`) are explicitly allowed on these
branches — that is the supported path to land work.

**Typical workflow:**

```bash
# Start new feature
git checkout develop
git pull origin develop
git checkout -b feature/task-name

# Work and commit
git add <files>
git commit -m "feat: descriptive message"

# Merge when complete
git checkout develop
git merge --no-ff feature/task-name
git push origin develop
```

### Collaboration convention

On this project, Claude creates task branches, commits, and merges to `develop`
once the work is complete. The user runs `git push origin develop` manually.
Git commands are confirmed one at a time rather than added to the permissions
allowlist — the confirmation prompt is the deliberate checkpoint to re-read the
diff before it lands.

### Spec lifecycle automation

The repo ships hooks that move spec files between `specs/{planned,in-progress,implemented}/`
based on git activity:

- **`post-checkout`**: on `git checkout -b {feature,fix,docs,refactor,experiment}/<name>`,
  the matching spec in `specs/planned/<name>.md` is moved to `specs/in-progress/`
  and its frontmatter `**Status**` field is updated.
- **`pre-commit.d/05-spec-workflow`**: on merge commits into `develop`, the branch
  name is parsed and the matching spec is moved to `specs/implemented/` and
  staged as part of the merge commit.

The script at `.development/scripts/spec-workflow.py` is the shared backend;
missing specs, unknown branch prefixes, and non-merge commits all exit silently.
Activation requires `git config core.hooksPath .githooks` (done once per clone).

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
