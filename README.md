# SheetAtlas

A powerful cross-platform desktop application for searching across multiple Excel files and comparing rows between them. Built with .NET 8 and Avalonia UI for native performance on Windows, Linux, and macOS.

🌐 **[Visit the official website](https://ghostintheshell-192.github.io/sheet-atlas/)** | 📥 **[Download Latest Release](https://github.com/ghostintheshell-192/sheet-atlas/releases/latest)**

## Features

### **Excel File Support**

- Load multiple Excel files (.xlsx, .xls, .csv)
- Extract data from all sheets for searching
- Handle errors gracefully with detailed error reporting
- Support for merged cells and complex Excel structures

### **Advanced Search**

- Search across all loaded files and sheets
- Search in file names, sheet names, and cell content
- Support for case-sensitive, exact match, and regex patterns
- Tree-view results with file/sheet/cell organization

### **Row Comparison**

- Compare rows from different Excel files
- Intelligent column header mapping
- Visual highlighting of differences

### **User Experience**

- Modern, responsive interface with Fluent Design
- Light and dark theme support
- Cross-platform native performance
- Professional data visualization

## System Requirements

### Supported Operating Systems

- **Windows**: Windows 10 1903+ (x64, Arm64) - **Installer Available**
- **Linux**: Ubuntu 20.04+, Debian 11+ (x64, Arm64) - **Installer Available**
- **macOS**: macOS 10.15 Catalina+ (x64, Apple Silicon) - **Installer Available**

### Runtime Requirements

- .NET 8 Runtime (included in self-contained builds)
- Minimum 4 GB RAM recommended
- 100 MB free disk space

## Installation

### Download Pre-built Binaries

Visit the **[Releases page](https://github.com/ghostintheshell-192/sheet-atlas/releases/latest)** to download the latest version:

- **Windows**: Installer available (`.exe`)
- **Linux**: Tarball (`.tar.gz`) and Debian package (`.deb`)
- **macOS**: DMG installer (`.dmg`) for easy drag-and-drop installation

You can also [build from source](#build-from-source) if preferred.

### Build from Source

```bash
# Clone the repository
git clone https://github.com/ghostintheshell-192/sheet-atlas.git
cd sheet-atlas

# Build the application
dotnet build --configuration Release

# Run the application
dotnet run --project src/SheetAtlas.UI.Avalonia
```

## Quick Start

### Loading Files

1. Click **"Load File"** or use `Ctrl+O`
2. Select one or more Excel files (.xlsx, .xls, .csv)
3. Files appear in the left panel with status indicators

### Searching Content

1. Enter search terms in the search box
2. Choose search options (case-sensitive, regex, exact match)
3. View results organized by file → sheet → cell
4. Click any result to highlight it in the results tree

### Comparing Rows

1. Perform a search to find related data
2. Select multiple search results from different files
3. Click **"Compare Rows"** to create a comparison
4. View side-by-side differences with highlighting

## Usage Examples

### Basic File Operations

```text
1. Load multiple Excel files containing sales data
2. Search for "Q4 2024" across all files
3. Compare quarterly results between different regions
4. Identify differences with visual highlighting
```

### Data Analysis Workflow

```text
1. Load budget files from different departments
2. Search for specific cost categories
3. Create row comparisons to identify discrepancies
4. Review differences in the comparison view
```

## Architecture

SheetAtlas follows Clean Architecture principles:

- **Core Layer**: Business logic, domain entities, and interfaces
- **Infrastructure Layer**: Excel file processing using OpenXML
- **UI Layer**: Avalonia-based MVVM interface

### Key Technologies

- **.NET 8**: Modern framework with LTS support
- **Avalonia UI**: Cross-platform native UI framework
- **DocumentFormat.OpenXml**: Robust Excel file processing
- **Microsoft.Extensions**: Dependency injection and logging

## Development

### Building

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Run tests
dotnet test

# Create distribution package
dotnet publish --configuration Release --self-contained
```

### Project Structure

```text
SheetAtlas/
├── src/
│   ├── SheetAtlas.Core/           # Business logic
│   ├── SheetAtlas.Infrastructure/ # File processing
│   └── SheetAtlas.UI.Avalonia/   # User interface
├── tests/
│   └── SheetAtlas.Tests/         # Unit and integration tests
├── docs/                          # Documentation
└── build/                         # Build scripts
```

### Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

### Common Issues

**File won't load**: Check file permissions and ensure the Excel file isn't corrupted
**Search not working**: Verify search terms and check selected options
**Performance issues**: Close unused files and restart the application

### Getting Help

- Check the [Documentation](docs/)
- Report bugs via [Issues](https://github.com/ghostintheshell-192/sheet-atlas/issues)
- Ask questions in [Discussions](https://github.com/ghostintheshell-192/sheet-atlas/discussions)
- View release notes in [CHANGELOG.md](CHANGELOG.md)

## Roadmap

### Version 0.2.0 (Current Alpha)

- ✅ Support for .xlsx, .xls, .csv files
- ✅ Multi-file loading and cross-file search
- ✅ Advanced search with regex support
- ✅ Row comparison with visual highlighting
- ✅ Windows installer available (.exe)
- ✅ Linux packages available (.tar.gz, .deb)
- ✅ macOS installer available (.dmg)

### Upcoming Features

- [ ] Export search results to Excel
- [ ] Export comparison results
- [ ] Advanced filtering and sorting
- [ ] Chart and graph visualization
- [ ] Batch processing operations
- [ ] Plugin system for extensions

### Long-term Goals

- [ ] Web-based companion app
- [ ] Real-time collaboration features
- [ ] Integration with cloud storage
- [ ] Advanced analytics and reporting

---

**Made with ❤️ using .NET 8 and Avalonia UI**

*SheetAtlas is designed for professionals who need powerful Excel analysis tools with complete data privacy and offline processing.*
