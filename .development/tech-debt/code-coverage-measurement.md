---
type: testing
priority: low
status: open
discovered: 2025-10-08
related: []
---

# Code Coverage Measurement

## Problem

Currently relying on manual testing and existing unit tests without formal coverage metrics.

**Context**: Multi-format support feature completed (2025-10-08)

## Analysis

Current approach (manual + unit tests) has been effective - manual tests caught performance issues with 1M+ row files that unit tests wouldn't detect.

Formal coverage metrics would complement, not replace, manual testing.

## Possible Solutions

- **Option A**: Add code coverage tooling
  - Use Coverlet or similar
  - Generate coverage reports in CI/CD pipeline
  - Target: 85%+ coverage for core services
  - Pro: Data-driven quality metrics
  - Con: Setup effort, CI/CD integration

- **Option B**: Continue current approach
  - Manual testing + targeted unit tests
  - Pro: Working well, no overhead
  - Con: No visibility into coverage gaps

## Recommended Approach

**Option B** for now - current approach is effective.

Consider Option A if:
- Team grows beyond solo development
- Coverage gaps are suspected
- Quality issues emerge

## Notes

- Current test count: 350+ unit tests
- Manual testing catches issues that automation misses
- Not urgent for solo/alpha development
- Good candidate for "when we have time" improvement

## Status Verification (2025-11-28)

**Status**: Proposal is **STILL VALID, LOW PRIORITY**.

**Current test status**: Build shows 0 warnings, 0 errors - code quality is solid.

**Decision**: KEEP AS LOW PRIORITY

**Rationale**:
- Current approach (manual + unit tests) is working effectively
- Solo development - coverage metrics less critical than team scenarios
- Manual testing proven valuable (caught performance issues agents wouldn't detect)
- No quality issues emerging that would justify coverage tooling overhead

**Recommendation**: Defer to later stage (v1.0+ or when team grows). Current Option B (manual + targeted unit tests) remains appropriate for alpha/beta phase.

**Reconsider if**:
- Team expands beyond solo development
- Quality issues suggest coverage gaps
- Preparing for commercial release audit
