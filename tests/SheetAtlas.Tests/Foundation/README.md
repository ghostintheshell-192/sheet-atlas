# Foundation Layer Test Infrastructure

**Created**: 2025-11-05
**Status**: Complete - Ready for TDD Implementation
**Framework**: xUnit + Moq + FluentAssertions
**Test Coverage Target**: >80% for all services

## Overview

This test infrastructure provides comprehensive test suites for the Foundation Layer services. Tests are organized following TDD principles and will initially fail with `NotImplementedException` as the services are implemented.

## Directory Structure

```
Foundation/
├── Services/
│   ├── CurrencyDetectorTests.cs           # 60+ tests
│   ├── DataNormalizationServiceTests.cs   # 70+ tests
│   ├── ColumnAnalysisServiceTests.cs      # 50+ tests
│   └── MergedCellResolverTests.cs         # 55+ tests
├── Fixtures/
│   ├── SampleDataFixture.cs               # Real sample file loading
│   └── FoundationLayerFixture.cs          # Common test utilities
├── Builders/
│   ├── SASheetDataBuilder.cs              # Fluent builder for sheets
│   ├── SACellDataBuilder.cs               # Fluent builder for cells
│   └── ColumnMetadataBuilder.cs           # Fluent builder for metadata
├── TestUtilities/
│   └── ExcelFormatStrings.cs              # Real Excel format strings
└── README.md                              # This file
```

## Test Files

### 1. CurrencyDetectorTests.cs
Tests for `ICurrencyDetector` service - detecting currencies from Excel number formats.

**Test Categories**:
- Euro currency detection (German, French, Italian, Spanish, Portuguese)
- US Dollar detection (US, Canada, Australia, etc.)
- British Pound detection
- Japanese Yen detection (including no-decimal formats)
- Ambiguous currency detection (low confidence $)
- Non-currency format rejection (General, %, dates)
- Mixed currency detection in samples
- Decimal places detection
- Thousand separator detection
- Locale code extraction
- Edge cases and unusual formats

**Key Tests**: 50+
- `DetectCurrency_EuroGermanFormat_ReturnsEUR`
- `DetectCurrency_USDFormat_ReturnsUSD`
- `DetectCurrency_AmbiguousDollar_LowConfidence`
- `DetectMixedCurrencies_MultipleFormats_ReturnsAllDistinct`

### 2. DataNormalizationServiceTests.cs
Tests for `IDataNormalizationService` - normalizing dates, numbers, text, and booleans.

**Test Categories**:
- Date normalization (Excel serial, ISO, US, EU formats)
- Number normalization (US/EU formats, thousand separators, scientific notation)
- Text normalization (whitespace, zero-width chars, BOMs, line endings)
- Boolean normalization (Yes/No, 1/0, TRUE/FALSE, X/blank, symbols)
- Batch normalization with context
- Error handling (invalid dates, invalid numbers, null handling)
- Data quality issue detection
- Currency value parsing
- Percentage parsing
- Edge cases (very large/small numbers, whitespace-only)

**Key Tests**: 70+
- `Normalize_ExcelSerialDate_ParsesCorrectly`
- `Normalize_ISODateString_ParsesCorrectly`
- `Normalize_USNumberFormats_ParsesCorrectly`
- `Normalize_EuropeanNumberFormat_ParsesCorrectly`
- `Normalize_TextWithZeroWidthCharacters_CleansCorrectly`
- `Normalize_BooleanVariations_AllMapToTrueFalse`
- `NormalizeBatch_MultipleValues_NormalizesAll`

### 3. ColumnAnalysisServiceTests.cs
Tests for `IColumnAnalysisService` - detecting column types and headers.

**Test Categories**:
- Column type detection (Number, Date, Currency, Text, Boolean)
- Confidence scoring (high/low confidence results)
- Header detection (single row, multi-row, no headers)
- Type distribution analysis
- Sample value inclusion
- Quality warning counting
- Mixed format detection
- Edge cases (empty sample, single cell, all nulls)

**Key Tests**: 50+
- `AnalyzeColumn_UniformNumericData_HighConfidence`
- `AnalyzeColumn_CurrencyColumn_PopulatesCurrencyInfo`
- `AnalyzeColumn_MixedTypes_LowConfidence`
- `DetectHeaders_SingleHeaderRow_ReturnsCorrectIndex`
- `DetectHeaders_MultiRowHeaders_ReturnsAllIndices`
- `AnalyzeColumn_MixedDateFormats_DetectsDateType`
- `AnalyzeColumn_WeakMixedData_LowConfidence`

### 4. MergedCellResolverTests.cs
Tests for `IMergedCellResolver` - handling merged cells with various strategies.

**Test Categories**:
- Simple header merge resolution
- Strategy testing (ExpandValue, KeepTopLeft, FlattenToString, TreatAsHeader)
- Horizontal and vertical merges
- Complexity analysis (Simple/Complex/Chaos levels)
- Warning generation and callbacks
- Multiple merge handling
- Overlapping merge edge cases
- Empty merge handling
- Immutability verification
- Full workflow (analyze then resolve)

**Key Tests**: 55+
- `ResolveMergedCells_SimpleHeaderMerge_ExpandsValue`
- `ResolveMergedCells_KeepTopLeftStrategy_OnlyTopLeftHasValue`
- `AnalyzeMergeComplexity_SimpleHeaderMerge_ReturnsSimple`
- `AnalyzeMergeComplexity_Over20PercentMerged_ReturnsChaos`
- `ResolveMergedCells_WithWarningCallback_ReportsWarnings`
- `ResolveMergedCells_MultipleMerges_HandlesAllCorrectly`

## Fixtures and Builders

### SampleDataFixture
Loads real test data from `samples/real-data/`:
```csharp
var fixture = new SampleDataFixture();
string[] lines = fixture.ReadCsvLines(fixture.GetMixedDatesFilePath());
List<Dictionary<string, string>> records = fixture.ReadCsvAsRecords(filePath);
```

**Available Files**:
- `test-mixed-dates.csv` - Mixed date formats
- `test-currency-issues.csv` - Currency symbols and formats
- `test-boolean-variations.csv` - Boolean value variations
- `test-whitespace-issues.csv` - Whitespace and zero-width chars
- `test-all-problems.csv` - Comprehensive data issues
- `test-clean-data.csv` - Well-formed reference data

### FoundationLayerFixture
Common test utilities and assertions:
```csharp
var sheet = FoundationLayerFixture.CreateTestSheet("Data", 10, 5);
var sheet = FoundationLayerFixture.CreateSheetWithMergedCells("Merged", "A1:C1");
var formats = FoundationLayerFixture.CreateSampleExcelFormats();
```

### SASheetDataBuilder
Fluent builder for test sheet creation:
```csharp
var sheet = new SASheetDataBuilder()
    .WithName("TestSheet")
    .WithColumns("Name", "Amount", "Date")
    .WithRows(5)
    .WithCellValue(0, 1, 1234.56m)
    .WithMergedCells("A1:C1", "Header")
    .Build();
```

### ColumnMetadataBuilder
Fluent builder for metadata:
```csharp
var metadata = new ColumnMetadataBuilder()
    .WithDetectedType(DataType.Currency)
    .WithTypeConfidence(0.95)
    .WithCurrency(CurrencyInfo.EUR)
    .Build();
```

### ExcelFormatStrings
Comprehensive library of real Excel format strings organized by currency and locale:
```csharp
ExcelFormatStrings.Euro.German        // "[$€-407] #,##0.00"
ExcelFormatStrings.Dollar.AmericanEnglish  // "[$$-409] #,##0.00"
ExcelFormatStrings.Yen.Japanese       // "[$¥-411] #,##0"
ExcelFormatStrings.Numeric.TwoDecimals // "0.00"
ExcelFormatStrings.Date.ShortDate     // "mm/dd/yyyy"
```

## Running Tests

### Run All Foundation Tests
```bash
cd /data/repos/sheet-atlas
dotnet test tests/SheetAtlas.Tests/Foundation/ -v normal
```

### Run Specific Test File
```bash
dotnet test tests/SheetAtlas.Tests/Foundation/Services/CurrencyDetectorTests.cs -v normal
```

### Run with Coverage
```bash
dotnet test tests/SheetAtlas.Tests/ /p:CollectCoverage=true /p:CoverageFormat=opencover
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~CurrencyDetectorTests.DetectCurrency_EuroGermanFormat_ReturnsEUR"
```

## Test Patterns Used

### AAA Pattern
All tests follow Arrange-Act-Assert:
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var input = ...;

    // Act
    var result = service.Method(input);

    // Assert
    result.Should().Be(expected);
}
```

### Theory vs Fact
- `[Fact]`: Single test case
- `[Theory]` + `[InlineData]`: Parametrized test with multiple cases

### FluentAssertions
Readable assertion syntax:
```csharp
result.Should().NotBeNull();
result.Code.Should().Be("EUR");
result.DetectedType.Should().Be(DataType.Currency);
result.Should().HaveCount(3);
```

## Expected Test Results

### Initial State (Before Implementation)
All tests will **FAIL** with `NotImplementedException` as services are stubs.

```
Test Run Summary
================
Total Tests: 235+
Passed: 0
Failed: 235+
Skipped: 0
```

### Implementation Sequence
1. **CurrencyDetector** (Days 1-2)
   - Implement regex parsing for Excel format strings
   - Build currency symbol → ISO code lookup
   - Handle locale detection
   - ~50 tests should pass

2. **DataNormalizationService** (Days 3-5)
   - Date parsing (serial, string formats, ISO, localized)
   - Number parsing (separators, scientific notation, currency)
   - Text cleaning (whitespace, zero-width chars)
   - Boolean mapping (all variations)
   - ~70 tests should pass

3. **ColumnAnalysisService** (Days 6-8)
   - Type detection algorithm (sampling, confidence)
   - Header detection (row analysis)
   - Metadata generation
   - ~50 tests should pass

4. **MergedCellResolver** (Days 9-10)
   - Merge strategy implementations
   - Complexity analysis
   - Warning generation
   - ~55 tests should pass

## Success Criteria

When all services are implemented:
- All 235+ tests should **PASS**
- Coverage should be **>80%** for all services
- No skipped tests
- All edge cases handled gracefully
- No unhandled exceptions

## Notes for Implementers

### Currency Detection
- Start with regex pattern for format parsing: `\[\$(.)-(\d+)\]`
- Build locale → currency code mapping
- Handle ambiguous symbols ($, ¥, kr) with confidence levels
- Real data: `samples/real-data/test-currency-issues.csv`

### Data Normalization
- Use `DateTime.TryParseExact()` with multiple format patterns
- Handle Excel serial date (1899-12-30 epoch for Windows, 1904-01-01 for Mac)
- Strip currency symbols and thousand separators before numeric parsing
- Detect and remove zero-width characters (U+200B, U+FEFF)

### Column Analysis
- Sample ~100 cells to balance accuracy and performance
- Confidence = Primary Type Count / (Total - Empty Cells)
- Header detection: look for row where data pattern stabilizes
- Use ColumnAnalysisResult DTO to return rich metadata

### Merged Cell Resolution
- Strategy pattern: ExpandValue (replicate), KeepTopLeft (original structure), FlattenToString (concatenate), TreatAsHeader (header rows)
- Complexity = merged cells percentage
  - 0-5%: Simple
  - 5-20%: Complex
  - >20%: Chaos (suggest export values)
- Return new SASheetData (immutable pattern)

## Future Enhancements

1. **Performance Testing**: Add benchmarks for large files (10MB+)
2. **Integration Tests**: Test with real Excel files from `samples/`
3. **Stress Testing**: Test with extreme edge cases (1M rows, 100+ columns)
4. **Localization**: Add more locale-specific format patterns
5. **Formula Support**: Test formula cached values and dependencies

## References

- **Design Doc**: `.personal/strategy/foundation-layer-design.md`
- **API Design**: `.personal/work-in-progress/foundation-layer-api-design.md`
- **Implementation Plan**: `.personal/work-in-progress/foundation-layer-tasks.md`
- **Project Standards**: `/data/repos/CLAUDE.md`

---

**Total Test Coverage**: 235+ comprehensive tests
**Framework**: xUnit 2.6.x + Moq 4.x + FluentAssertions 6.x
**Status**: Ready for implementation phase
**Last Updated**: 2025-11-05
