# Decision 010: DataRegion UI Pattern

**Date**: February 2026
**Status**: Proposed
**Impact**: high
**Related**: data-region-selection spec, ADR-009 (data model)
**Summary**: Region selection via interactive canvas (required, not optional). Dedicated Regions Sidebar for selection. Per-file display, no automatic grouping of similar regions.

## Context

The DataRegion feature needs UI for:

1. Selecting/defining regions within a sheet
2. Choosing which regions to use for operations (search, compare)
3. Managing regions across multiple files

During architecture review, several UI questions arose about the best interaction patterns.

## Decision

### 1. Interactive Canvas (Required)

Region selection is done via an interactive canvas showing a preview of the sheet. Users click-and-drag to select rectangular areas.

**Not optional**: The original design had canvas as "Phase 6 - Optional". This has been changed to **required for Phase 2**.

**Alternatives considered**:

| Approach | Pro | Contra |
| -------- | --- | ------ |
| Manual input ("A1:F100") | Simple to implement | Tedious, error-prone, anti-UX for desktop app |
| Canvas only | Intuitive, visual | Complex to implement |
| Both (canvas + manual) | Flexibility | More UI surface to maintain |

**Why canvas required**: Users of a desktop application expect visual interaction. Typing cell coordinates manually is tedious and error-prone. The snap-to-merged-cells feature (ADR-009) only works with visual selection.

**Canvas features**:

- Sheet grid preview (headers + sample rows)
- Click-and-drag selection
- Visual highlight of selected region
- Dimmed area outside selection
- Coordinates display (e.g., "A1:F100")
- Snap-to-merged-cells (cannot select partial merged cell)

### 2. Dedicated Regions Sidebar

A new sidebar (third, alongside Files and Columns) displays all defined regions and allows selection.

```
┌─────────────────┬──────────────────┬──────────────────┬────────────┐
│  FILES          │  COLUMNS         │  REGIONS         │  DETAILS   │
│  (sidebar)      │  (sidebar)       │  (NEW sidebar)   │  (main)    │
├─────────────────┼──────────────────┼──────────────────┼────────────┤
│  ☑ vendite_23   │  ☑ ProductID     │  vendite_23:     │            │
│  ☑ vendite_24   │  ☑ ProductName   │    ☑ Sales Data  │            │
│                 │                  │    ☐ Summary     │            │
│                 │                  │  vendite_24:     │            │
│                 │                  │    ☑ Sales Data  │            │
└─────────────────┴──────────────────┴──────────────────┴────────────┘
```

**Why a sidebar**: Consistent with existing UI patterns (Files sidebar, Columns sidebar). Allows multi-select. Always visible, no modal dialogs.

### 3. Per-File Display, No Grouping

Regions are displayed grouped by file, not merged by name.

**Not like Columns**: The Columns sidebar groups columns with the same name across files ("ProductID" appears once even if in 3 files). The Regions sidebar does NOT do this.

**Why no grouping**:

Columns group well because identity is semantic (name). Regions have physical bounds that vary per file:

```
File A: "Sales Data" = A1:D24
File B: "Sales Data" = A1:D28

If grouped, selecting "Sales Data" is ambiguous.
Rows 25-28 in File B might be empty or part of another region.
```

**Display**:

```
Regions Sidebar:
├─ vendite_2023.xlsx
│   ├─ ☑ Sales Data (A1:D24)
│   └─ ☐ Summary (A26:D30)
├─ vendite_2024.xlsx
│   ├─ ☑ Sales Data (A1:D28)
│   └─ ☐ Summary (A30:D35)
```

**Trade-off acknowledged**: User must select regions manually for each file. Mitigated by "Apply to similar files" action for bulk operations.

## Rationale

- **Canvas**: Desktop app UX expectations. Visual selection is intuitive.
- **Sidebar**: Consistent with existing patterns. Multi-select. Non-modal.
- **No grouping**: Regions have bounds, bounds vary. Explicit is safer than implicit.

## Consequences

- Canvas implementation is required (higher initial effort)
- New UI component: RegionsSidebarView
- Search/Compare operations use selected regions from sidebar
- "Apply to similar files" becomes important UX feature
- More verbose UI (regions listed per-file) but less ambiguous

## Performance Considerations

Canvas with large sheets (100k+ rows):

- Show only sample rows initially (first N rows)
- Consider virtualization if performance issues arise
- Zoom controls for navigation

## Related Decisions

- See `data-region-selection-progress.md` for full discussion (Q11, Q12, Q13)
- ADR-009: Data model (no ActiveRegion)
- `data-region-selection-mockups.md` for visual designs
