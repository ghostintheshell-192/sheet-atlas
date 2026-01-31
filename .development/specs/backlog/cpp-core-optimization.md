# C++ Core Optimization

**Status**: backlog
**Release**: unassigned (post-v1.0, major effort)
**Priority**: nice-to-have
**Depends on**: foundation-layer.md (stable API first)

## Summary

Rewrite performance-critical core components in C++ for significant speed and memory improvements, especially for large files.

## User Stories

- As a user, I want to process 100MB+ files without slowdown
- As a user, I want instant search results even on large datasets
- As a user, I want minimal memory footprint

## Requirements

### Functional
- [ ] Identify Components to Rewrite
  - [ ] File parsing (xlsx, xls, csv, ods)
  - [ ] Search engine
  - [ ] Comparison algorithms
  - [ ] Data normalization

- [ ] C++ Implementation
  - [ ] Cross-platform (Windows, Linux, macOS)
  - [ ] Native library with C# interop (P/Invoke or C++/CLI)
  - [ ] Memory-efficient data structures
  - [ ] SIMD optimizations where applicable

- [ ] Integration
  - [ ] Seamless integration with existing C# UI
  - [ ] Fallback to C# implementation if native fails
  - [ ] Same API surface for callers

### Non-Functional
- Performance: 5-10x improvement for large files
- Memory: 50% reduction for large datasets
- Stability: no crashes, proper error handling across boundary

## Technical Notes

- Existing PoC notes: `ideas/design/cpp-core-poc/README.md`
- Consider: Rust as alternative to C++ (memory safety)
- Build system: CMake for cross-platform
- Interop options: P/Invoke, C++/CLI, or separate process with IPC

## Open Questions

- [ ] C++ or Rust?
- [ ] Which components give best ROI?
- [ ] How to handle cross-platform builds in CI?
- [ ] Gradual migration or big bang?

## Notes

**Post v1.0 investigation** — This feature is primarily a learning opportunity to explore mixed managed/unmanaged codebases (C#/C++ interop via P/Invoke or C++/CLI). Performance gains are secondary to the educational value. Before starting:

1. Profile the app to identify actual bottlenecks
2. Try pure C# optimizations first (Span<T>, SIMD, NativeAOT)
3. Pick ONE small component for a proof-of-concept (e.g., CSV parsing)
4. Document the interop patterns learned for future reference

## Acceptance Criteria

- [ ] Measurable performance improvement (benchmarks)
- [ ] No functional regressions
- [ ] Works on all three platforms
- [ ] Memory usage reduced for large files
