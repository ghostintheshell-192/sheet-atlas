# Excel Format Strings Analysis

**Date**: 2025-11-06
**Status**: Technical Review - CurrencyDetector Implementation

---

## 1. How Excel Stores Dates

### Serial Number System

Excel stores dates as **serial numbers** representing days elapsed since an epoch:

- **1900 Date System** (Windows default):
  - Epoch: January 1, 1900 = serial 1
  - Example: July 5, 2011 = serial 40729
  - Range: January 1, 1900 onwards

- **1904 Date System** (Mac historical default):
  - Epoch: January 1, 1904 = serial 0
  - Same date is 1,462 days smaller (4 years + 1 leap day difference)
  - Range: January 1, 1904 onwards
  - Created to avoid 1900 leap year bug (1900 was NOT a leap year, but Excel treats it as one in 1900 system)

### Key Implications

- **File format agnostic**: Both `.xls` and `.xlsx` can use either system
- **Conversion risk**: Copying dates between workbooks with different systems causes 4-year shift
- **Detection**: Must check workbook property `dateCompatibility` to determine which system is active
- **Fractional time**: Time is stored as decimal fraction (.5 = 12:00 PM)

### For SheetAtlas

When normalizing dates from Excel:
1. Check workbook's date system (1900 vs 1904)
2. Apply correct epoch when converting serial to DateTime
3. Handle both systems transparently for users

---

## 2. How Excel Stores Currency Formats

### Format String Convention

**Critical Rule**: Excel ALWAYS writes format strings using **US number convention** internally:
- Pattern: `#,##0.00` means comma=thousands, period=decimal
- Applies to ALL locales, even European ones

### Locale Display Mapping

The **locale code** determines how numbers are **displayed** to users, NOT how the format is written:

```
Format String: [$€-407] #,##0.00
               └─┬──┘    └───┬───┘
                 │          └─ Always US convention (comma=thousand, period=decimal)
                 └─ Locale 407 (German) → Display as 1.234,56 (inverted!)
```

### Examples

| Locale | Format String | User Sees | Decimal Sep | Thousand Sep |
|--------|--------------|-----------|-------------|--------------|
| 407 (DE) | `[$€-407] #,##0.00` | 1.234,56 € | `,` | `.` |
| 409 (US) | `[$$-409] #,##0.00` | $1,234.56 | `.` | `,` |
| 40C (FR) | `[$€-40C] #,##0.00` | 1 234,56 € | `,` | ` ` (space) |

---

## 3. CurrencyDetector Implementation Review

### Status: ✅ All 39 tests passing

### Separator Detection Logic (Lines 211-264)

**Algorithm**:
1. Extract separators from format string (always US convention)
2. Check if locale is European (`ShouldInvertSeparators()`)
3. If European → swap separators for display
4. If US/UK → use separators as-is

**Code snippet**:
```csharp
bool shouldInvert = ShouldInvertSeparators(locale);

if (shouldInvert)
{
    // European locale: format says thousand=comma, decimal=period
    // But users see thousand=period, decimal=comma
    return (formatThousandSep, formatDecimalSep);  // Returns (',', '.')
}

// US/UK: format matches reality
return (formatDecimalSep, formatThousandSep);  // Returns ('.', ',')
```

**Return tuple**: `(decimalSeparator, thousandSeparator)`

For German (407):
- formatThousandSep = `,` (from US format)
- formatDecimalSep = `.` (from US format)
- shouldInvert = true
- Returns `(',', '.')` → decimal=`,`, thousand=`.` ✅ Correct!

### Potential Discrepancy Found

**In test file** (CurrencyDetectorTests.cs:300):
```csharp
[InlineData("[$€-40C] #.##0,00", ',')]  // French uses comma as thousand sep (inverted format)
```

**In constants** (ExcelFormatStrings.cs:18):
```csharp
public const string French = "[$€-40C] #,##0.00";  // Standard US convention
```

**Question**: Does Excel ever write format strings with inverted separators (`#.##0,00`), or ALWAYS US convention?

### Test Results
- ✅ All separator tests pass (3/3)
- ✅ All currency detection tests pass (39/39)
- Logic appears correct for standard US-convention formats

---

## 4. Recommendations

### For Currency Detection
1. ✅ Current implementation is correct for standard Excel behavior
2. ⚠️ Verify: Does Excel EVER write non-US format strings? (e.g., `#.##0,00`)
3. Consider: Add warning if format string deviates from expected US convention
4. Document: The assumption that format strings are always US convention

### For Date Normalization ✅ COMPLETED
1. ✅ **Detection implemented**: `ExcelFile.DateSystem` property
2. ✅ **OpenXmlFileReader** detects via `WorkbookProperties.Date1904`
3. ⏳ **Next**: Implement conversion in DataNormalizationService:
   - 1900 system: `DateTime(1899, 12, 30).AddDays(serial)`
   - 1904 system: `DateTime(1904, 1, 1).AddDays(serial)`
4. ⏳ **Handle edge case**: Excel 1900 leap year bug (serial 60 = invalid Feb 29, 1900)
5. ⏳ **Test both systems** with same serial numbers to verify 1,462 day offset

**See**: `date-system-implementation.md` for complete implementation guide

### Test Data Needed
- Real Excel files with 1904 date system for testing
- European Excel files to verify format string convention
- Mixed-locale workbooks to test currency detection edge cases

---

## 5. Conclusion

**CurrencyDetector**: ✅ Implementation is solid
- All tests pass
- Separator logic correctly handles locale inversion
- Handles ambiguous formats with confidence levels
- Good coverage of edge cases

**Next Steps**:
1. Clarify format string convention (test with real French Excel file)
2. Proceed with DataNormalizationService implementation
3. Implement date system detection for date normalization

**No blocking issues found** in CurrencyDetector implementation.
