#!/bin/bash
# Generate ARCHITECTURE.md with project tree and class descriptions
# Descriptions are extracted automatically from /// <summary> comments in C# files
#
# Run: .development/scripts/generate-architecture.sh
# Intended to be called by pre-commit hook or manually

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$DEV_DIR")"
OUTPUT_FILE="$DEV_DIR/ARCHITECTURE.md"
ADR_DIR="$DEV_DIR/reference/decisions"

MAX_DESC_LENGTH=200

# Colors for terminal output
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Stats
missing_count=0
long_count=0
total_count=0

# Extract /// <summary> for the main class/interface in a C# file
extract_summary() {
    local filepath="$1"

    # Use awk to find the summary block before the main type declaration
    awk '
        /\/\/\/ <summary>/ {
            in_summary = 1
            summary = ""
            next
        }
        /\/\/\/ <\/summary>/ {
            in_summary = 0
            next
        }
        in_summary && /\/\/\// {
            line = $0
            gsub(/^[[:space:]]*\/\/\/[[:space:]]*/, "", line)
            if (summary != "") summary = summary " "
            summary = summary line
        }
        /^[[:space:]]*(public|internal|private|protected)?[[:space:]]*(sealed|abstract|static|partial)?[[:space:]]*(class|interface|record|struct|enum)[[:space:]]/ {
            if (summary != "") {
                print summary
                exit
            }
        }
    ' "$filepath" 2>/dev/null
}

# Generate ADR list dynamically
generate_adr_list() {
    if [[ ! -d "$ADR_DIR" ]]; then
        echo "- See \`reference/decisions/\` for architecture decisions"
        return
    fi

    for adr in "$ADR_DIR"/[0-9]*.md; do
        [[ -f "$adr" ]] || continue
        local filename=$(basename "$adr" .md)
        local number="${filename%%-*}"
        local title="${filename#*-}"
        # Convert kebab-case to title case
        title=$(echo "$title" | sed 's/-/ /g' | sed 's/\b\(.\)/\u\1/g')
        echo "- [ADR-$number: $title](reference/decisions/$filename.md)"
    done
}

# Generate the header
generate_header() {
    cat << 'HEADER'
# Architecture Reference

Quick reference for navigating the SheetAtlas codebase.
For diagrams and detailed explanations, see [docs/project/ARCHITECTURE.md](../docs/project/ARCHITECTURE.md).

## Layer Overview

| Layer | Project | Purpose |
|-------|---------|---------|
| **UI** | SheetAtlas.UI.Avalonia | Avalonia views, ViewModels, Managers |
| **Core** | SheetAtlas.Core | Business logic, domain entities, services |
| **Infrastructure** | SheetAtlas.Infrastructure | File readers/writers, external integrations |
| **Logging** | SheetAtlas.Logging | Cross-cutting logging abstraction |

**Dependency rule**: UI → Core ← Infrastructure. Core has no knowledge of UI or Infrastructure.

## Key Decisions

HEADER

    generate_adr_list

    cat << 'HEADER2'

## Project Tree

> Auto-generated from `/// <summary>` comments in source files.
> Run `.development/scripts/generate-architecture.sh` to update.

HEADER2
}

# Process a directory: first files, then subdirectories
process_directory() {
    local dir="$1"
    local reldir="${dir#$PROJECT_ROOT/src/}"

    # Get files directly in this directory (not in subdirs)
    local files=()
    while IFS= read -r -d '' file; do
        files+=("$file")
    done < <(find "$dir" -maxdepth 1 -name "*.cs" -type f -print0 2>/dev/null | sort -z)

    # Only print header if there are files
    if [[ ${#files[@]} -gt 0 ]]; then
        echo ""
        echo "### $reldir"

        for filepath in "${files[@]}"; do
            local file=$(basename "$filepath")
            local desc=$(extract_summary "$filepath")

            ((total_count++)) || true

            if [[ -z "$desc" ]]; then
                echo "- \`$file\`"
                ((missing_count++)) || true
            elif [[ ${#desc} -gt $MAX_DESC_LENGTH ]]; then
                local truncated="${desc:0:$MAX_DESC_LENGTH}..."
                echo "- \`$file\` — $truncated ⚠️"
                ((long_count++)) || true
            else
                echo "- \`$file\` — $desc"
            fi
        done
    fi

    # Now process subdirectories
    local subdirs=()
    while IFS= read -r -d '' subdir; do
        subdirs+=("$subdir")
    done < <(find "$dir" -maxdepth 1 -mindepth 1 -type d ! -name "obj" ! -name "bin" -print0 2>/dev/null | sort -z)

    for subdir in "${subdirs[@]}"; do
        process_directory "$subdir"
    done
}

# Generate tree with proper grouping
generate_tree() {
    # Process each top-level project directory
    for project_dir in "$PROJECT_ROOT/src"/*/; do
        [[ -d "$project_dir" ]] || continue
        local dirname=$(basename "$project_dir")
        # Skip non-project directories
        [[ "$dirname" == "obj" || "$dirname" == "bin" ]] && continue
        process_directory "${project_dir%/}"
    done
}

# Generate footer
generate_footer() {
    cat << 'FOOTER'

---

*Auto-generated from source code comments by `.development/scripts/generate-architecture.sh`*
FOOTER
}

# Main execution
main() {
    echo "Generating architecture reference..."

    # Reset counters (they're in subshell, so we re-count at the end)
    missing_count=0
    long_count=0
    total_count=0

    # Generate the file
    {
        generate_header
        generate_tree
        generate_footer
    } > "$OUTPUT_FILE"

    echo -e "${GREEN}Generated:${NC} $OUTPUT_FILE"

    # Re-count for stats (since subshell loses variables)
    local total=0
    local missing=0
    local long=0

    while IFS= read -r -d '' filepath; do
        ((total++)) || true
        local desc=$(extract_summary "$filepath")
        if [[ -z "$desc" ]]; then
            ((missing++)) || true
        elif [[ ${#desc} -gt $MAX_DESC_LENGTH ]]; then
            ((long++)) || true
            echo -e "${YELLOW}Warning:${NC} $(basename "$filepath"): summary too long (${#desc} chars)"
        fi
    done < <(find "$PROJECT_ROOT/src" -name "*.cs" -type f ! -path "*/obj/*" ! -path "*/bin/*" -print0)

    echo ""
    echo "Stats: $total files, $missing without summary, $long with long summary"

    if [[ $missing -gt 0 ]]; then
        echo -e "${YELLOW}Tip:${NC} Add /// <summary> comments to document classes"
    fi
}

main "$@"
