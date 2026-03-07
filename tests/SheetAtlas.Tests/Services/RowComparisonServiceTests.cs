using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.Exceptions;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Infrastructure.External;
using SheetAtlas.Infrastructure.External.Readers;
using FluentAssertions;
using Moq;
using SheetAtlas.Logging.Services;
using SheetAtlas.Core.Configuration;
using Microsoft.Extensions.Options;
using DocumentFormat.OpenXml.Packaging;

namespace SheetAtlas.Tests.Services
{
    public class RowComparisonServiceTests
    {
        private readonly Mock<ILogService> _mockLogger;
        private readonly RowComparisonService _service;
        private static readonly string[] _stringArray = new[] { "Name", "Age", "City" };

        public RowComparisonServiceTests()
        {
            _mockLogger = new Mock<ILogService>();
            _service = new RowComparisonService(_mockLogger.Object);
        }

        [Fact]
        public void ExtractRowFromSearchResult_SheetNotFound_ThrowsComparisonException()
        {
            // Arrange
            var excelFile = CreateExcelFileWithSheet("ExistingSheet");
            var searchResult = new SearchResult(
                excelFile,
                "NonExistentSheet",
                0,
                0,
                "test value"
            );

            // Act & Assert
            _service.Invoking(s => s.ExtractRowFromSearchResult(searchResult))
                .Should().Throw<ComparisonException>()
                .Where(ex => ex.UserMessage.Contains("NonExistentSheet"))
                .Where(ex => ex.UserMessage.Contains("is not present"));
        }

        [Fact]
        public void ExtractRowFromSearchResult_NullSearchResult_ThrowsArgumentNullException()
        {
            // Act & Assert
            _service.Invoking(s => s.ExtractRowFromSearchResult(null!))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ExtractRowFromSearchResult_InvalidCellCoordinates_ThrowsArgumentException()
        {
            // Arrange
            var excelFile = CreateExcelFileWithSheet("Sheet1");
            var searchResult = new SearchResult(
                excelFile,
                "Sheet1",
                -1,
                -1,
                "test value"
            );

            // Act & Assert
            _service.Invoking(s => s.ExtractRowFromSearchResult(searchResult))
                .Should().Throw<ArgumentException>()
                .WithMessage("*does not represent a valid cell*");
        }

        [Fact]
        public void GetColumnHeaders_SheetNotFound_ThrowsComparisonException()
        {
            // Arrange
            var excelFile = CreateExcelFileWithSheet("ExistingSheet");

            // Act & Assert
            _service.Invoking(s => s.GetColumnHeaders(excelFile, "NonExistentSheet"))
                .Should().Throw<ComparisonException>()
                .Where(ex => ex.UserMessage.Contains("NonExistentSheet"));
        }

        [Fact]
        public void GetColumnHeaders_NullFile_ThrowsArgumentNullException()
        {
            // Act & Assert
            _service.Invoking(s => s.GetColumnHeaders(null!, "Sheet1"))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetColumnHeaders_ValidSheet_ReturnsColumnHeaders()
        {
            // Arrange
            var excelFile = CreateExcelFileWithSheet("Sheet1", _stringArray);

            // Act
            var headers = _service.GetColumnHeaders(excelFile, "Sheet1");

            // Assert
            headers.Should().NotBeNull();
            headers.Should().HaveCount(3);
            headers.Should().Contain("Name");
            headers.Should().Contain("Age");
            headers.Should().Contain("City");
        }

        [Fact]
        public void CreateRowComparison_LessThanTwoResults_ThrowsArgumentException()
        {
            // Arrange
            var excelFile = CreateExcelFileWithSheet("Sheet1");
            var searchResult = new SearchResult(
                excelFile,
                "Sheet1",
                0,
                0,
                "test value"
            );

            var request = new RowComparisonRequest(
                new List<SearchResult> { searchResult },
                name: "Comparison1"
            );

            // Act & Assert
            _service.Invoking(s => s.CreateRowComparison(request))
                .Should().Throw<ArgumentException>()
                .WithMessage("*At least two search results*");
        }

        #region Integration Tests with Real Files

        [Fact]
        public async Task GetColumnHeaders_RealSimpleFile_ReturnsCorrectHeaders()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var filePath = GetTestFilePath("Valid", "simple.xlsx");
            var excelFile = await excelReaderService.LoadFileAsync(filePath);

            // Act
            var headers = _service.GetColumnHeaders(excelFile, "Sheet1");

            // Assert
            headers.Should().NotBeNull();
            headers.Should().HaveCount(3);
            headers.Should().Contain("Name");
            headers.Should().Contain("Age");
            headers.Should().Contain("City");
        }

        [Fact]
        public async Task GetColumnHeaders_RealMultiSheetFile_ReturnsCorrectHeadersForEachSheet()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var filePath = GetTestFilePath("Valid", "multi-sheet.xlsx");
            var excelFile = await excelReaderService.LoadFileAsync(filePath);

            // Act - Test Employees sheet
            var employeeHeaders = _service.GetColumnHeaders(excelFile, "Employees");

            // Assert
            employeeHeaders.Should().HaveCount(2);
            employeeHeaders.Should().Contain("Employee");
            employeeHeaders.Should().Contain("Department");

            // Act - Test Departments sheet
            var departmentHeaders = _service.GetColumnHeaders(excelFile, "Departments");

            // Assert
            departmentHeaders.Should().HaveCount(2);
            departmentHeaders.Should().Contain("Department");
            departmentHeaders.Should().Contain("Budget");
        }

        [Fact]
        public async Task ExtractRowFromSearchResult_RealSimpleFile_ExtractsCorrectRow()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var filePath = GetTestFilePath("Valid", "simple.xlsx");
            var excelFile = await excelReaderService.LoadFileAsync(filePath);

            // Create a search result pointing to the first data row
            // Row index is ABSOLUTE 0-based (row 0 = header, row 1 = first data)
            var searchResult = new SearchResult(
                excelFile,
                "Sheet1",
                1,  // Row index (absolute 0-based, row 1 = Alice, first data row)
                0,  // Column index (0-based)
                "Alice"
            );

            // Act
            var extractedRow = _service.ExtractRowFromSearchResult(searchResult);

            // Assert
            extractedRow.Should().NotBeNull();
            extractedRow.Cells.Should().HaveCount(3);
            extractedRow.GetCellAsString(0).Should().Be("Alice");
            extractedRow.GetCellAsString(1).Should().Be("30");
            extractedRow.GetCellAsString(2).Should().Be("Rome");
        }

        [Fact]
        public async Task CreateRowComparison_RealFiles_CreatesValidComparison()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var filePath = GetTestFilePath("Valid", "simple.xlsx");
            var excelFile = await excelReaderService.LoadFileAsync(filePath);

            // Create two search results from the same file but different rows
            // Row indices are ABSOLUTE 0-based (row 0 = header, row 1+ = data)
            var searchResult1 = new SearchResult(excelFile, "Sheet1", 1, 0, "Alice");  // First data row (absolute row 1)
            var searchResult2 = new SearchResult(excelFile, "Sheet1", 2, 0, "Bob");    // Second data row (absolute row 2)

            var request = new RowComparisonRequest(
                new List<SearchResult> { searchResult1, searchResult2 },
                name: "Alice vs Bob Comparison"
            );

            // Act
            var comparison = _service.CreateRowComparison(request);

            // Assert
            comparison.Should().NotBeNull();
            comparison.Name.Should().Be("Alice vs Bob Comparison");
            comparison.Rows.Should().HaveCount(2);

            comparison.Rows[0].GetCellAsString(0).Should().Be("Alice");
            comparison.Rows[0].GetCellAsString(1).Should().Be("30");
            comparison.Rows[0].GetCellAsString(2).Should().Be("Rome");

            comparison.Rows[1].GetCellAsString(0).Should().Be("Bob");
            comparison.Rows[1].GetCellAsString(1).Should().Be("25");
            comparison.Rows[1].GetCellAsString(2).Should().Be("Milan");
        }

        #endregion

        #region DataRegion Tests

        [Fact]
        public void ExtractRowFromSearchResult_WithRegion_UsesRegionDataStartRowAsHeaderBoundary()
        {
            // Arrange: 4 rows — rows 0-1 are region headers, rows 2-3 are region data
            var columns = new[] { "ID", "Name", "Value" };
            var sheet = new SASheetData("Sheet1", columns);
            for (int i = 0; i < 4; i++)
                sheet.AddRow(columns.Select(_ => new SACellData(SACellValue.FromText($"R{i}"))).ToArray());

            var region = new DataRegion
            {
                Name = "MyRegion",
                HeaderStartRow = 0,
                HeaderEndRow = 1,
                DataStartRow = 2,
                DataEndRow = 3
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            // Row 1 is a header row in the region → should throw
            var headerResult = new SearchResult(file, "Sheet1", 1, 0, "R1") { RegionName = "MyRegion" };
            _service.Invoking(s => s.ExtractRowFromSearchResult(headerResult))
                .Should().Throw<ArgumentException>().WithMessage("*header row*");

            // Row 2 is the first data row → should succeed
            var dataResult = new SearchResult(file, "Sheet1", 2, 0, "R2") { RegionName = "MyRegion" };
            var extracted = _service.ExtractRowFromSearchResult(dataResult);
            extracted.Should().NotBeNull();
            extracted.RowIndex.Should().Be(2);
        }

        [Fact]
        public void ExtractRowFromSearchResult_WithUnknownRegionName_FallsBackToSheetHeaderRowCount()
        {
            // Arrange: sheet has 1 header row (default), search result references non-existent region
            var columns = new[] { "ID", "Name" };
            var sheet = new SASheetData("Sheet1", columns);
            sheet.AddRow(columns.Select(n => new SACellData(SACellValue.FromText(n))).ToArray()); // header row 0
            sheet.AddRow(columns.Select(_ => new SACellData(SACellValue.FromText("data"))).ToArray()); // data row 1

            var file = new ExcelFile("test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            // Unknown region → falls back to sheet.HeaderRowCount=1, so row 0 is a header
            var result = new SearchResult(file, "Sheet1", 0, 0, "ID") { RegionName = "NoSuchRegion" };
            _service.Invoking(s => s.ExtractRowFromSearchResult(result))
                .Should().Throw<ArgumentException>().WithMessage("*header row*");

            // Row 1 should succeed
            var dataResult = new SearchResult(file, "Sheet1", 1, 0, "data") { RegionName = "NoSuchRegion" };
            var extracted = _service.ExtractRowFromSearchResult(dataResult);
            extracted.Should().NotBeNull();
        }

        [Fact]
        public void ExtractRowFromSearchResult_WithRegionColumnBounds_FiltersToRegionColumns()
        {
            // Arrange: 5 columns, region covers columns 1-3 (Name, Age, City)
            var columns = new[] { "Extra", "Name", "Age", "City", "Tail" };
            var sheet = new SASheetData("Sheet1", columns);
            sheet.AddRow(columns.Select(n => new SACellData(SACellValue.FromText(n))).ToArray()); // row 0 = header
            sheet.AddRow(new[] // row 1 = data
            {
                new SACellData(SACellValue.FromText("x")),
                new SACellData(SACellValue.FromText("Alice")),
                new SACellData(SACellValue.FromInteger(30)),
                new SACellData(SACellValue.FromText("Rome")),
                new SACellData(SACellValue.FromText("y"))
            });

            var region = new DataRegion
            {
                Name = "Core",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 1,
                EndColumn = 3
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            var searchResult = new SearchResult(file, "Sheet1", 1, 1, "Alice") { RegionName = "Core" };
            var extracted = _service.ExtractRowFromSearchResult(searchResult);

            // Should have 3 cells (cols 1-3) and 3 matching headers
            extracted.Cells.Should().HaveCount(3);
            extracted.ColumnHeaders.Should().HaveCount(3);
            extracted.ColumnHeaders[0].Should().Be("Name");
            extracted.ColumnHeaders[1].Should().Be("Age");
            extracted.ColumnHeaders[2].Should().Be("City");
        }

        [Fact]
        public void GetColumnHeaders_WithRegionName_FiltersToRegionColumnBounds()
        {
            // Arrange: 5 columns, region covers columns 2-4
            var columns = new[] { "A", "B", "C", "D", "E" };
            var sheet = new SASheetData("Sheet1", columns);
            sheet.AddRow(columns.Select(n => new SACellData(SACellValue.FromText(n))).ToArray());

            var region = new DataRegion
            {
                Name = "Right",
                DataStartRow = 1,
                StartColumn = 2,
                EndColumn = 4
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            var headers = _service.GetColumnHeaders(file, "Sheet1", "Right");

            headers.Should().HaveCount(3);
            headers[0].Should().Be("C");
            headers[1].Should().Be("D");
            headers[2].Should().Be("E");
        }

        [Fact]
        public void GetColumnHeaders_WithNullRegionName_ReturnsAllColumns()
        {
            // Arrange
            var columns = new[] { "A", "B", "C" };
            var file = CreateExcelFileWithSheet("Sheet1", columns);

            var headers = _service.GetColumnHeaders(file, "Sheet1", null);

            headers.Should().HaveCount(3);
        }

        #endregion

        #region Helper Methods

        private static ExcelFile CreateExcelFileWithSheet(string sheetName, string[]? columnNames = null)
        {
            var columns = columnNames ?? new[] { "Column1" };
            var sheetData = new SASheetData(sheetName, columns);

            // Add a sample row
            var rowData = new SACellData[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                rowData[i] = new SACellData(SACellValue.FromText($"Value{i}"));
            }
            sheetData.AddRow(rowData);

            var sheets = new Dictionary<string, SASheetData>
            {
                { sheetName, sheetData }
            };

            return new ExcelFile(
                "test.xlsx",
                LoadStatus.Success,
                sheets,
                new List<ExcelError>()
            );
        }

        private static IExcelReaderService CreateRealExcelReaderService()
        {
            var serviceLogger = new Mock<ILogService>();
            var readerLogger = new Mock<ILogService>();
            var cellParser = new CellReferenceParser();
            var cellValueReader = new CellValueReader();
            var mergedRangeExtractor = new OpenXmlMergedRangeExtractor(cellParser);

            // Foundation services (real implementations for integration tests)
            var currencyDetector = new CurrencyDetector();
            var normalizationService = new DataNormalizationService();
            var columnAnalysisService = new ColumnAnalysisService(currencyDetector);
            var mergedCellResolver = new MergedCellResolver();

            // Create orchestrator (with MergedCellResolver as first parameter)
            var orchestrator = new SheetAnalysisOrchestrator(mergedCellResolver, columnAnalysisService, normalizationService, readerLogger.Object);

            // Create settings mock
            var settings = new AppSettings
            {
                Performance = new PerformanceSettings { MaxConcurrentFileLoads = 5 }
            };
            var settingsMock = new Mock<IOptions<AppSettings>>();
            settingsMock.Setup(s => s.Value).Returns(settings);

            // Create user settings mock
            var userSettingsMock = new Mock<ISettingsService>();
            userSettingsMock.Setup(s => s.Current).Returns(UserSettings.CreateDefault());

            // Create FileReaderContext (facade for common dependencies)
            var readerContext = new SheetAtlas.Infrastructure.External.Readers.FileReaderContext(
                readerLogger.Object,
                orchestrator,
                userSettingsMock.Object,
                settingsMock.Object);

            // Create OpenXmlFileReader with context and specific dependencies
            var openXmlReader = new OpenXmlFileReader(
                readerContext,
                cellParser,
                mergedRangeExtractor,
                cellValueReader);
            var readers = new List<IFileFormatReader> { openXmlReader };

            return new ExcelReaderService(readers, serviceLogger.Object, settingsMock.Object);
        }

        private static string GetTestFilePath(string category, string filename)
        {
            var testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "TestData"
            );

            var path = Path.Combine(testDataPath, category, filename);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Test file not found: {path}. Make sure TestData files are generated.");
            }

            return path;
        }

        #endregion
    }
}
