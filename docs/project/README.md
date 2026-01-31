# SheetAtlas - Developer Hub

**Cross-platform Excel file comparison tool built with .NET 8 and Avalonia UI**

This guide provides everything you need to start developing SheetAtlas.

---

## 🚀 Quick Start

### Prerequisites

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **VSCode** (recommended) with extensions:
  - C# Dev Kit
  - Avalonia for VSCode
- **Linux users**: Install system libraries
  ```bash
  sudo apt install libx11-dev libice-dev libsm-dev libfontconfig1-dev
  ```

### Clone and Build

```bash
# Clone repository
git clone https://github.com/ghostintheshell-192/sheet-atlas.git
cd sheet-atlas

# Restore dependencies and build
dotnet restore
dotnet build

# Run the application
dotnet run --project src/SheetAtlas.UI.Avalonia/SheetAtlas.UI.Avalonia.csproj

# Run tests
dotnet test
```

---

## 📁 Project Structure

```text
SheetAtlas/
├── src/
│   ├── SheetAtlas.Core/                    # Core business logic (Clean Architecture)
│   │   ├── Application/                     # Application services & DTOs
│   │   ├── Domain/                          # Domain entities & value objects
│   │   └── Shared/                          # Shared utilities & extensions
│   ├── SheetAtlas.Infrastructure/          # Infrastructure layer
│   │   └── External/                        # File readers (Excel, CSV)
│   └── SheetAtlas.UI.Avalonia/             # Avalonia UI layer (MVVM)
│       ├── ViewModels/                      # MVVM ViewModels
│       ├── Views/                           # XAML Views
│       ├── Services/                        # UI-specific services
│       └── Converters/                      # XAML converters
├── tests/
│   └── SheetAtlas.Tests/                   # Unit and integration tests
├── docs/                                    # Documentation
│   ├── website/                             # GitHub Pages site
│   ├── project/                             # Developer docs (you are here)
│   └── development/                         # Design reviews and planning
├── build/                                   # Build scripts (installers)
├── .github/workflows/                       # CI/CD pipelines
├── CLAUDE.md                                # Development conventions
├── CHANGELOG.md                             # Version history
└── README.md                                # User-facing project overview
```

---

## 🎯 Architecture

### Technology Stack

- **Framework**: .NET 8 (LTS)
- **UI**: Avalonia UI 11+ (cross-platform native UI)
- **Architecture**: Clean Architecture + MVVM
- **File Processing**: DocumentFormat.OpenXml, ExcelDataReader, CsvHelper
- **Testing**: xUnit + Moq + FluentAssertions

### Core Principles

- **Security First**: 100% local processing, no cloud dependencies
- **Clean Architecture**: Separation of concerns, testable, maintainable
- **MVVM Pattern**: Clear separation between UI and business logic
- **Fail Fast**: Exceptions for bugs, Result objects for business errors

For architecture overview, see [ARCHITECTURE.md](ARCHITECTURE.md). For detailed specs, see [technical-specs.md](technical-specs.md).

---

## 🛠️ Development Workflow

### Coding Standards

Follow conventions in [CLAUDE.md](../../CLAUDE.md):
- **Naming**: PascalCase for classes/methods, camelCase for locals
- **Code Style**: Enforced via `dotnet format` and `.editorconfig`
- **Comments**: English only, explain "why" not "what"
- **Error Handling**: Never throw for business errors, use Result objects

### Git Workflow

**Always work on feature branches:**

```bash
# Start new feature (from develop)
git checkout develop
git pull origin develop
git checkout -b feature/your-feature-name

# Commit and push
git add .
git commit -m "feat: descriptive message"
git push -u origin feature/your-feature-name

# Merge when ready
git checkout develop
git merge feature/your-feature-name
git push origin develop
```

**Branch naming:**
- `feature/description` - New features
- `fix/description` - Bug fixes
- `refactor/description` - Code improvements
- `docs/description` - Documentation updates

### Testing

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "TestClassName"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Building Release

```bash
# Build release configuration
dotnet build --configuration Release

# Create platform-specific build
dotnet publish -c Release --self-contained -r win-x64
dotnet publish -c Release --self-contained -r linux-x64
dotnet publish -c Release --self-contained -r osx-x64
```

---

## 🐛 Troubleshooting

### Build Issues

**"SDK not found"**
```bash
dotnet --version  # Verify .NET 8 SDK installed
dotnet clean && dotnet restore && dotnet build
```

**"Project file could not be loaded"**
- Check `.csproj` files for errors
- Ensure all package references are resolvable

### Runtime Issues

**Application doesn't start on Linux**
```bash
# Install required system libraries
sudo apt install libx11-dev libice-dev libsm-dev libfontconfig1-dev
```

**Excel files won't load**
- Check file permissions
- Verify file is not corrupted
- See logs in `logs/` directory (created at runtime)

### Testing Issues

**Tests fail with "file not found"**
- Check test data paths in `tests/SheetAtlas.Tests/TestData/`
- Ensure working directory is project root

---

## 📚 Key Documentation

### Repository Root
- **[CLAUDE.md](../../CLAUDE.md)** - Complete development standards
- **[CHANGELOG.md](../../CHANGELOG.md)** - Auto-generated version history
- **[RELEASE_PROCESS.md](../RELEASE_PROCESS.md)** - Release workflow

### This Directory
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Architecture overview with diagrams
- **[technical-specs.md](technical-specs.md)** - Performance, security, config specs

### External Resources
- [Avalonia UI Docs](https://docs.avaloniaui.net/)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [DocumentFormat.OpenXml](https://docs.microsoft.com/en-us/office/open-xml/open-xml-sdk)

---

## 🚢 Release Process

Releases are automated via GitHub Actions:

1. **Create tag** (manual or via `release-changelog.yml` workflow)
2. **Pipeline builds** Windows/Linux/macOS installers automatically
3. **GitHub release** created with all artifacts
4. **Website updates** automatically with new version

See [RELEASE_PROCESS.md](../RELEASE_PROCESS.md) for complete details.

---

## 💡 Contributing

1. Fork the repository
2. Create feature branch from `develop`
3. Follow coding standards in [CLAUDE.md](../../CLAUDE.md)
4. Write tests for new features
5. Submit pull request to `develop` branch

---

*This developer hub provides everything needed to contribute to SheetAtlas. For development standards and conventions, see [CLAUDE.md](../../CLAUDE.md).*

**Last Updated**: January 2026 | **Project Status**: Alpha (v0.5.x)
