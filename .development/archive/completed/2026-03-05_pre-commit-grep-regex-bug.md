---
type: bug
priority: low
status: resolved
discovered: 2025-12-10
resolved: 2026-02-02
related: []
related_decision: null
report: null
---

# Pre-commit hook grep regex incompatibility

## Problem

The global pre-commit security scanner (`/data/repos/.git-hooks/pre-commit.d/01-security`) uses Perl-style regex patterns with `grep -E`, causing warnings during commit:

```bash
grep: unrecognized option '-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----'
```

The check still passes, but the private key pattern is not actually being scanned.

## Analysis

Line 94 in `01-security`:

```bash
'-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----'
```

Problems:

1. `\s+` is PCRE (Perl regex) syntax, not supported by `grep -E` (ERE)
2. The `-----` at the start is interpreted as a command-line option
3. `grep -qE "$pattern"` fails silently for this pattern

The security scanner uses `grep -E` (Extended Regular Expressions) which doesn't support:

- `\s` (whitespace shorthand) - should be `[[:space:]]`
- `\d` (digit shorthand) - should be `[0-9]`

## Possible Solutions

- **Option A**: Use `grep -P` (Perl regex) instead of `grep -E`
  - Pro: All patterns work as-is
  - Con: `-P` not available on all systems (macOS, some BSDs)

- **Option B**: Convert patterns to ERE syntax
  - Change `\s+` to `[[:space:]]+`
  - Pro: Portable across all systems
  - Con: Patterns become longer/uglier

- **Option C**: Add `--` separator and use ERE patterns
  - `grep -qE -- "$pattern"` prevents option parsing
  - Still need to fix `\s+` syntax
  - Pro: Fixes both issues

## Recommended Approach

**Option C** - Add `--` and convert to ERE:

```bash
# Change line 94 from:
'-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----'

# To:
'-----BEGIN[[:space:]]+(RSA[[:space:]]+)?PRIVATE[[:space:]]+KEY-----'

# And change grep calls from:
grep -qE "$pattern"

# To:
grep -qE -- "$pattern"
```

## Notes

- This is a global hook issue (`/data/repos/.git-hooks/`), not specific to sheet-atlas
- The bug doesn't block commits, but means private keys aren't being scanned
- Other patterns in the file also use `\s` and should be reviewed

## Resolution

Hooks moved to project-level `.githooks/` directory and fixed:

1. Converted `\s` to `[[:space:]]` in all SECRET_PATTERNS (ERE-compatible)
2. Added `--` to grep calls to prevent pattern interpretation as options
3. Configured git to use local hooks: `git config core.hooksPath .githooks`

## Related Documentation

- **Original Location**: `/data/repos/.git-hooks/pre-commit.d/01-security`
- **New Location**: `.githooks/pre-commit.d/01-security`

---

📍 **Investigation Note**: Read [ARCHITECTURE.md](../ARCHITECTURE.md) to locate relevant files and understand the architectural context before starting your analysis.
