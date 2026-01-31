# Date System Implementation Guide

**Date**: 2025-11-06
**Status**: Implementation Complete - Ready for DataNormalizationService

---

## Overview

Excel date handling now supports both 1900 and 1904 date systems. The workbook's date system is detected during file load and stored in `ExcelFile.DateSystem`.

---

## Implementation Summary

### 1. DateSystem Enum
**File**: `src/SheetAtlas.Core/Domain/ValueObjects/DateSystem.cs`

```csharp
public enum DateSystem
{
    Date1900 = 0,  // Default: Windows Excel
    Date1904 = 1   // Historical: Mac Excel
}
```

### 2. ExcelFile Extension
**File**: `src/SheetAtlas.Core/Domain/Entities/ExcelFile.cs`

- Added property: `public DateSystem DateSystem { get; }`
- Updated constructor with optional parameter: `DateSystem dateSystem = DateSystem.Date1900`
- Backward compatible: existing code works without changes

### 3. Detection in OpenXmlFileReader
**File**: `src/SheetAtlas.Infrastructure/External/Readers/OpenXmlFileReader.cs`

**Detection method**:
```csharp
private DateSystem DetectDateSystem(WorkbookPart workbookPart)
{
    var workbookProperties = workbookPart.Workbook.WorkbookProperties;

    if (workbookProperties?.Date1904?.Value == true)
        return DateSystem.Date1904;

    return DateSystem.Date1900; // Default
}
```

**Integration**: Called during `ReadAsync()`, result passed to ExcelFile constructor.

---

## Date Serial Number Conversion

### Conversion Formulas

**1900 System**:
```csharp
DateTime ConvertSerial1900(double serial)
{
    // Epoch: December 30, 1899 (serial 1 = Jan 1, 1900)
    var epoch = new DateTime(1899, 12, 30);
    return epoch.AddDays(serial);

    // IMPORTANT: Handle serial 60 edge case (Feb 29, 1900 doesn't exist)
    // Excel bug: treats 1900 as leap year for Lotus 1-2-3 compatibility
}
```

**1904 System**:
```csharp
DateTime ConvertSerial1904(double serial)
{
    // Epoch: January 1, 1904
    var epoch = new DateTime(1904, 1, 1);
    return epoch.AddDays(serial);
}
```

### Key Differences

| Aspect | 1900 System | 1904 System |
|--------|------------|-------------|
| Epoch | Dec 30, 1899 (serial=1 is Jan 1, 1900) | Jan 1, 1904 (serial=0) |
| Same date serial | N | N - 1,462 |
| Leap year bug | Yes (treats 1900 as leap year) | No |
| Serial 60 | Feb 29, 1900 (invalid date) | Mar 2, 1904 |
| Default for | Windows Excel | Old Mac Excel |

### Conversion Offset

Serial numbers differ by **1,462 days** (4 years + 1 leap day):
```
July 5, 2011:
- 1900 system: serial 40729
- 1904 system: serial 39267
- Difference: 40729 - 39267 = 1462
```

---

## Implementation for DataNormalizationService

### Interface Update (Recommended)

Add `dateSystem` parameter to normalization methods that handle dates:

```csharp
public interface IDataNormalizationService
{
    NormalizedValue Normalize(
        SACellValue cellValue,
        DataType expectedType,
        DateSystem dateSystem);  // NEW parameter

    // Existing methods...
}
```

### Implementation Pattern

```csharp
public NormalizedValue Normalize(
    SACellValue cellValue,
    DataType expectedType,
    DateSystem dateSystem)
{
    if (expectedType == DataType.Date && cellValue.ValueType == CellValueType.Number)
    {
        // Convert Excel serial number to DateTime
        var serial = Convert.ToDouble(cellValue.RawValue);

        DateTime result = dateSystem == DateSystem.Date1904
            ? ConvertSerial1904(serial)
            : ConvertSerial1900(serial);

        return NormalizedValue.Success(result);
    }

    // ... other normalizations
}
```

### Helper Methods

```csharp
private static readonly DateTime Epoch1900 = new DateTime(1899, 12, 30);
private static readonly DateTime Epoch1904 = new DateTime(1904, 1, 1);

private DateTime ConvertSerial1900(double serial)
{
    // Handle Feb 29, 1900 bug (serial 60)
    if (serial == 60.0)
    {
        // Return Feb 28 or Mar 1? Decision needed.
        // Excel displays "Feb 29, 1900" but date didn't exist.
        return new DateTime(1900, 3, 1); // Skip non-existent date
    }

    // Adjust for leap year bug: serials > 60 are off by 1
    if (serial > 60.0)
        serial -= 1;

    return Epoch1900.AddDays(serial);
}

private DateTime ConvertSerial1904(double serial)
{
    return Epoch1904.AddDays(serial);
}
```

---

## Usage Example

```csharp
// During file comparison workflow:
var fileA = await excelReaderService.LoadFileAsync("data.xlsx");
var fileB = await excelReaderService.LoadFileAsync("old_mac_file.xlsx");

// fileA.DateSystem → Date1900 (Windows)
// fileB.DateSystem → Date1904 (Old Mac)

// When normalizing dates from sheetA:
var normalizedDate = dataNormalizationService.Normalize(
    cellValue,
    DataType.Date,
    fileA.DateSystem);  // Pass detected system

// Comparison engine receives normalized DateTime values
// No 4-year offset issue!
```

---

## Testing Strategy

### Test Cases Needed

1. **1900 System Tests**:
   - Normal date: `serial 40729 → July 5, 2011`
   - Epoch: `serial 1 → January 1, 1900`
   - Pre-bug: `serial 59 → February 28, 1900`
   - Bug date: `serial 60 → ??? (Feb 29, 1900 invalid)`
   - Post-bug: `serial 61 → March 1, 1900`

2. **1904 System Tests**:
   - Normal date: `serial 39267 → July 5, 2011`
   - Epoch: `serial 0 → January 1, 1904`
   - Early date: `serial 60 → March 2, 1904`

3. **Offset Verification**:
   - Same DateTime from both systems
   - Verify 1,462 day difference

4. **Edge Cases**:
   - Fractional serials (time component)
   - Negative serials (invalid)
   - Very large serials (future dates)

### Sample Test Data

Create test Excel files:
- `test-dates-1900.xlsx` - Windows Excel with various dates
- `test-dates-1904.xlsx` - Mac Excel (1904 system) with same dates
- Verify both parse to same DateTime after normalization

---

## Migration Checklist

- [x] Create DateSystem enum
- [x] Add DateSystem property to ExcelFile
- [x] Implement detection in OpenXmlFileReader
- [x] Verify build without errors
- [ ] Update IDataNormalizationService interface
- [ ] Implement date serial conversion helpers
- [ ] Handle 1900 leap year bug (serial 60)
- [ ] Write comprehensive tests
- [ ] Create test Excel files (1900 and 1904)
- [ ] Document comparison workflow changes

---

## References

- **Microsoft Docs**: [WorkbookProperties.Date1904](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.workbookproperties.date1904)
- **Excel Quirk**: 1900 leap year bug exists for Lotus 1-2-3 compatibility
- **Offset Calculation**: 1,462 days = 4 years + 1 leap day (1900, 1904, 1908, 1912)

---

## Next Steps

1. Implement date conversion in DataNormalizationService
2. Add test cases for both date systems
3. Test with real 1904 Excel file (create if needed)
4. Update comparison engine to use normalized dates
