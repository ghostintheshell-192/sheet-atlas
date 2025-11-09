using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Services.Foundation;
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
            var excelFile = CreateExcelFileWithSheet("Sheet1", new[] { "Name", "Age", "City" });

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
                "Comparison1"
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
            // Note: Row index is 0-based in the DataTable (header is already removed)
            var searchResult = new SearchResult(
                excelFile,
                "Sheet1",
                0,  // Row index (0-based in DataTable, this is Alice)
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
            // Note: Row indices are 0-based in DataTable (header already removed)
            var searchResult1 = new SearchResult(excelFile, "Sheet1", 0, 0, "Alice");  // First data row
            var searchResult2 = new SearchResult(excelFile, "Sheet1", 1, 0, "Bob");    // Second data row

            var request = new RowComparisonRequest(
                new List<SearchResult> { searchResult1, searchResult2 },
                "Alice vs Bob Comparison"
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

        #region Helper Methods

        private ExcelFile CreateExcelFileWithSheet(string sheetName, string[]? columnNames = null)
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

        private IExcelReaderService CreateRealExcelReaderService()
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

            // Create OpenXmlFileReader with orchestrator
            var openXmlReader = new OpenXmlFileReader(
                readerLogger.Object,
                cellParser,
                mergedRangeExtractor,
                cellValueReader,
                orchestrator);
            var readers = new List<IFileFormatReader> { openXmlReader };

            // Create settings mock
            var settings = new AppSettings
            {
                Performance = new PerformanceSettings { MaxConcurrentFileLoads = 5 }
            };
            var settingsMock = new Mock<IOptions<AppSettings>>();
            settingsMock.Setup(s => s.Value).Returns(settings);

            return new ExcelReaderService(readers, serviceLogger.Object, settingsMock.Object);
        }

        private string GetTestFilePath(string category, string filename)
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
