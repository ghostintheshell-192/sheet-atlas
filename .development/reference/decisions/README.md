# Decision Log

This folder contains documentation of significant architectural decisions made for the SheetAtlas project.

## Format

Each decision document follows this structure:

- **Date**: When the decision was made
- **Status**: Active, Superseded, Deprecated
- **Context**: Why was this decision needed?
- **Decision**: What was decided
- **Rationale**: Why this option was chosen
- **Consequences**: Implications and trade-offs

## Decisions

1. [Error Handling Philosophy](001-error-handling-philosophy.md) - Fail Fast vs Result Objects
2. [Row Indexing Semantics](002-row-indexing-semantics.md) - 0-based internal, 1-based display
3. [Technology Stack](003-technology-stack.md) - C# vs C++ for core
4. [Foundation Layer First](004-foundation-layer-first.md) - Infrastructure before features
5. [Security First](005-security-first.md) - Local processing, no cloud
6. [Git Workflow](006-git-workflow.md) - Branch strategy and release process
7. [Unified Data Flow for Export](007-unified-data-flow-for-export.md) - Single source of truth for normalized data
8. [Facade Pattern for Dependency Injection](008-facade-pattern-for-dependency-injection.md) - Reduce constructor over-injection
