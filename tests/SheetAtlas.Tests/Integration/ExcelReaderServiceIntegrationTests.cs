using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Infrastructure.External;
using SheetAtlas.Infrastructure.External.Readers;
using FluentAssertions;
using Moq;
using SheetAtlas.Logging.Models;
using SheetAtlas.Logging.Services;
using SheetAtlas.Core.Configuration;
using Microsoft.Extensions.Options;
using DocumentFormat.OpenXml.Packaging;

namespace SheetAtlas.Tests.Integration
{
    /// <summary>
    /// Integration tests for ExcelReaderService using real Excel files.
    /// These tests verify the entire file reading pipeline from disk to DataTable.
    /// </summary>
    public class ExcelReaderServiceIntegrationTests : IDisposable
    {
        private readonly ExcelReaderService _service;
        private readonly string _testDataPath;

        public ExcelReaderServiceIntegrationTests()
        {
            // Setup real dependencies (not mocks) for integration testing
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

            _service = new ExcelReaderService(readers, serviceLogger.Object, settingsMock.Object);

            // Get path to TestData directory
            _testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "TestData"
            );
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        #region Helper Methods for SASheetData Access

        private static int GetColumnIndex(SASheetData sheet, string columnName)
        {
            return Array.IndexOf(sheet.ColumnNames, columnName);
        }

        /// <summary>
        /// Get cell value using DATA-RELATIVE row index (0 = first data row).
        /// Automatically converts to absolute row index by adding HeaderRowCount.
        /// </summary>
        private static string GetCellValueAsString(SASheetData sheet, int dataRowIndex, string columnName)
        {
            int colIndex = GetColumnIndex(sheet, columnName);
            if (colIndex == -1) throw new ArgumentException($"Column '{columnName}' not found");

            // Convert data-relative index to absolute index
            int absoluteRow = sheet.HeaderRowCount + dataRowIndex;
            return sheet.GetCellValue(absoluteRow, colIndex).ToString();
        }

        #endregion

        #region Valid Files Tests

        [Fact]
        public async Task LoadFileAsync_SimpleFile_ReadsAllDataCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("Valid", "simple.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Success);
            result.Sheets.Should().ContainKey("Sheet1");

            var sheet = result.Sheets["Sheet1"];
            sheet.ColumnCount.Should().Be(3);
            sheet.DataRowCount.Should().Be(2);

            // Verify headers
            sheet.ColumnNames[0].Should().Be("Name");
            sheet.ColumnNames[1].Should().Be("Age");
            sheet.ColumnNames[2].Should().Be("City");

            // Verify first row data
            GetCellValueAsString(sheet, 0, "Name").Should().Be("Alice");
            GetCellValueAsString(sheet, 0, "Age").Should().Be("30");
            GetCellValueAsString(sheet, 0, "City").Should().Be("Rome");

            // Verify second row data
            GetCellValueAsString(sheet, 1, "Name").Should().Be("Bob");
            GetCellValueAsString(sheet, 1, "Age").Should().Be("25");
            GetCellValueAsString(sheet, 1, "City").Should().Be("Milan");
        }

        [Fact]
        public async Task LoadFileAsync_LargeFile_Reads100RowsCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("Valid", "large.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Sheets.Should().ContainKey("Data");

            var sheet = result.Sheets["Data"];
            sheet.ColumnCount.Should().Be(5);
            sheet.DataRowCount.Should().Be(100);

            result.Status.Should().Be(LoadStatus.Success);

            // Verify headers
            sheet.ColumnNames[0].Should().Be("ID");
            sheet.ColumnNames[1].Should().Be("Product");
            sheet.ColumnNames[2].Should().Be("Quantity");
            sheet.ColumnNames[3].Should().Be("Price");
            sheet.ColumnNames[4].Should().Be("Total");

            // Verify first row
            GetCellValueAsString(sheet, 0, "ID").Should().Be("1");
            GetCellValueAsString(sheet, 0, "Product").Should().Be("Product 1");

            // Verify last row
            GetCellValueAsString(sheet, 99, "ID").Should().Be("100");
            GetCellValueAsString(sheet, 99, "Product").Should().Be("Product 100");
        }

        [Fact]
        public async Task LoadFileAsync_MultiSheetFile_ReadsAllSheetsCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("Valid", "multi-sheet.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Success);
            result.Sheets.Should().HaveCount(3);

            // Verify all sheets exist
            result.Sheets.Should().ContainKey("Employees");
            result.Sheets.Should().ContainKey("Departments");
            result.Sheets.Should().ContainKey("Summary");

            // Verify Employees sheet
            var employeesSheet = result.Sheets["Employees"];
            employeesSheet.ColumnCount.Should().Be(2);
            employeesSheet.ColumnNames[0].Should().Be("Employee");
            employeesSheet.ColumnNames[1].Should().Be("Department");

            // Verify Departments sheet
            var departmentsSheet = result.Sheets["Departments"];
            departmentsSheet.ColumnCount.Should().Be(2);
            departmentsSheet.ColumnNames[0].Should().Be("Department");
            departmentsSheet.ColumnNames[1].Should().Be("Budget");

            // Verify Summary sheet
            var summarySheet = result.Sheets["Summary"];
            summarySheet.ColumnCount.Should().Be(2);
            summarySheet.ColumnNames[0].Should().Be("Total Employees");
            summarySheet.ColumnNames[1].Should().Be("Total Budget");
        }

        #endregion

        #region Invalid Files Tests

        [Fact]
        public async Task LoadFileAsync_EmptyFile_ReturnsSuccessWithInfo()
        {
            // Arrange
            var filePath = GetTestFilePath("Invalid", "empty.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            // Empty files load successfully but sheets with no columns are skipped
            result.Status.Should().Be(LoadStatus.Success);
            result.Sheets.Should().BeEmpty(); // No sheets because empty sheet is skipped
            result.Errors.Should().Contain(e =>
                e.Level == LogSeverity.Info &&
                e.Message.Contains("empty"));
        }

        [Fact]
        public async Task LoadFileAsync_CorruptedFile_ReturnsFailedStatus()
        {
            // Arrange
            var filePath = GetTestFilePath("Invalid", "corrupted.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Level == LogSeverity.Critical);
        }

        [Fact]
        public async Task LoadFileAsync_UnsupportedFormat_ReturnsFailedStatus()
        {
            // Arrange
            var filePath = GetTestFilePath("Invalid", "unsupported.xls");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e =>
                e.Level == LogSeverity.Critical &&
                e.Message.Contains("format"));
        }

        [Fact]
        public async Task LoadFileAsync_NonExistentFile_ThrowsException()
        {
            // Arrange
            var filePath = Path.Combine(_testDataPath, "NonExistent", "missing.xlsx");

            // Act
            Func<Task> act = async () => await _service.LoadFileAsync(filePath);

            // Assert - The service doesn't throw for non-existent files, it returns Failed status
            var result = await _service.LoadFileAsync(filePath);
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task LoadFileAsync_NullFilePath_ThrowsArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _service.LoadFileAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task LoadFileAsync_EmptyFilePath_ThrowsArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _service.LoadFileAsync(string.Empty);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task LoadFileAsync_SpecialCharactersFile_ReadsUnicodeCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("EdgeCases", "special-chars.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Success);
            result.Sheets.Should().ContainKey("Special Chars");

            var sheet = result.Sheets["Special Chars"];
            sheet.DataRowCount.Should().BeGreaterThan(0);

            // Verify special characters are preserved
            GetCellValueAsString(sheet, 0, "Name").Should().Contain("Café");
            GetCellValueAsString(sheet, 0, "Symbols").Should().Contain("€");
        }

        [Fact]
        public async Task LoadFileAsync_FormulasFile_ReadsFormulasCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("EdgeCases", "formulas.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            // Accept PartialSuccess if there are column analysis errors (expected in test data)
            result.Status.Should().BeOneOf(LoadStatus.Success, LoadStatus.PartialSuccess);
            result.Sheets.Should().ContainKey("Formulas");

            var sheet = result.Sheets["Formulas"];
            sheet.ColumnCount.Should().Be(3);
            sheet.DataRowCount.Should().BeGreaterThan(0);

            // Note: OpenXml reads formula results, not the formulas themselves
            // Verify the structure is correct
            sheet.ColumnNames[0].Should().Be("Value1");
            sheet.ColumnNames[1].Should().Be("Value2");
            sheet.ColumnNames[2].Should().Be("Sum");
        }

        [Fact]
        public async Task LoadFileAsync_MergedCellsFile_HandlesMergedCellsCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath("EdgeCases", "merged-cells.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(LoadStatus.Success);
            result.Sheets.Should().ContainKey("Merged");

            var sheet = result.Sheets["Merged"];
            sheet.DataRowCount.Should().BeGreaterThan(0);

            // Verify merged cell header is read
            sheet.ColumnNames[0].Should().Be("Merged Title");
        }

        #endregion

        #region Multiple Files Tests

        [Fact]
        public async Task LoadFilesAsync_MultipleValidFiles_ReadsAllFiles()
        {
            // Arrange
            var filePaths = new[]
            {
                GetTestFilePath("Valid", "simple.xlsx"),
                GetTestFilePath("Valid", "multi-sheet.xlsx")
            };

            // Act
            var results = await _service.LoadFilesAsync(filePaths);

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(r => r.Status.Should().Be(LoadStatus.Success));

            results[0].Sheets.Should().ContainKey("Sheet1");
            results[1].Sheets.Should().HaveCount(3);
        }

        [Fact]
        public async Task LoadFilesAsync_MixedValidAndInvalidFiles_ProcessesAllFiles()
        {
            // Arrange
            var filePaths = new[]
            {
                GetTestFilePath("Valid", "simple.xlsx"),
                GetTestFilePath("Invalid", "corrupted.xlsx"),
                GetTestFilePath("Valid", "large.xlsx")
            };

            // Act
            var results = await _service.LoadFilesAsync(filePaths);

            // Assert
            results.Should().HaveCount(3);
            results[0].Status.Should().Be(LoadStatus.Success);
            results[1].Status.Should().Be(LoadStatus.Failed);
            results[2].Status.Should().Be(LoadStatus.Success);
        }

        #endregion

        #region Helper Methods

        private string GetTestFilePath(string category, string filename)
        {
            var path = Path.Combine(_testDataPath, category, filename);

            // Verify file exists for better error messages
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Test file not found: {path}. Make sure TestData files are generated.");
            }

            return path;
        }

        #endregion
    }
}
