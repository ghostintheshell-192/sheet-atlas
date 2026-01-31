# Foundation Layer

**Status**: implemented
**Release**: v0.3.x (già presente)
**Priority**: must-have
**Depends on**: file-loading.md

## Summary

Unified data infrastructure providing normalization, column analysis, and type detection - the foundation for all advanced features.

## User Stories

- As a user, I want search to find "1,234.56" when I search "1234.56"
- As a user, I want dates in different formats to be recognized as dates
- As a user, I want the app to detect column types automatically

## Requirements

### Functional

- [x] **Data Normalization Service** (`DataNormalizationService.cs`, 410 lines)
  - [x] Date normalization (serial numbers 1900/1904, leap year bug handling, various string formats)
  - [x] Number normalization (locale-aware, currency symbols, percentage, scientific notation)
  - [x] Text cleaning (whitespace trim, zero-width chars, control chars, line ending normalization)
  - [x] Boolean detection ("Yes"/"No", "true"/"false", checkmarks ✓✗ - intentionally excludes "1"/"0")

- [x] **Column Analysis Service** (`ColumnAnalysisService.cs`, 402 lines)
  - [x] Type detection per column with confidence scoring
  - [x] Type distribution calculation
  - [x] Context-aware anomaly detection (±3 cells window)
  - [x] Formula error detection (#REF!, #VALUE!, etc.)
  - [x] Currency integration (delegates to CurrencyDetector)

- [x] **Currency Detector** (`CurrencyDetector.cs`, 358 lines)
  - [x] Extract from Excel number formats (pattern [$€-407])
  - [x] Locale-to-currency mapping (EUR, USD, GBP, JPY, etc.)
  - [x] Symbol detection with confidence levels
  - [x] Position detection (prefix/suffix)
  - [x] Decimal/thousand separator handling (including European locales)
  - [x] Mixed currency detection across column

- [x] **Merged Cell Resolver** (`MergedCellResolver.cs`, 243 lines) - BONUS
  - [x] Multiple strategies: ExpandValue, KeepTopLeft, FlattenToString, TreatAsHeader
  - [x] Complexity analysis (Simple/Complex/Chaos)
  - [x] Warning callbacks for high merge density

### Non-Functional

- [x] Performance: negligible overhead on file load
- [x] Accuracy: >95% correct type detection (based on test suite)

## Technical Notes

- Design doc: `archive/legacy/foundation-layer-design.md`
- All services registered in DI container
- Stateless services for thread safety

## Acceptance Criteria

- [x] Search accuracy improved (normalized values searchable)
- [x] Column types detected correctly for test files
- [x] No regression in load performance

## Future Enhancements (v0.4.1)

- [ ] Quality metrics aggregation (null percentage, outlier count per column)
- [ ] Statistical summary per column (min, max, mean for numeric)
