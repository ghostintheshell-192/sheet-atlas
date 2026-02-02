#!/usr/bin/env python3
"""
session-archive.py
Archives Claude Code session transcripts to .memory-bank/sessions/
Called automatically by the SessionEnd hook.

Input: JSON from stdin with session_id, transcript_path, cwd, reason
Output: Copies transcript to .memory-bank/sessions/YYYY-MM-DD_HHmm_<short-id>.jsonl
"""

import json
import shutil
import sys
from datetime import datetime
from pathlib import Path


def find_project_root(start_path: Path) -> Path:
    """Find project root by looking for .memory-bank folder."""
    current = start_path.resolve()
    while current != current.parent:
        if (current / ".memory-bank").exists():
            return current
        current = current.parent
    # Fallback: if no .memory-bank found, use current directory
    return start_path.resolve()


def main():
    # Read JSON from stdin
    try:
        data = json.load(sys.stdin)
    except json.JSONDecodeError as e:
        print(f"Failed to parse JSON input: {e}", file=sys.stderr)
        sys.exit(1)

    session_id = data.get("session_id", "unknown")
    transcript_path = Path(data.get("transcript_path", ""))
    reason = data.get("reason", "unknown")
    cwd = Path(data.get("cwd", "."))

    # Find project root
    project_root = find_project_root(cwd)
    destination_dir = project_root / ".memory-bank" / "sessions"
    destination_dir.mkdir(parents=True, exist_ok=True)

    # Verify transcript exists
    if not transcript_path.exists():
        print(f"Transcript file not found: {transcript_path}", file=sys.stderr)
        sys.exit(1)

    # Generate filename: YYYY-MM-DD_HHmm_<short-id>.jsonl
    timestamp = datetime.now().strftime("%Y-%m-%d_%H%M")
    short_id = session_id[:8] if len(session_id) >= 8 else session_id
    filename = f"{timestamp}_{short_id}.jsonl"
    destination_path = destination_dir / filename

    # Copy the transcript
    try:
        shutil.copy2(transcript_path, destination_path)
        print(f"Session archived to {project_root.name}/.memory-bank/sessions/{filename} (reason: {reason})")
    except Exception as e:
        print(f"Failed to copy transcript: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
