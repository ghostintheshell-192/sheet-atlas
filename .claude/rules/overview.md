# Project Overview

## SheetAtlas

**SheetAtlas** is an open-source cross-platform desktop application for comparing and analyzing Excel files with complete local processing.

- **Type**: Open-source (potential commercial licensing after v1.0)
- **Platform**: Cross-platform (.NET 8 + Avalonia UI)
- **Status**: Alpha (v0.5.x)

## Development Methodology

Based on `.rules/core/principles.md`:

- **Functional minimalism**: Minimum complexity for current requirements
- **Incrementality**: One component at a time, test before proceeding
- **Responsiveness**: Non-blocking UI is a requirement
- **Effective simplicity**: Simplest solution that works

## Architecture

- **Clean Architecture**: Core (domain) ← Infrastructure, UI → Core
- **MVVM Pattern**: UI/ViewModel separation
- **Dependency Injection**: Constructor injection, interface-based
- **Event-Driven**: Responsive UI with async operations

## Technology Stack

- **.NET 8** + **C# 12**
- **Avalonia UI** - Cross-platform native UI
- **DocumentFormat.OpenXml** - Excel file processing
- **Testing**: xUnit + Moq + FluentAssertions
- **CI/CD**: GitHub Actions
