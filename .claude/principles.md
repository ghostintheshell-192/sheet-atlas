# Development Principles

Core principles that apply to ALL coding projects.

---

## Core Philosophy

- **Functional minimalism**: Implement the minimum complexity necessary for current requirements
- **Incrementality**: Test each change before proceeding, implement one component at a time
- **Responsiveness as requirement**: A blocked interface makes the application non-functional
- **Effective simplicity**: Prefer the simplest solution that works
- **Reversibility**: Return to working versions when optimizations compromise functionality

## Critical Approach

- Critically evaluate and question questionable assumptions
- For ambiguous questions: identify the unclear part and ask for direct clarification
- Do not develop elaborate explanations for possible interpretations of the question

---

## Development Workflow

When developing code or debugging, always follow these general rules:

1. **Development**: Work on one bug, feature, or thematically coherent development at a time
2. **Build**: Compile and build the project to verify correctness
3. **User Testing**: Let the user test the changes from her perspective
4. **Atomic Commit**: Create a single, focused commit for the completed work

This cycle ensures:

- **Focus**: One logical change per iteration
- **Quality**: Immediate verification through build and test
- **Traceability**: Clean commit history with atomic, reversible changes
- **Reliability**: Each commit represents a working state

---

## Technology Compatibility

- Verify complete compatibility between proposed frameworks and libraries
- Explicitly list compatibility requirements before suggesting any library
- Confirm active support for third-party libraries with recent framework versions
- Compare alternatives highlighting compatibility advantages/disadvantages
- Report potential integration problems before implementation

---

## State and Lifecycle Management

- Clearly define the possible states of each component
- Document allowable state transitions
- Properly manage resource cleanup
- Implement appropriate state management patterns
- Avoid inconsistent states or race conditions

---

## Declarative Systems and UI Frameworks

- Consider not only WHAT happens but WHEN (timing of operations)
- The ORDER of evaluation is more important than static relationships
- Properties and bindings are evaluated at specific lifecycle phases
- Trace the complete temporal flow to diagnose dependency problems

---

## Scalability and Configuration

- Define security models for device access and credential management
- Establish versioning, validation and configuration backup processes
- Scale for different needs by identifying potential bottlenecks
- Plan optimization strategies based on measurable requirements, not hypothetical ones

---

*For language-specific standards, see `coding-standards/[language].md`*
*For security rules, see `core/security-boundaries.md`*
