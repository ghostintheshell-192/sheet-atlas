#!/bin/bash
# Archive Resolved Tech Debt Issues
# Automatically moves resolved/closed/rejected issues from tech-debt/ to archive/completed/
#
# Usage: ./archive-resolved-issues.sh
# Location: .development/scripts/

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEVELOPMENT_DIR="$(dirname "$SCRIPT_DIR")"
TECH_DEBT_DIR="$DEVELOPMENT_DIR/tech-debt"
ARCHIVE_DIR="$DEVELOPMENT_DIR/archive/completed"
CURRENT_DATE=$(date +%Y-%m-%d)

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}  Archive Resolved Tech Debt Issues${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Check if tech-debt directory exists
if [ ! -d "$TECH_DEBT_DIR" ]; then
    echo -e "${RED}✗ Error:${NC} tech-debt directory not found: $TECH_DEBT_DIR"
    exit 1
fi

# Create archive directory if needed
mkdir -p "$ARCHIVE_DIR"

MOVED_COUNT=0
SKIPPED_COUNT=0

# Process each markdown file in tech-debt/
for file in "$TECH_DEBT_DIR"/*.md; do
    # Skip if no files found
    if [ ! -f "$file" ]; then
        continue
    fi

    FILENAME=$(basename "$file")

    # Skip template and README
    if [ "$FILENAME" = "_TEMPLATE.md" ] || [ "$FILENAME" = "README.md" ]; then
        continue
    fi

    # Extract status from YAML frontmatter
    # Look for "status: resolved", "status: closed", or "status: rejected"
    STATUS=$(grep -m 1 "^status:" "$file" 2>/dev/null | sed 's/status: *//' | tr -d ' \r\n')

    if [ "$STATUS" = "resolved" ] || [ "$STATUS" = "closed" ] || [ "$STATUS" = "rejected" ]; then
        # Check if filename already has date prefix (YYYY-MM-DD_)
        if [[ ! "$FILENAME" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}_ ]]; then
            # Add date prefix
            NEW_FILENAME="${CURRENT_DATE}_${FILENAME}"
        else
            # Already has date, keep as is
            NEW_FILENAME="$FILENAME"
        fi

        NEW_PATH="$ARCHIVE_DIR/$NEW_FILENAME"

        # Check if target already exists
        if [ -f "$NEW_PATH" ]; then
            echo -e "${YELLOW}  ⚠ Skipped:${NC} $FILENAME (already exists in archive)"
            SKIPPED_COUNT=$((SKIPPED_COUNT + 1))
            continue
        fi

        # Move the file
        mv "$file" "$NEW_PATH"
        MOVED_COUNT=$((MOVED_COUNT + 1))

        echo -e "${GREEN}  ✓ Archived:${NC} $FILENAME → ${NEW_FILENAME} (status: $STATUS)"
    fi
done

echo ""
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# Summary
if [ $MOVED_COUNT -gt 0 ]; then
    echo -e "${GREEN}✓ Archived $MOVED_COUNT issue(s)${NC}"
else
    echo -e "${BLUE}ℹ No resolved issues to archive${NC}"
fi

if [ $SKIPPED_COUNT -gt 0 ]; then
    echo -e "${YELLOW}⚠ Skipped $SKIPPED_COUNT issue(s) (duplicates)${NC}"
fi

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

exit 0
