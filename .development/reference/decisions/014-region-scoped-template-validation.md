# Decision 014: Region-Scoped Template Validation

**Date**: March 2026
**Status**: Accepted
**Impact**: moderate
**Related**: ADR-009 (DataRegion data model), ADR-011 (DataRegion persistence)
**Summary**: Template validation and row comparison services accept an optional `regionName` parameter to scope operations to a DataRegion's bounds. The UI integration is intentionally deferred.

## Context

DataRegions (ADR-009) allow users to define named rectangular areas within complex sheets — sheets that contain multiple tables, mixed content, or non-standard layouts. Template validation and row comparison currently operate on the entire sheet, using the sheet's global header row count and all columns.

When a sheet contains multiple distinct tables (e.g., "Switch Layout" in columns B-F and "Switch Connections" in columns J-L), creating a template from the full sheet produces a template that mixes unrelated columns. Validating against such a template is meaningless.

## Decision

### Backend: add optional `regionName` to service interfaces

All methods in `ITemplateValidationService` and `IRowComparisonService` gain an optional `string? regionName = null` parameter. When provided:

- **Column mapping** is limited to `region.StartColumn..region.EndColumn`
- **Data row enumeration** uses `sheet.EnumerateDataRows(region)` instead of the full sheet
- **Header boundary** uses `region.DataStartRow` instead of `sheet.HeaderRowCount`
- **Template creation** uses `region.HeaderRowCount` and only includes region columns

When `regionName` is null or the region is not found, behavior is unchanged (full sheet).

### UI integration: intentionally deferred

No region selector is added to the Templates tab. Reasons:

1. **Feature maturity**: Templates are still in alpha. Adding region selection increases UI complexity for a flow that few users will need before v1.0.
2. **Niche-within-niche**: DataRegions are already a power-user feature for complex sheets. Region-scoped templates are a subset of that subset.
3. **Cost/benefit**: The UI work (ComboBox, conditional state, empty-state handling) is non-trivial relative to the current user base.
4. **Backend readiness has no cost**: Optional parameters with defaults don't affect existing callers. The implementation is tested and available when the UI catches up.

### When to revisit

Add the UI integration when:
- Users explicitly request region-scoped template validation
- The Templates feature matures beyond alpha (post v1.0 roadmap)
- A concrete workflow emerges that requires it (e.g., batch validation of multi-table sheets)

## Consequences

- **Positive**: Backend is ready for immediate use by future UI or programmatic callers. No refactoring needed later.
- **Positive**: 9 tests cover region scoping, column filtering, header boundary, and fallback behavior.
- **Negative**: The capability exists but is not discoverable by users. Acceptable given the current project phase.
