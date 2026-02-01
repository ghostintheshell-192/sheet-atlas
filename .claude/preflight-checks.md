# Pre-flight Checks

Pre-flight checks ensure the development environment is safe and properly configured before making code modifications.

## When to Run

**Run pre-flight checks when:**

- User requests code modifications ("implement...", "add...", "fix...")
- User says "work on..." or "start working on..."
- About to make changes that will be committed

**Skip pre-flight checks when:**

- Read-only operations (exploring, reading files, explaining code)
- Already on a feature branch and continuing work
- User explicitly requests to skip checks

## Essential Checks

### 1. Git Repository Verification

- Verify git repository exists (`git rev-parse --git-dir`)
- If not: warn and offer to initialize
- If yes: proceed

### 2. Current Branch Check

- **If on `main`/`master`**: STOP, create feature branch required
- **If on `develop`**: suggest feature branch for non-trivial changes
- **If on feature branch**: proceed

Check for existing relevant branches before creating new ones.

### 3. Uncommitted Changes Check

- **"warn" mode (default)**: notify about uncommitted files, ask to continue/commit/stash
- **"block" mode**: stop until changes committed or stashed
- **"ignore" mode**: proceed silently

### 4. Remote Configuration Check

- **"info" mode (default)**: display remote status (informational)
- **"warn" mode**: warn if no remote configured
- **"ignore" mode**: skip check

### 5. Untracked Important Files Check

- Detect important untracked files (*.csproj,*.sln, pubspec.yaml, package.json, requirements.txt, README.md, CLAUDE.md, config files)
- Offer to add them to git if found

## Policy

**If check fails critically:**

- Stop workflow
- Show clear error message with actionable resolution
- Wait for user to resolve

**If check warns but doesn't block:**

- Show warning
- Ask user for confirmation
- Respect user's decision

## Configuration

Configuration is defined in `.rules/user-preferences.yaml`:

```yaml
preflight_checks:
  enabled: true
  checks:
    git_initialized: true
    current_branch: true
    uncommitted_changes: "warn"      # warn | block | ignore
    remote_configured: "info"        # info | warn | ignore
    untracked_files: "info"          # info | warn | ignore
  auto_create_feature_branch: true
  check_existing_branch: true
```
