# Data Region Selection - UI Mockups

**Status**: in-progress
**Related**: data-region-selection.md, data-region-selection-progress.md
**Last updated**: 2026-02-03

---

## Overview

UI mockups per la feature DataRegion selection. Questi mockups sono **work in progress** e non definitivi.

Focus su:

- Region selection workflow
- Similarity detection feedback
- Apply to similar files dialog
- Template integration UI

---

## 1. File Details View - Region Selection

```text
┌────────────────────────────────────────────────────────────┐
│ File Details - vendite_2023.xlsx               Sheet: Data │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌─ SHEET PREVIEW ──────────────────────────────────────┐  │
│  │                                                      │  │
│  │  [Sheet Grid Canvas - Interactive]                   │  │
│  │  ┌─────┬─────┬─────┬─────┬─────┬─────┐               │  │
│  │  │  A  │  B  │  C  │  D  │  E  │  F  │  ← Headers    │  │
│  │  ├─────┼─────┼─────┼─────┼─────┼─────┤               │  │
│  │  │ ▓▓▓ │▓▓▓▓ │▓▓▓▓ │▓▓▓▓ │     │     │               │  │
│  │  │ ▓▓▓ │▓▓▓▓ │▓▓▓▓ │▓▓▓▓ │     │     │  ← Selected   │  │
│  │  │ ▓▓▓ │▓▓▓▓ │▓▓▓▓ │▓▓▓▓ │     │     │     region    │  │
│  │  │ ▓▓▓ │▓▓▓▓ │▓▓▓▓ │▓▓▓▓ │     │     │     (A1:D50)  │  │
│  │  │ ... │ ... │ ... │ ... │     │     │               │  │
│  │  └─────┴─────┴─────┴─────┴─────┴─────┘               │  │
│  │                                                      │  │
│  │  [Zoom: 100%] [Fit to width] [Show gridlines: ✓]     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                            │
│  ┌─ SELECTED REGION ────────────────────────────────────┐  │
│  │ Region: A1:D50                                       │  │
│  │ Size: 50 rows × 4 columns = 200 cells                │  │
│  │                                                      │  │
│  │ 🟢 Data appears homogeneous (analyzing...)           │  │
│  │                                                      │  │
│  │ [ Clear Selection ]  [ Apply to Similar Files... ]   │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                            │
└────────────────────────────────────────────────────────────┘

Interaction:
- Click and drag on grid to select rectangular region
- Visual highlight with border and shading
- Real-time display of coordinates and size
- Background analysis starts on selection (debounced 300ms)
```

---

## 2. Files Sidebar - Similarity Feedback (Expandable)

```text
┌────────────────────────────────────────┐
│ FILES                                  │
├────────────────────────────────────────┤
│                                        │
│ ☑ vendite_2023.xlsx               [▼]  │  ← Selected + expanded
│   ├─ Data regions: 1                   │
│   ├─ Current region: 50 × 4 (A1:D50)   │
│   └─ Similarity: 100% (self)           │
│                                        │
│ ☑ vendite_2024.xlsx  🟢 97%       [▼]  │  ← Similar (highlighted) + checked
│   ├─ Data regions: 1                   │
│   ├─ Current region: 48 × 4 (A1:D48)   │
│   └─ Similarity: 97%                   │
│                                        │
│ ☑ vendite_2025.xlsx  🟢 95%       [▶] │  ← Similar (collapsed) + checked
│                                        │
│ ☐ vendite_Q1.xlsx    🟡 82%       [▶] │  ← Medium match (collapsed) + unchecked
│                                        │
│ ○ report_annual.xlsx              [▶] │  ← No similarity (empty expandable)
│                                        │
├────────────────────────────────────────┤
│ ✓ 3 files selected                     │
│ [ Apply Region to Selected Files ]     │
└────────────────────────────────────────┘

Expanded medium-match file (vendite_Q1.xlsx):
┌────────────────────────────────────────┐
│ ☐ vendite_Q1.xlsx    🟡 82%       [▼]  │
│   ├─ Data regions: 1                   │
│   ├─ Current region: 45 × 4 (A1:D45)   │
│   └─ Similarity: 82%                   │
└────────────────────────────────────────┘

Expanded non-similar file (report_annual.xlsx):
┌────────────────────────────────────────┐
│ ○ report_annual.xlsx              [▼]  │
│   (no data regions defined)            │
└────────────────────────────────────────┘

Legend:
● = Selected file (always expanded by default)
◉ = Similar file (highlighted with accent color)
○ = Regular file (no highlight)
☑ = Checkbox checked (will apply region)
☐ = Checkbox unchecked
🟢 = High match (95-100%) - auto-checked
🟡 = Medium match (80-94%) - unchecked by default
🔴 = Low match (< 80%) - unchecked by default
[▼] = Expanded
[▶] = Collapsed

Behavior:
- Click file name → select file (switch to its FileDetailsView)
- Click expand arrow [▶]/[▼] → toggle expansion (show/hide details)
- Click checkbox → toggle selection for "Apply to Selected" action
- High-match files (🟢 95%+) are auto-checked by default
- Medium/Low-match files are unchecked by default
- Selected file shows "100% (self)" similarity
- Files without similarity show empty expandable or no expand button
```

---

## 3. Apply Region Action (No Dialog - Sidebar Only)

**Design Decision**: No separate dialog. All interaction happens in Files Sidebar (see section 2).

**Workflow**:

1. User selects region in File 1
2. Sidebar highlights similar files with badges (🟢🟡)
3. User expands files to see details (region size, similarity %)
4. User checks/unchecks files via checkboxes
5. User clicks "Apply Region to Selected Files" button (bottom of sidebar)
6. Quick confirmation snackbar: "Region applied to 2 files ✓"

**Warnings and Type Issues**:

Detailed warnings (type mismatches, column name differences, etc.) appear in:

- **FileDetailsView → NOTIFICATIONS tab** (for each affected file)
- NOT in sidebar (keeps sidebar clean and structural)

Example log entry for vendite_Q1.xlsx (82% match):

```text
FileDetailsView → NOTIFICATIONS AND ERRORS tab:

⚠️  DataRegion Similarity: Medium Match (82%)
    Source: vendite_2023.xlsx (A1:D50)

    Issues detected:
    - Column "ProductID" mapped to "ID" (name differs)
    - Column "Price": Type mismatch (Text vs Currency)
    - Recommendation: Review data before applying region

    [View Details] [Dismiss]
```

**Advantages of no-dialog approach**:

- Cleaner UI (no popup)
- Reuses existing Files Sidebar pattern
- Progressive disclosure (expand only what you need)
- Warnings go to proper place (Notifications tab)
- Less context switching for user

---

## 4. Template Editor - With DataRegion

```text
┌─────────────────────────────────────────────────────────────┐
│ Template: Monthly Sales Report                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ ┌─ DATA REGION SCOPE ─────────────────────────────────-─┐   │
│ │                                                       │   │
│ │ Region: Sales Data                                    │   │
│ │ Bounds: A1:F100                                       │   │
│ │                                                       │   │
│ │ Structure:                                            │   │
│ │  ├─ Headers: Row 1                                    │   │
│ │  ├─ Data: Rows 2-100                                  │   │
│ │  └─ Columns: A-F (6 total)                            │   │
│ │                                                       │   │
│ │ [ Edit Region... ]  [ Clear Region ]                  │   │
│ │                                                       │   │
│ └───────────────────────────────────────────────────────┘   │
│                                                             │
│ ─────────────────────────────────────────────────────────── │
│                                                             │
│ Expected Columns (within region):                           │
│                                                             │
│  [A] ProductID      Number     Required  [Edit] [Remove]    │
│  [B] ProductName    Text       Required  [Edit] [Remove]    │
│  [C] Category       Text       Optional  [Edit] [Remove]    │
│  [D] Price          Currency   Required  [Edit] [Remove]    │
│  [E] Quantity       Number     Optional  [Edit] [Remove]    │
│  [F] Total          Currency   Optional  [Edit] [Remove]    │
│                                                             │
│  [ + Add Column ]                                           │
│                                                             │
│ ─────────────────────────────────────────────────────────── │
│                                                             │
│ ℹ️  Validation will check ONLY cells within the defined     │
│    DataRegion. Cells outside this region are ignored.       │
│                                                             │
│ ─────────────────────────────────────────────────────────── │
│                                                             │
│ Description:                                                │
│ [Template for monthly sales reports with product data...]   │
│                                                             │
│                              [Cancel]  [Save Template]      │
│                                                             │
└─────────────────────────────────────────────────────────────┘

Key changes from current template UI:
- Added "DATA REGION SCOPE" section at top
- Shows region name + bounds + structure
- Edit/Clear actions for region
- Info message about validation scope
- Column positions (A, B, C...) are RELATIVE to region
  (if region starts at C, then [A] = column C of sheet)
```

---

## 5. Template Validation Results - With Region

```text
┌─────────────────────────────────────────────────────────────┐
│ Validation Results                                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ Template: Monthly Sales Report                              │
│ File: vendite_2024.xlsx                                     │
│                                                             │
│ ┌─ DATAREGION VALIDATION ──────────────────────────────┐   │
│ │                                                       │   │
│ │ Template Region: A1:F100 (Sales Data)                 │   │
│ │ File Region:     A1:F48  (Applied)                    │   │
│ │                                                       │   │
│ │ ✓ Region bounds: OK (within file bounds)             │   │
│ │ ✓ Headers found: Row 1                                │   │
│ │ ✓ Data rows: 47 (template expects up to 99)          │   │
│ │                                                       │   │
│ └───────────────────────────────────────────────────────┘   │
│                                                             │
│ ┌─ COLUMN VALIDATION (within region) ──────────────────┐   │
│ │                                                       │   │
│ │ ✓ ProductID    (A) - Number   - Found, type matches  │   │
│ │ ✓ ProductName  (B) - Text     - Found, type matches  │   │
│ │ ✓ Category     (C) - Text     - Found, type matches  │   │
│ │ ✓ Price        (D) - Currency - Found, type matches  │   │
│ │ ✓ Quantity     (E) - Number   - Found, type matches  │   │
│ │ ✓ Total        (F) - Currency - Found, type matches  │   │
│ │                                                       │   │
│ └───────────────────────────────────────────────────────┘   │
│                                                             │
│ ┌─ SUMMARY ────────────────────────────────────────────┐   │
│ │                                                       │   │
│ │ Status: ✓ VALID                                       │   │
│ │                                                       │   │
│ │ Columns: 6/6 match (100%)                            │   │
│ │ Issues: 0 errors, 0 warnings                         │   │
│ │                                                       │   │
│ │ ℹ️  Cells outside region A1:F100 were not validated. │   │
│ │                                                       │   │
│ └───────────────────────────────────────────────────────┘   │
│                                                             │
│                                        [Close]  [Export]    │
│                                                             │
└─────────────────────────────────────────────────────────────┘

Note:
- Validation report now includes DataRegion section
- Shows region bounds match/mismatch
- Clarifies that validation is scoped to region
- Info message about cells outside region
```

---

## 6. Background Analysis Progress

```text
┌────────────────────────────────────────┐
│ FILES                                  │
├────────────────────────────────────────┤
│                                        │
│ ● vendite_2023.xlsx               [▼]  │  ← Selected
│   ├─ Data regions: 1                   │
│   ├─ Current region: 50 × 4 (A1:D50)   │
│   ├─ Similarity: 100% (self)           │
│   └─ Analyzing types... ⏳             │  ← Analysis indicator
│                                        │
│ ◌ vendite_2024.xlsx  [━━━━━░░] 60%     │  ← Analysis in progress
│                                        │
│ ◌ vendite_2025.xlsx  [━━░░░░░░] 30%    │  ← Analysis in progress
│                                        │
│ ○ vendite_Q1.xlsx    Queued...         │  ← Waiting for analysis
│                                        │
│ ○ report_annual.xlsx                   │  ← Not analyzed (different structure)
│                                        │
└────────────────────────────────────────┘

After analysis completes:
┌────────────────────────────────────────┐
│ ● vendite_2023.xlsx               [▼]  │
│   ├─ Data regions: 1                   │
│   ├─ Current region: 50 × 4 (A1:D50)   │
│   └─ Similarity: 100% (self)           │
│                                        │
│ ☑ vendite_2024.xlsx  🟢 97%       [▶]  │  ← Analysis done, high match
│                                        │
│ ☑ vendite_2025.xlsx  🟢 95%       [▶]  │  ← Analysis done, high match
│                                        │
│ ☐ vendite_Q1.xlsx    🟡 82%       [▶]  │  ← Analysis done, medium match
│                                        │
│ ○ report_annual.xlsx              [▶]  │  ← No similarity
│                                        │
├────────────────────────────────────────┤
│ ✓ 2 files selected                     │
│ [ Apply Region to Selected Files ]     │
└────────────────────────────────────────┘

Behavior:
- Spinner (⏳) on source file while analyzing
- Progress bars on target files during analysis
- Real-time update of similarity % as analysis completes
- Auto-check high-match files (🟢 95%+) when analysis done
- Debounce rapid region changes (300ms)
- Cancel pending analyses if user changes region selection
```

---

## 7. Region List (Multiple Regions - Fase 3)

```text
┌────────────────────────────────────────────────────────────┐
│ File: complex_report.xlsx                      Sheet: Data │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ ┌─ DEFINED REGIONS ────────────────────────────────────┐   │
│ │                                                      │   │
│ │ ● Sales Data          A1:F100   (active)             │   │
│ │   └─ 99 rows × 6 cols                                │   │
│ │                                                      │   │
│ │ ○ Inventory Summary   H1:K50                         │   │
│ │   └─ 49 rows × 4 cols                                │   │
│ │                                                      │   │
│ │ ○ Financial Notes     A105:D120                      │   │
│ │   └─ 15 rows × 4 cols                                │   │
│ │                                                      │   │
│ │ [ + Add Region ]                                     │   │
│ │                                                      │   │
│ └──────────────────────────────────────────────────────┘   │
│                                                            │
│ [Sheet preview shows all regions with different colors]    │
│                                                            │
└────────────────────────────────────────────────────────────┘

Note: Multiple regions per sheet - Fase 3 only
```

---

## Design Notes

### Visual Hierarchy

1. **Primary action**: Region selection (canvas interaction)
2. **Secondary action**: Apply to similar files
3. **Tertiary actions**: Edit region, clear, create template

### Color Coding

- **Selected region**: Primary accent color (orange) with transparency
- **High similarity** (🟢): Success color (green)
- **Medium similarity** (🟡): Warning color (yellow/orange)
- **Low similarity** (🔴): Error/danger color (red)
- **Active region**: Highlighted border

### Feedback & Transparency

- Real-time similarity updates (as analysis completes)
- Progress indicators for background tasks
- Detailed tooltips with match breakdown
- Preview before applying (show what will happen)
- Clear visual distinction between similar/non-similar files

### Performance Considerations

- Debounce region selection (300ms) before triggering analysis
- Show progress for analyses taking > 500ms
- Cancel pending analyses on selection change
- Cache analysis results per region bounds

---

*Mockups are work in progress and subject to change based on implementation feedback.*
