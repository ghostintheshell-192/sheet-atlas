using FluentAssertions;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Infrastructure.External.Writers;
using SheetAtlas.Logging.Services;
using Moq;

namespace SheetAtlas.Tests.Infrastructure.Writers
{
    /// <summary>
    /// Tests for region-scoped export in ExcelWriterService.
    /// Verifies that DataRegion bounds correctly limit rows, columns, and headers in output.
    /// </summary>
    public class ExcelWriterServiceTests : IDisposable
    {
        private readonly ExcelWriterService _service;
        private readonly string _tempDir;

        public ExcelWriterServiceTests()
        {
            var logger = new Mock<ILogService>();
            _service = new ExcelWriterService(logger.Object);
            _tempDir = Path.Combine(Path.GetTempPath(), $"sheetatlas_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        #region Region-Scoped CSV Export

        [Fact]
        public async Task WriteToCsvAsync_WithRegion_ExportsOnlyRegionRows()
        {
            // Arrange: 6 rows (0=header, 1-5=data), region covers rows 2-4 with row 2 as header
            var sheet = CreateSheet("Sheet1", new[] { "A", "B" }, 6);
            var region = new DataRegion
            {
                Name = "Middle",
                HeaderStartRow = 2,
                HeaderEndRow = 2,
                DataStartRow = 3,
                DataEndRow = 4,
                StartColumn = 0,
                EndColumn = 1
            };

            var outputPath = Path.Combine(_tempDir, "region_rows.csv");
            var options = new CsvExportOptions { Region = region };

            // Act
            var result = await _service.WriteToCsvAsync(sheet, outputPath, options);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(outputPath);
            // 1 header line + 2 data lines (rows 3-4)
            lines.Should().HaveCount(3);
        }

        [Fact]
        public async Task WriteToCsvAsync_WithRegion_ExportsOnlyRegionColumns()
        {
            // Arrange: 5 columns, region covers columns 1-3
            var columns = new[] { "Extra", "Name", "Age", "City", "Tail" };
            var sheet = CreateSheet("Sheet1", columns, 4);
            var region = new DataRegion
            {
                Name = "Core",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 3,
                StartColumn = 1,
                EndColumn = 3
            };

            var outputPath = Path.Combine(_tempDir, "region_cols.csv");
            var options = new CsvExportOptions { Region = region };

            // Act
            var result = await _service.WriteToCsvAsync(sheet, outputPath, options);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(outputPath);
            // Header line should have 3 fields (columns 1-3)
            var headerFields = lines[0].Split(',');
            headerFields.Should().HaveCount(3);
        }

        [Fact]
        public async Task WriteToCsvAsync_WithRegion_UsesRegionHeaderRow()
        {
            // Arrange: row 0 has "GlobalA,GlobalB", row 2 has "RegionA,RegionB"
            var columns = new[] { "GlobalA", "GlobalB" };
            var sheet = new SASheetData("Sheet1", columns);
            // Row 0 (sheet header): GlobalA, GlobalB
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("GlobalA")), new SACellData(SACellValue.FromText("GlobalB")) });
            // Row 1 (gap)
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("gap")), new SACellData(SACellValue.FromText("gap")) });
            // Row 2 (region header): RegionA, RegionB
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("RegionA")), new SACellData(SACellValue.FromText("RegionB")) });
            // Row 3-4 (region data)
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("d1")), new SACellData(SACellValue.FromText("d2")) });
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("d3")), new SACellData(SACellValue.FromText("d4")) });

            var region = new DataRegion
            {
                Name = "Sub",
                HeaderStartRow = 2,
                HeaderEndRow = 2,
                DataStartRow = 3,
                DataEndRow = 4,
                StartColumn = 0,
                EndColumn = 1
            };

            var outputPath = Path.Combine(_tempDir, "region_header.csv");
            var options = new CsvExportOptions { Region = region };

            // Act
            await _service.WriteToCsvAsync(sheet, outputPath, options);

            // Assert: header should be "RegionA,RegionB" (from row 2), NOT "GlobalA,GlobalB"
            var lines = await File.ReadAllLinesAsync(outputPath);
            lines[0].Should().Be("RegionA,RegionB");
            lines[1].Should().Be("d1,d2");
            lines[2].Should().Be("d3,d4");
        }

        [Fact]
        public async Task WriteToCsvAsync_WithoutRegion_ExportsFullSheet()
        {
            // Arrange
            var sheet = CreateSheet("Sheet1", new[] { "A", "B" }, 5);

            var outputPath = Path.Combine(_tempDir, "full_sheet.csv");
            var options = new CsvExportOptions();

            // Act
            var result = await _service.WriteToCsvAsync(sheet, outputPath, options);

            // Assert: 1 header + 4 data rows (sheet has 1 header row by default)
            result.IsSuccess.Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(outputPath);
            lines.Should().HaveCount(5); // header + 4 data rows
        }

        [Fact]
        public async Task WriteToCsvAsync_WithRegionNoHeader_ExportsDataOnly()
        {
            // Arrange: region without explicit header
            var sheet = CreateSheet("Sheet1", new[] { "A", "B" }, 5);
            var region = new DataRegion
            {
                Name = "NoHeader",
                DataStartRow = 1,
                DataEndRow = 3,
                StartColumn = 0,
                EndColumn = 1
            };

            var outputPath = Path.Combine(_tempDir, "no_header_region.csv");
            var options = new CsvExportOptions { Region = region };

            // Act
            await _service.WriteToCsvAsync(sheet, outputPath, options);

            // Assert: header from ColumnNames (fallback) + 3 data rows
            var lines = await File.ReadAllLinesAsync(outputPath);
            lines.Should().HaveCount(4); // header + rows 1-3
            lines[0].Should().Be("A,B"); // from sheet.ColumnNames
        }

        #endregion

        #region Normalize In-Place Export

        [Fact]
        public async Task NormalizeToExcelAsync_PreservesSheetStructure()
        {
            // Arrange: create a source .xlsx, then normalize it
            var sheet = CreateSheet("Sheet1", new[] { "A", "B" }, 4);
            var sourcePath = Path.Combine(_tempDir, "source_preserve.xlsx");
            await _service.WriteToExcelAsync(sheet, sourcePath);

            var sheetsData = new Dictionary<string, SASheetData> { ["Sheet1"] = sheet };
            var outputPath = Path.Combine(_tempDir, "normalized_preserve.xlsx");

            // Act
            var result = await _service.NormalizeToExcelAsync(sourcePath, sheetsData, outputPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue();

            // Verify sheet structure is preserved (same sheet name, same row count)
            using var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(outputPath, false);
            var sheets = doc.WorkbookPart!.Workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList();
            sheets.Should().HaveCount(1);
            sheets[0].Name!.Value.Should().Be("Sheet1");
        }

        [Fact]
        public async Task NormalizeToExcelAsync_NormalizesCellValues()
        {
            // Arrange: create sheet with CleanedValues set on some cells
            var columns = new[] { "Name", "Value" };
            var sheet = new SASheetData("Sheet1", columns);
            var meta = new CellMetadata { CleanedValue = SACellValue.FromText("cleaned") };
            var cell0 = new SACellData(SACellValue.FromText("original"), meta);
            var cell1 = new SACellData(SACellValue.FromText("unchanged"));
            sheet.AddRow(new[] { cell0, cell1 });

            var sourcePath = Path.Combine(_tempDir, "source_normalize.xlsx");
            await _service.WriteToExcelAsync(sheet, sourcePath);

            var sheetsData = new Dictionary<string, SASheetData> { ["Sheet1"] = sheet };
            var outputPath = Path.Combine(_tempDir, "normalized_values.xlsx");

            // Act
            var result = await _service.NormalizeToExcelAsync(sourcePath, sheetsData, outputPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.NormalizedCellCount.Should().Be(1);
        }

        [Fact]
        public async Task NormalizeToExcelAsync_WithRegion_OnlyNormalizesRegionCells()
        {
            // Arrange: 4 rows, region covers rows 2-3. Only those cells should be normalized.
            var columns = new[] { "A" };
            var sheet = new SASheetData("Sheet1", columns);
            for (int i = 0; i < 4; i++)
            {
                var meta = new CellMetadata { CleanedValue = SACellValue.FromText($"clean{i}") };
                var cell = new SACellData(SACellValue.FromText($"R{i}"), meta);
                sheet.AddRow(new[] { cell });
            }

            // Add a region covering only rows 2-3
            sheet.AddDataRegion(new DataRegion
            {
                Name = "Middle",
                DataStartRow = 2,
                DataEndRow = 3,
                StartColumn = 0,
                EndColumn = 0
            });

            var sourcePath = Path.Combine(_tempDir, "source_region_scope.xlsx");
            await _service.WriteToExcelAsync(sheet, sourcePath);

            var sheetsData = new Dictionary<string, SASheetData> { ["Sheet1"] = sheet };
            var outputPath = Path.Combine(_tempDir, "normalized_region.xlsx");

            // Act
            var result = await _service.NormalizeToExcelAsync(sourcePath, sheetsData, outputPath);

            // Assert: only 2 cells normalized (rows 2-3), not rows 0-1
            result.IsSuccess.Should().BeTrue();
            result.NormalizedCellCount.Should().Be(2);
        }

        [Fact]
        public async Task NormalizeToExcelAsync_WithoutRegions_NormalizesAllCells()
        {
            // Arrange: 3 rows, no regions, all cells have CleanedValues
            var columns = new[] { "A" };
            var sheet = new SASheetData("Sheet1", columns);
            for (int i = 0; i < 3; i++)
            {
                var meta = new CellMetadata { CleanedValue = SACellValue.FromText($"clean{i}") };
                var cell = new SACellData(SACellValue.FromText($"R{i}"), meta);
                sheet.AddRow(new[] { cell });
            }

            var sourcePath = Path.Combine(_tempDir, "source_all.xlsx");
            await _service.WriteToExcelAsync(sheet, sourcePath);

            var sheetsData = new Dictionary<string, SASheetData> { ["Sheet1"] = sheet };
            var outputPath = Path.Combine(_tempDir, "normalized_all.xlsx");

            // Act
            var result = await _service.NormalizeToExcelAsync(sourcePath, sheetsData, outputPath);

            // Assert: all 3 cells normalized
            result.IsSuccess.Should().BeTrue();
            result.NormalizedCellCount.Should().Be(3);
        }

        #endregion

        #region Helpers

        private static SASheetData CreateSheet(string name, string[] columns, int totalRows)
        {
            var sheet = new SASheetData(name, columns);
            for (int r = 0; r < totalRows; r++)
            {
                var cells = columns.Select((col, ci) =>
                    new SACellData(SACellValue.FromText($"R{r}C{ci}"))).ToArray();
                sheet.AddRow(cells);
            }
            return sheet;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        #endregion
    }
}
