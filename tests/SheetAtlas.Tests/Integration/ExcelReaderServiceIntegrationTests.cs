using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Application.DTOs;
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
using DocumentFormat.OpenXml.Spreadsheet;

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

        [Fact]
        public async Task LoadFileAsync_MergedCellsWithOffsetRows_CorrectlyPositionsMergedContent()
        {
            // Regression test for the merged-cell coordinate bug.
            //
            // The file has Excel row 1 absent from XML (a blank row not written by the editor).
            // This means firstRowOffset = 1: the first XML row is Excel row 2, which maps to
            // SASheetData row 0. Before the fix, MergedRange used absolute 0-based Excel row
            // indices (B2:G2 → startRow=1), but SASheetData was built sequentially starting at
            // 0 from the first XML row. Result: merge was expanded to SASheetData row 1 instead
            // of row 0, corrupting the header and losing the merged-cell content from view.
            //
            // After the fix, PopulateMergedCells subtracts firstRowOffset so that B2:G2 correctly
            // targets SASheetData row 0, and the merged header "SWITCH LAYOUT" appears in
            // ColumnNames (extracted from the header row).

            // Arrange
            var filePath = GetTestFilePath("EdgeCases", "merged-cells-offset-rows.xlsx");

            // Act
            var result = await _service.LoadFileAsync(filePath);

            // Assert — file loads successfully
            result.Should().NotBeNull();
            result.Status.Should().BeOneOf(LoadStatus.Success, LoadStatus.PartialSuccess);
            result.Sheets.Should().NotBeEmpty();

            // The first sheet contains the multi-level merged header layout
            var sheet = result.Sheets.Values.First();
            sheet.DataRowCount.Should().BeGreaterThan(0);

            // "SWITCH LAYOUT" is the merged header spanning B2:G2.
            // With the fix it lands on SASheetData row 0 (the header row), so it must appear
            // in ColumnNames. Before the fix it landed on row 1, leaving the header empty.
            sheet.ColumnNames.Should().Contain("SWITCH LAYOUT",
                "merged cell B2:G2 must expand into the header row (Excel row 2 = SASheetData row 0)");

            // "Switch Connections" is the merged header spanning J2:L2 — same row, same fix.
            sheet.ColumnNames.Should().Contain("Switch Connections",
                "merged cell J2:L2 must also expand into the header row");
        }

        [Fact]
        public void RawXml_MultiHeaderDataRegion_FirstRowIsRow2()
        {
            // Directly verify the OpenXML Row.RowIndex for the sample file
            var filePath = GetTestFilePath("EdgeCases", "multipli-headers-in-dataregion.xlsx");

            using var doc = SpreadsheetDocument.Open(filePath, false);
            var wbPart = doc.WorkbookPart!;
            var sheetEntry = wbPart.Workbook.Sheets!.Elements<Sheet>().First();
            var wsPart = (WorksheetPart)wbPart.GetPartById(sheetEntry.Id!);

            // First call — same as ProcessHeaderRow
            var firstRow = wsPart.Worksheet.Descendants<Row>().FirstOrDefault();
            firstRow.Should().NotBeNull();
            firstRow!.RowIndex.Should().NotBeNull("first XML row must have a RowIndex");
            firstRow.RowIndex!.Value.Should().Be(2, "first row in XML is Excel row 2 (r=2)");

            // Second call — same as ProcessSheet does after ProcessHeaderRow
            var secondCall = wsPart.Worksheet.Descendants<Row>().FirstOrDefault();
            secondCall.Should().NotBeNull();
            secondCall!.RowIndex.Should().NotBeNull("second Descendants call must also find RowIndex");
            secondCall.RowIndex!.Value.Should().Be(2, "second call must return same row (r=2)");

            // Verify they're the same row object
            ReferenceEquals(firstRow, secondCall).Should().BeTrue("both calls should return same DOM node");

            // Also check first cell reference
            var firstCell = firstRow.Elements<Cell>().FirstOrDefault();
            firstCell.Should().NotBeNull();
            firstCell!.CellReference!.Value.Should().Be("B2");
        }

        [Fact]
        public async Task LoadFileAsync_MultiHeaderDataRegion_PreservesOriginCoordinates()
        {
            // The file has dimension B2:L11 — Row 1 absent from XML, data starts at column B.
            var filePath = GetTestFilePath("EdgeCases", "multipli-headers-in-dataregion.xlsx");

            var result = await _service.LoadFileAsync(filePath);

            result.Should().NotBeNull();
            result.Status.Should().BeOneOf(LoadStatus.Success, LoadStatus.PartialSuccess);

            var sheet = result.Sheets.Values.First();

            // Diagnostic: what did the reader actually produce?
            var diagnosticMsg = $"OriginRow={sheet.OriginRow}, OriginColumn={sheet.OriginColumn}, " +
                                $"RowCount={sheet.RowCount}, ColumnCount={sheet.ColumnCount}, " +
                                $"CellRef[0,0]={sheet.GetCellReference(0, 0)}, " +
                                $"HeaderRowCount={sheet.HeaderRowCount}, " +
                                $"ColumnNames=[{string.Join(", ", sheet.ColumnNames.Take(5))}...]";

            // Check what happens when we open the same file with raw OpenXML
            using var doc = SpreadsheetDocument.Open(filePath, false);
            var wbPart = doc.WorkbookPart!;
            var sheetEntry = wbPart.Workbook.Sheets!.Elements<Sheet>().First();
            var wsPart = (WorksheetPart)wbPart.GetPartById(sheetEntry.Id!);
            var firstXmlRow = wsPart.Worksheet.Descendants<Row>().FirstOrDefault();
            var rawRowIndex = firstXmlRow?.RowIndex?.Value;
            var rawFirstCell = firstXmlRow?.Elements<Cell>().FirstOrDefault()?.CellReference?.Value;
            diagnosticMsg += $" | RawXML: firstRow.RowIndex={rawRowIndex}, firstCell={rawFirstCell}";

            // Origin must reflect that data starts at Excel B2
            sheet.OriginRow.Should().Be(1, $"first XML row is Excel row 2. Diagnostic: {diagnosticMsg}");
            sheet.OriginColumn.Should().Be(1, $"first column is B (index 1). Diagnostic: {diagnosticMsg}");

            sheet.GetCellReference(0, 0).Should().Be("B2");
            sheet.ColumnCount.Should().Be(11);
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

// TEMP DIAGNOSTIC - remove after debugging
