# Export Results

**Status**: implemented
**Release**: v0.5.1 (completed), v0.4.0 (backend), v0.5.0 (normalized files UI)
**Priority**: must-have
**Depends on**: foundation-layer.md, settings-configuration.md

## Implementation Status (2026-01-18)

**Backend implemented. Normalized files export complete. Comparison results export UI pending.**

| Component | Status | Notes |
|-----------|--------|-------|
| `IExcelWriterService` | Done | Interface in Core/Application/Interfaces |
| `ExcelWriterService` | Done | Implementation in Infrastructure/External/Writers |
| `ExportResult` DTO | Done | Success/failure with statistics |
| `ExcelExportOptions` | Done | Headers, auto-fit, freeze row, use original values |
| `CsvExportOptions` | Done | Delimiter, encoding, BOM, date format |
| Export normalized files (backend) | Done | Service ready |
| Export normalized files (UI) | **Done** | UI connected in v0.5.0 (FileDetailsView) |
| Export comparison results (backend) | Done | Service ready |
| Export comparison results (UI) | Not done | **Pending for v0.5.1** |
| Menu "Export Results" | Disabled | Still `IsEnabled="False"` in MainWindow.axaml |

**Next steps (v0.5.1)**: Connect ExcelWriterService to comparison results, add export button in RowComparisonView.

## Summary

Export search/comparison results and processed files, with format based on context: reports use configured format, processed files preserve input format.

## User Stories

- As a user, I want to export search results to share with colleagues
- As a user, I want comparison results in my preferred format
- As a user, I want normalized files in the same format as the original

## Export Logic

| What | Output Format | Naming |
|------|---------------|--------|
| Comparison results | User preference from Settings | `{timestamp}_{keywords}.{ext}` |
| Search results | User preference from Settings | `{timestamp}_{keyword}.{ext}` (nice-to-have) |
| Normalized files | Same as input | Pattern from Settings |
| Templatized files | Same as input | Pattern from Settings |

### File Naming Details

**Normalized files** (`normalized/` folder):
- Naming pattern configurable in Settings (see settings-configuration spec)
- Presets: `{filename}.{ext}`, `{date}_{filename}.{ext}`, etc.
- Rationale: user may want to directly replace original file

**Comparison files** (`comparison/` folder):
- Format: `{YYYY-MM-DD}_{HHmm}_{keyword1}_{keyword2}.{ext}`
- Keywords from the search queries that generated the compared data
- Timestamp important: user may export multiple comparisons in quick succession
- Example: `2025-11-30_1523_revenue_expenses.xlsx`

**Search results** (`search/` folder) - nice-to-have:
- Format: `{YYYY-MM-DD}_{HHmm}_{keyword}.{ext}`
- Example: `2025-11-30_1520_fatturato.csv`

## Requirements

### Functional

#### Exporters (backend done, UI pending)
- [x] **CSV Exporter** (WriteToCsvAsync in ExcelWriterService)
  - [ ] ~~Write search results to CSV~~ UI not connected
  - [ ] ~~Write comparison results to CSV~~ UI not connected
  - [x] Write normalized data to CSV (service ready)
- [x] **Excel Exporter (.xlsx)** (WriteToExcelAsync in ExcelWriterService)
  - [ ] ~~Write search results with basic formatting~~ UI not connected
  - [ ] ~~Write comparison results with conditional formatting~~ UI not connected
  - [x] Write normalized data preserving types (service ready)
- [ ] **Legacy Excel Exporter (.xls)** - for input compatibility
  - [ ] Write normalized data in .xls format (if input was .xls)

#### Export UI
- [ ] Export button/menu for comparison results
- [ ] "Save normalized" action for processed files
- [ ] Uses default output folder from Settings
- [ ] Uses default format from Settings (for reports)
- [ ] Uses naming pattern from Settings (for normalized)
- [ ] Export button for search results (nice-to-have)

#### Report Content
- [ ] Search results columns: FileName, SheetName, CellAddress, Value, Context
- [ ] Comparison results: Side-by-side values, match status, statistics summary

#### Data Preservation
- [ ] **Formula preservation**
  - [ ] Read both calculated value AND formula from source cells
  - [ ] Use calculated value for search/compare operations
  - [ ] Write original formula (not calculated value) when exporting
  - [ ] If cell has no formula, write the value
- [ ] **Macro preservation** (nice-to-have)
  - [ ] Copy VBA storage from source to destination file
  - [ ] Only relevant for .xlsm/.xls output formats
  - [ ] SheetAtlas does not execute macros, only preserves them

### Non-Functional
- Performance: <5 seconds for typical results
- Quality: Excel preserves cell types (dates as dates, numbers as numbers)
- **Non-destructive**: exported files preserve all original information (formulas, formatting)

## Technical Notes

- Use DocumentFormat.OpenXml for .xlsx writing
- Use NPOI or similar for .xls writing (same library used for reading)
- CSV: standard RFC 4180 format with proper escaping
- Reuse Foundation Layer's NormalizationResult for type preservation

## PDF Export (Experimental - Deferred)

PDF export is **deferred** pending experimentation:
- Concern: tabular data may not look good in PDF
- Need to prototype and evaluate before committing
- Possible approaches:
  - Summary report with key stats (not full data)
  - Visual diff representation
  - HTML-to-PDF conversion
- Decision: revisit after core exporters are done

## Acceptance Criteria

- [ ] CSV export works for search and comparison results
- [ ] Excel export preserves types and adds formatting
- [ ] Normalized files saved in same format as input
- [ ] Default folder and format from Settings respected
- [ ] Large exports don't freeze UI (async with progress)
