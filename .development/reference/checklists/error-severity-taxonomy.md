# Error Severity Taxonomy - File Loading

This document defines the severity levels for errors encountered during Excel file loading operations.

## Severity Levels Overview

| Level | Icon | Badge Color | Load Status | UI Behavior |
|-------|------|-------------|-------------|-------------|
| CRITICAL | ⬢ Red | Red | Failed | File not loaded, show error |
| ERROR | ⬢ Orange | Orange | PartialSuccess | Some sheets OK, some failed |
| WARNING | △ Yellow | Yellow | Success | File loaded with workarounds |
| INFO | ○ Green | None/Blue | Success | Normal operation, no issues |

---

## 🔴 CRITICAL - File Completely Unusable

**Definition**: The file cannot be loaded at all. No data is accessible.

**Icon**: ⬢ Red hexagon
**Badge**: "CRITICAL" in red
**Load Status**: `LoadStatus.Failed`
**UI Stack Panel**: Show error in expandable list

### Cases

1. **Corrupted file format**
   - Invalid ZIP structure (.xlsx)
   - Corrupted binary format (.xls)
   - File is not a valid Excel format

2. **File system errors**
   - File not found
   - Access denied (permissions)
   - File locked by another process
   - Disk I/O errors

3. **Critical OpenXML errors**
   - Workbook part missing
   - Document structure completely invalid

### Example Messages
```
CRITICAL: Corrupted or invalid .xlsx file - File contains corrupted data
CRITICAL: Cannot access file - Permission denied
CRITICAL: File not found at specified path
```

---

## 🟠 ERROR - Serious Problem, Partial Data Available

**Definition**: Significant data loss or corruption, but some sheets/data may still be usable.

**Icon**: ⬢ Orange hexagon
**Badge**: "ERROR" in orange
**Load Status**: `LoadStatus.PartialSuccess`
**UI Stack Panel**: Show error in expandable list

### Cases

1. **Sheet-level corruption**
   - Sheet contains corrupted cells (data loss)
   - Invalid sheet structure in OpenXML
   - Formula parsing errors that prevent data reading

2. **Encoding/data integrity issues**
   - Encoding problems causing illegible text
   - Data type mismatches causing parsing failures
   - Missing critical worksheet parts (OpenXML)

3. **Structural problems**
   - Invalid cell references in merged cells
   - Broken relationships between sheets
   - Missing shared string table entries (OpenXML)

### Example Messages
```
ERROR: Sheet 'Data' corrupted - Invalid cell references
ERROR: Sheet 'Sales' - Formula parsing failed, data may be incomplete
ERROR: Invalid sheet structure - Unable to read cell data
```

---

## 🟡 WARNING - Anomaly, But Fully Recoverable

**Definition**: Something unusual detected, but handled gracefully with workarounds. No data loss.

**Icon**: △ Yellow triangle
**Badge**: "WARNING" in yellow
**Load Status**: `LoadStatus.Success` (or `PartialSuccess` if sheet skipped)
**UI Stack Panel**: Show warning in expandable list

### Cases

1. **Sheet naming issues**
   - Sheet with empty/null name → Skipped
   - Duplicate sheet names → Auto-renamed with suffix

2. **Legacy format limitations**
   - Merged cells not fully supported in .xls → Flattened
   - Formula not calculated → Show raw formula text
   - Missing features in legacy format

3. **Data normalization**
   - Duplicate column headers → Auto-renamed ("Name" → "Name_2")
   - Mixed data types in column → Converted to text
   - Whitespace trimming applied

4. **Optional features missing**
   - Missing worksheet part (non-critical sheet) → Sheet skipped
   - Invalid chart/image → Chart skipped, data preserved

### Example Messages
```
WARNING: Sheet with empty name skipped
WARNING: Duplicate column 'Name' renamed to 'Name_2'
WARNING: Merged cells in legacy .xls file - using first cell value only
WARNING: Formula not calculated, displaying raw formula
```

---

## 🔵 INFO - Normal Operation, No Issues

**Definition**: Informational messages about normal file processing. Not a problem.

**Icon**: ○ Green circle
**Badge**: None (or "INFO" in blue, if displayed)
**Load Status**: `LoadStatus.Success`
**UI Stack Panel**: **NOT SHOWN** (filtered out)

### Cases

1. **Empty sheets (expected)**
   - Sheet with 0 rows → Skipped
   - Sheet with 0 columns (no header) → Skipped
   - CSV file with no data rows → Empty file loaded

2. **Auto-generated metadata**
   - Auto-generated column names ("Column_0", "Column_1") for missing headers
   - Default sheet name assigned for unnamed sheets
   - Whitespace normalized in cell values

3. **Processing notifications**
   - File loaded successfully with N sheets
   - Data normalization applied (e.g., trimmed strings)
   - Standard conversions applied

### Example Messages
```
INFO: Sheet 'EmptySheet' is empty and was skipped
INFO: CSV file has no header row, using auto-generated column names
INFO: File loaded successfully with 3 sheets, 1 empty sheet skipped
INFO: Whitespace normalized in cell values
```

---

## UI Behavior Rules

### Icon Selection (File List)

Based on `ExcelFile.HasErrors`, `HasWarnings`, `HasCriticalErrors`:

```csharp
if (HasErrors || HasCriticalErrors)
    Icon = ⬢ Red;   // Critical or Error present
else if (HasWarnings)
    Icon = △ Yellow; // Only warnings
else
    Icon = ○ Green;  // Success (includes Info-only)
```

### Badge Display (File List)

Show count of **errors and warnings only** (INFO excluded):

```csharp
var visibleErrorCount = Errors.Count(e =>
    e.Level == LogSeverity.Critical ||
    e.Level == LogSeverity.Error ||
    e.Level == LogSeverity.Warning);

if (visibleErrorCount > 0)
    Badge.Text = visibleErrorCount.ToString();
```

### Expandable Stack Panel Filter

Only show **Critical, Error, Warning**:

```csharp
var visibleErrors = Errors.Where(e =>
    e.Level == LogSeverity.Critical ||
    e.Level == LogSeverity.Error ||
    e.Level == LogSeverity.Warning);
```

INFO messages are:
- ✅ Logged to file (`%AppData%/SheetAtlas/Logs/app-{date}.log`)
- ✅ Available via `ILogService.GetMessages()`
- ❌ **NOT shown** in file panel expandable error list

---

## Implementation Checklist

### Core Domain
- [x] `ExcelError.Info()` factory method exists
- [x] `ExcelError` supports `LogSeverity.Info`
- [ ] Update `ExcelFile.HasErrors` to exclude Info
- [ ] Update `ExcelFile.HasWarnings` to exclude Info

### Readers (Infrastructure)
- [ ] XlsFileReader: Empty sheet → Info (not Error)
- [ ] OpenXmlFileReader: Empty sheet → Info (not Warning)
- [ ] CsvFileReader: Empty CSV → Info (not Warning)
- [ ] All readers: Consistent severity assignments

### UI Layer
- [ ] FileLoadResultViewModel: Filter Info from `Errors` property
- [ ] MainWindow.axaml: Badge count excludes Info
- [ ] MainWindow.axaml: Stack panel filters Info
- [ ] Icon logic: Only Critical/Error → Red

### Logging
- [x] LogService writes all levels to file
- [ ] Log format improved for readability
- [ ] INFO messages clearly distinguished in log

---

## Example Scenarios

### Scenario 1: Perfect File
```
File: sales_2024.xlsx
Sheets: 3 (Sales, Customers, Products)
Result: ○ Green, no badge, no errors
LoadStatus: Success
```

### Scenario 2: Empty Sheet (Normal)
```
File: report.xlsx
Sheets: 2 (Data [100 rows], Notes [empty])
Result: ○ Green, no badge
Info logged: "Sheet 'Notes' is empty and was skipped"
LoadStatus: Success
```

### Scenario 3: Duplicate Headers (Auto-fixed)
```
File: data.csv
Issue: Two columns named "Name"
Result: △ Yellow, badge "(1)"
Warning: "Duplicate column 'Name' renamed to 'Name_2'"
LoadStatus: Success
```

### Scenario 4: Corrupted Sheet
```
File: financial.xlsx
Issue: Sheet 'Q1' corrupted, Sheet 'Q2' OK
Result: ⬢ Orange, badge "(1)"
Error: "Sheet 'Q1' corrupted - Invalid cell references"
LoadStatus: PartialSuccess
```

### Scenario 5: File Not Found
```
File: missing.xlsx
Issue: File doesn't exist
Result: ⬢ Red, badge "(1)"
Critical: "Cannot access file - File not found"
LoadStatus: Failed
```

---

## Decision Log

**Date**: 2025-10-17
**Decision**: INFO level should not be shown in UI error panels
**Rationale**: Empty sheets and auto-generated column names are normal operations, not problems. Showing them clutters the UI and creates unnecessary alarm for users. INFO messages are still logged for debugging purposes.

**Impact**:
- Cleaner UI - only real problems shown
- Green ○ icon remains for files with Info-only messages
- Badge count excludes Info messages
- Better user experience - less false positives

---

**Last Updated**: 2025-10-17
**Version**: 1.0
**Status**: Approved for implementation
