---
name: session-handoff
description: Use when ending a session, switching projects, before /exit or /clear, or when the user asks for a summary of what was done. Also activate when the user says goodbye or requests a handoff/summary in their native language.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Session Handoff

When this skill is activated, create handoff notes for the current session.

## What to do

### 1. Create a new handoff file

**Location**: `.memory-bank/`

Each session gets its own file. Create a new file with this naming convention:

**Filename format**: `YYYY-MM-DD-HHmm-brief-title-slug.md`

- `YYYY-MM-DD` = today's date
- `HHmm` = current time (24h format, no colon for filesystem compatibility)
- `brief-title-slug` = lowercase, hyphen-separated summary (max 50 chars)

**Examples**:
- `2026-01-21-2330-release-v0-5-1.md`
- `2026-01-19-1430-fix-export-bug.md`
- `2025-12-10-0915-add-settings-page.md`

### 2. File content structure

```markdown
## YYYY-MM-DD - Brief title

**Done**:
- What was completed
- Important files modified
- Decisions made

**Next**:
- Suggested next steps
- Blockers identified

**Notes**:
- Useful context for next session
- Gotchas to remember
```

### 3. Content guidelines

- Write content in the **user's preferred language** (check user profile or recent messages)
- Keep field names in English (Done/Next/Notes) for consistency
- Max 5-7 bullet points per section
- Include specific file paths when relevant
- Be concise but complete

### 4. Confirm to the user

After creating the file, confirm:
- Which handoff file was created
- The filename used
- Remind the user to type `/exit` or `/clear` to complete

## Example

If you worked on sheet-atlas:

1. Create `.memory-bank/2026-01-21-2330-release-v0-5-1.md`
2. Confirm: "Created handoff notes: `2026-01-21-2330-release-v0-5-1.md`. You can exit with /exit."

## Reading previous sessions

When starting a new session, to get context:
1. List files in `.memory-bank/`
2. Read the most recent 1-3 handoff files (sorted by filename = sorted by date)
3. Skip the `_archive-*` files unless deep history is needed

## Localized triggers

This skill should also activate when the user expresses intent to end the session or requests a summary **in their native language**. Common patterns include:

- "I'm about to leave" / "I'm done for today"
- "Give me a summary" / "What did we do?"
- "Let's wrap up" / "Closing the session"
- Saying goodbye in any form

Recognize these phrases in whatever language the user speaks.
