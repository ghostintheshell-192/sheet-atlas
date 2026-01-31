#!/usr/bin/env python3
"""
Generate INDEX.md for sheet-atlas development documentation.
Scans .development/ and docs/ folders and creates a navigable index.
"""

import os
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Tuple

# Configuration
DEVELOPMENT_DIR = Path(__file__).parent.parent
PROJECT_ROOT = DEVELOPMENT_DIR.parent
DOCS_DIR = PROJECT_ROOT / "docs"
INDEX_FILE = DEVELOPMENT_DIR / "INDEX.md"
DAYS_RECENT = 7

# Folders to exclude from indexing
EXCLUDE_FOLDERS = {
    "scripts",
    "__pycache__",
}

# Quick links - files to highlight at the top
QUICK_LINKS = [
    ("Current Status", "CURRENT-STATUS.md"),
    ("Tech Debt", "tech-debt/README.md"),
    ("Specs", "specs/README.md"),
    ("ADR", "reference/decisions/README.md"),
]


def get_file_info(path: Path) -> Tuple[str, int, datetime]:
    """Get file name, size in KB, and modification time."""
    stat = path.stat()
    size_kb = stat.st_size // 1024
    mtime = datetime.fromtimestamp(stat.st_mtime)
    return path.name, size_kb, mtime


def scan_directory(base_dir: Path, relative_to: Path) -> Dict[str, List[dict]]:
    """Scan directory and return organized file info."""
    result = {}

    if not base_dir.exists():
        return result

    for item in sorted(base_dir.iterdir()):
        if item.name.startswith(".") or item.name in EXCLUDE_FOLDERS:
            continue

        rel_path = item.relative_to(relative_to)

        if item.is_file() and item.suffix == ".md":
            folder = str(rel_path.parent) if rel_path.parent != Path(".") else "root"
            if folder not in result:
                result[folder] = []
            name, size, mtime = get_file_info(item)
            result[folder].append({
                "name": name,
                "path": str(rel_path),
                "size_kb": size,
                "mtime": mtime,
            })
        elif item.is_dir():
            sub_result = scan_directory(item, relative_to)
            result.update(sub_result)

    return result


def format_file_entry(file_info: dict, now: datetime) -> str:
    """Format a single file entry with optional 'recent' marker."""
    days_ago = (now - file_info["mtime"]).days
    recent_marker = " **RECENT**" if days_ago <= DAYS_RECENT else ""
    date_str = file_info["mtime"].strftime("%Y-%m-%d")
    size_str = f"{file_info['size_kb']}KB" if file_info["size_kb"] > 0 else "<1KB"
    return f"- [{file_info['name']}]({file_info['path']}) ({size_str}, {date_str}){recent_marker}"


def generate_index() -> str:
    """Generate the complete INDEX.md content."""
    now = datetime.now()
    lines = []

    # Header
    lines.append("# INDEX - Sheet Atlas Development Documentation")
    lines.append("")
    lines.append(f"*Auto-generated: {now.strftime('%Y-%m-%d %H:%M')}*")
    lines.append("")
    lines.append("---")
    lines.append("")

    # Quick Links
    lines.append("## Quick Links")
    lines.append("")
    for title, path in QUICK_LINKS:
        full_path = DEVELOPMENT_DIR / path
        if full_path.exists():
            lines.append(f"- [{title}]({path})")
        else:
            lines.append(f"- {title} *(file not found: {path})*")
    lines.append("")
    lines.append("---")
    lines.append("")

    # Development Documentation (.development/)
    lines.append("## Development Documentation (.development/)")
    lines.append("")
    lines.append("*Not committed to git - specs, tech-debt, decisions*")
    lines.append("")

    dev_files = scan_directory(DEVELOPMENT_DIR, DEVELOPMENT_DIR)

    # Sort folders in a logical order
    folder_order = ["root", "specs", "tech-debt", "reference", "archive"]
    sorted_folders = sorted(
        dev_files.keys(),
        key=lambda x: (folder_order.index(x.split("/")[0]) if x.split("/")[0] in folder_order else 99, x)
    )

    for folder in sorted_folders:
        files = dev_files[folder]
        if not files:
            continue

        folder_display = folder if folder != "root" else "(root)"
        lines.append(f"### {folder_display}/ ({len(files)} files)")
        lines.append("")

        # Sort files by modification time (newest first)
        files.sort(key=lambda x: x["mtime"], reverse=True)

        for f in files:
            lines.append(format_file_entry(f, now))
        lines.append("")

    lines.append("---")
    lines.append("")

    # Public Documentation (docs/)
    lines.append("## Public Documentation (docs/)")
    lines.append("")
    lines.append("*Committed to git - user-facing documentation*")
    lines.append("")

    if DOCS_DIR.exists():
        docs_files = scan_directory(DOCS_DIR, PROJECT_ROOT)
        for folder in sorted(docs_files.keys()):
            files = docs_files[folder]
            if not files:
                continue
            lines.append(f"### {folder}/")
            lines.append("")
            for f in files:
                lines.append(format_file_entry(f, now))
            lines.append("")
    else:
        lines.append("*docs/ folder not found*")
        lines.append("")

    lines.append("---")
    lines.append("")

    # Recently Modified
    lines.append("## Recently Modified (last 7 days)")
    lines.append("")

    all_files = []
    for folder, files in dev_files.items():
        all_files.extend(files)

    recent_cutoff = now - timedelta(days=DAYS_RECENT)
    recent_files = [f for f in all_files if f["mtime"] > recent_cutoff]
    recent_files.sort(key=lambda x: x["mtime"], reverse=True)

    if recent_files:
        for i, f in enumerate(recent_files[:10], 1):
            days = (now - f["mtime"]).days
            days_str = "today" if days == 0 else f"{days}d ago"
            lines.append(f"{i}. [{f['name']}]({f['path']}) ({days_str})")
    else:
        lines.append("*No files modified in the last 7 days*")

    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("*Run `python .development/scripts/generate-index.py` to regenerate*")

    return "\n".join(lines)


def main():
    """Main entry point."""
    content = generate_index()
    INDEX_FILE.write_text(content, encoding="utf-8")
    print(f"Generated {INDEX_FILE}")


if __name__ == "__main__":
    main()
