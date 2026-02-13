#!/bin/bash
# Extract /// <summary> from C# files
# Usage: extract-summary.sh <filepath>
# Output: summary text on stdout (empty if none found)

filepath="$1"
[[ -f "$filepath" ]] || exit 0

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
