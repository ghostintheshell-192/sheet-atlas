# Decision 003: Technology Stack - C# vs C++

**Date**: November 2025
**Status**: Active

## Context

Evaluated whether to migrate core file processing to C++ for performance. Estimated 8-11 weeks of investment.

## Decision

**KEEP C#** unless performance bottleneck is validated with real profiling data.

Current stack:
- **.NET 8** - Modern framework, LTS support
- **C# 12** - Latest language features
- **Avalonia UI** - Cross-platform native UI
- **DocumentFormat.OpenXml** - Excel file processing

## Rationale

1. **No proven bottleneck**: Current C# implementation handles target file sizes (10MB, <2s load time)
2. **Development velocity**: C# allows faster iteration
3. **Cross-platform**: Avalonia + .NET 8 already provides native performance
4. **Maintenance cost**: C++ interop adds complexity

## When to Reconsider

- If profiling shows >50% time in file parsing for production workloads
- If users report unacceptable performance on target file sizes
- If competitive pressure requires order-of-magnitude improvement

## Consequences

- Continue optimizing C# code (caching, lazy loading, parallel processing)
- Monitor performance metrics in production
- Keep C++ option documented in `ideas/design/cpp-core-poc/` for future reference
