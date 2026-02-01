# Decision 004: Foundation Layer First

**Date**: November 2025
**Status**: Active
**Impact**: important
**Summary**: Build unified Foundation Layer (data normalization, column analysis, currency detection, merged cells) before feature implementations. Reduces code duplication, creates solid base for filtering, comparison, data quality features.

## Context

Multiple features (filtering, advanced comparison, data analysis) need common infrastructure. Building each feature independently would create technical debt.

## Decision

Build unified **Foundation Layer** infrastructure before feature-specific implementations.

Foundation components:
1. **Data Normalization Service** - Consistent string/number/date handling
2. **Column Analysis Service** - Type detection, statistics, anomaly detection
3. **Currency Detection** - Multi-currency value recognition
4. **Merged Cells Handler** - Consistent merged cell behavior

## Rationale

- Reduces code duplication across features
- Creates solid base for future features
- Improves overall data quality and consistency
- Investment pays off across multiple feature implementations

## Implementation Order

1. Foundation Layer (current sprint)
2. Column Filtering (uses Column Analysis)
3. Advanced Comparison (uses Data Normalization)
4. Data Quality Reports (uses all foundation services)

## Consequences

- Slower initial feature delivery (building infrastructure first)
- Faster subsequent feature delivery (infrastructure ready)
- More consistent behavior across all features
- Easier testing with centralized services
