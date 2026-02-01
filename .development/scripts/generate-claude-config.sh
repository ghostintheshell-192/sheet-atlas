#!/bin/bash
# Generate auto-generated Claude Code configuration files

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$DEV_DIR")"
CLAUDE_DIR="$PROJECT_ROOT/.claude"
ADR_DIR="$DEV_DIR/reference/decisions"

echo "Generating Claude Code configuration files..."
echo ""

# ==========================================================================
# Generate critical-rules.md
# ==========================================================================

echo "Generating critical-rules.md..."

OUTFILE="$CLAUDE_DIR/critical-rules.md"

# Header
echo "# Critical Architecture Rules" > "$OUTFILE"
echo "" >> "$OUTFILE"
echo "⚠️ **These rules apply across the entire codebase and must not be violated.**" >> "$OUTFILE"
echo "" >> "$OUTFILE"

# Extract critical ADRs
critical_count=0
for adr_file in "$ADR_DIR"/*.md; do
    filename=$(basename "$adr_file")

    # Skip README
    if [ "$filename" = "README.md" ]; then
        continue
    fi

    # Extract Impact
    impact=$(grep "^\*\*Impact\*\*:" "$adr_file" | sed 's/\*\*Impact\*\*:[[:space:]]*//')

    # Only process critical ADRs
    if [ "$impact" = "critical" ]; then
        # Extract info
        title=$(head -1 "$adr_file" | sed 's/^#[[:space:]]*//')
        summary=$(grep "^\*\*Summary\*\*:" "$adr_file" | sed 's/\*\*Summary\*\*:[[:space:]]*//')

        # Clean title
        clean_title=$(echo "$title" | sed 's/^Decision [0-9]*:[[:space:]]*//' | sed 's/^ADR-[0-9]*:[[:space:]]*//')

        # Write to file
        echo "## $clean_title" >> "$OUTFILE"
        echo "" >> "$OUTFILE"
        echo "$summary" >> "$OUTFILE"
        echo "" >> "$OUTFILE"

        critical_count=$((critical_count + 1))
    fi
done

# Footer with links
echo "---" >> "$OUTFILE"
echo "" >> "$OUTFILE"
echo "📖 **For detailed context, read the complete ADR:**" >> "$OUTFILE"
echo "" >> "$OUTFILE"

# Add critical ADR links
for adr_file in "$ADR_DIR"/*.md; do
    filename=$(basename "$adr_file")

    if [ "$filename" = "README.md" ]; then
        continue
    fi

    impact=$(grep "^\*\*Impact\*\*:" "$adr_file" | sed 's/\*\*Impact\*\*:[[:space:]]*//')

    if [ "$impact" = "critical" ]; then
        title=$(head -1 "$adr_file" | sed 's/^#[[:space:]]*//')
        echo "- [$title](../.development/reference/decisions/$filename)" >> "$OUTFILE"
    fi
done

echo "- [All ADRs](../.development/reference/decisions/)" >> "$OUTFILE"
echo "" >> "$OUTFILE"
echo "---" >> "$OUTFILE"
echo "" >> "$OUTFILE"
echo "*Auto-generated from ADRs with Impact=critical. Run \`.development/scripts/generate-claude-config.sh\` to update.*" >> "$OUTFILE"

echo "✓ Generated $OUTFILE ($critical_count critical ADR(s))"

# ==========================================================================
# Generate coding-standards.md
# ==========================================================================

echo "Generating coding-standards.md..."

OUTFILE="$CLAUDE_DIR/coding-standards.md"

cat > "$OUTFILE" << 'ENDOFFILE'
# Coding Standards

Extracted from `.rules/coding-standards/csharp-dotnet.md`.

## Naming Conventions

- **Classes/Methods/Properties**: `PascalCase`
- **Local variables/parameters**: `camelCase`
- **Private fields**: `_camelCase`
- **Constants**: `PascalCase`
- **Interfaces**: Prefix with `I` (e.g., `IUserService`)

## Code Style

- **Enforced via**: `dotnet format` + `.editorconfig`
- **Pre-commit hook**: Verifies formatting before commit
- **Manual check**: `dotnet format --verify-no-changes`

## File Organization

- **One class per file**: Class name matches file name
- **Using statements order**:
  1. System namespaces
  2. Microsoft namespaces
  3. Third-party namespaces
  4. Local project namespaces
- **File structure**:
  1. Constants
  2. Fields
  3. Constructor
  4. Public methods
  5. Private methods

## Documentation

- **XML comments**: Required for public APIs and complex methods
- **Language**: English only
- **Focus**: Explain "why", not "what"
- **Example**:
  ```csharp
  /// <summary>
  /// Normalizes cell value for comparison by removing formatting artifacts.
  /// Required because Excel stores dates as numbers with formatting.
  /// </summary>
  ```

## Dependency Injection

- **Constructor injection**: Inject dependencies through constructor
- **Use interfaces**: Depend on abstractions, not implementations
- **Validate early**: Check for null in constructor, fail fast
- **Example**:
  ```csharp
  public class FileReader
  {
      private readonly ILogService _logger;

      public FileReader(ILogService logger)
      {
          _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      }
  }
  ```

---

*For complete coding standards, see `.rules/coding-standards/csharp-dotnet.md`.*
ENDOFFILE

echo "✓ Generated $OUTFILE"

echo ""
echo "Configuration files ready! Claude Code will load them via @includes in .claude/CLAUDE.md"
