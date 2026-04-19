#!/usr/bin/env python3
"""Manage spec file lifecycle: move between status directories and update frontmatter.

Usage:
    spec-workflow.py start <spec-name>    # Move spec to in-progress
    spec-workflow.py done <spec-name>     # Move spec to implemented
    spec-workflow.py status <spec-name>   # Show current status

The spec-name can be with or without .md extension.
Called by git hooks (post-checkout, pre-commit) or manually.
"""

import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
SPECS_DIR = PROJECT_ROOT / ".development" / "specs"

STATUS_DIRS = ["implemented", "in-progress", "planned", "backlog", "archived"]

BRANCH_PREFIXES = ("feature/", "fix/", "docs/", "experiment/", "refactor/")


def find_spec(name: str) -> Path | None:
    """Find a spec file by name across all status directories."""
    if not name.endswith(".md"):
        name = f"{name}.md"
    for status_dir in STATUS_DIRS:
        path = SPECS_DIR / status_dir / name
        if path.exists():
            return path
    return None


def current_status(spec_path: Path) -> str:
    """Get the current status directory of a spec."""
    return spec_path.parent.name


def move_spec(spec_path: Path, target_status: str) -> Path | None:
    """Move a spec to a target status directory and update frontmatter.

    Returns the new path, or None if no move was needed.
    """
    if current_status(spec_path) == target_status:
        return None

    target_dir = SPECS_DIR / target_status
    target_dir.mkdir(parents=True, exist_ok=True)
    new_path = target_dir / spec_path.name

    # Update frontmatter status field
    text = spec_path.read_text(encoding="utf-8")
    status_label = "implemented" if target_status == "implemented" else target_status
    text = re.sub(
        r"^(\*\*Status\*\*:\s*).+$",
        rf"\g<1>{status_label}",
        text,
        count=1,
        flags=re.MULTILINE,
    )
    new_path.write_text(text, encoding="utf-8")

    # Remove original
    spec_path.unlink()

    return new_path


def branch_to_spec_name(branch: str) -> str | None:
    """Extract spec name from branch name.

    feature/redis-caching -> redis-caching
    fix/feed-parsing-bug -> feed-parsing-bug
    """
    for prefix in BRANCH_PREFIXES:
        if branch.startswith(prefix):
            return branch[len(prefix):]
    return None


def cmd_start(spec_name: str) -> int:
    """Move spec to in-progress."""
    spec_path = find_spec(spec_name)
    if spec_path is None:
        return 1

    status = current_status(spec_path)
    if status == "implemented":
        # Don't move backwards from implemented
        return 0
    if status == "in-progress":
        return 0

    new_path = move_spec(spec_path, "in-progress")
    if new_path:
        print(f"  spec: {spec_name} -> in-progress (was {status})")
    return 0


def cmd_done(spec_name: str) -> int:
    """Move spec to implemented."""
    spec_path = find_spec(spec_name)
    if spec_path is None:
        return 1

    status = current_status(spec_path)
    if status == "implemented":
        return 0

    new_path = move_spec(spec_path, "implemented")
    if new_path:
        print(f"  spec: {spec_name} -> implemented (was {status})")
    return 0


def cmd_status(spec_name: str) -> int:
    """Show current spec status."""
    spec_path = find_spec(spec_name)
    if spec_path is None:
        print(f"  spec: {spec_name} not found")
        return 1
    print(f"  spec: {spec_name} -> {current_status(spec_path)}")
    return 0


def main() -> int:
    if len(sys.argv) < 3:
        print(f"Usage: {sys.argv[0]} <start|done|status> <spec-name>")
        return 1

    command = sys.argv[1]
    spec_name = sys.argv[2]

    commands = {"start": cmd_start, "done": cmd_done, "status": cmd_status}
    if command not in commands:
        print(f"Unknown command: {command}")
        return 1

    return commands[command](spec_name)


if __name__ == "__main__":
    sys.exit(main())
