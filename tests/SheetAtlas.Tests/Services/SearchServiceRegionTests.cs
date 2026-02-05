using FluentAssertions;
using Moq;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using Xunit;

namespace SheetAtlas.Tests.Services
{
    public class SearchServiceRegionTests
    {
        private readonly Mock<ILogService> _mockLogger;
        private readonly SearchService _searchService;

        public SearchServiceRegionTests()
        {
            _mockLogger = new Mock<ILogService>();
            _searchService = new SearchService(_mockLogger.Object);
        }

        [Fact]
        public void SearchInSheet_WithRegionName_OnlySearchesRegionBounds()
        {
            // Arrange — region covers rows 2-3 only (data rows), columns 0-1
            var file = CreateTestFileWithRegion(
                columnNames: new[] { "A", "B", "C" },
                dataRows: new[]
                {
                    new[] { "match", "no", "match" },  // row 1 (data, outside region)
                    new[] { "match", "no", "match" },   // row 2 (data, region start)
                    new[] { "no", "match", "match" },   // row 3 (data, region end)
                    new[] { "match", "match", "match" }, // row 4 (data, outside region)
                },
                regionName: "TestRegion",
                regionDataStart: 2, regionDataEnd: 3,
                regionStartCol: 0, regionEndCol: 1);

            // Act
            var results = _searchService.SearchInSheet(file, "Sheet1", "match", regionName: "TestRegion");

            // Assert — only matches within region bounds (rows 2-3, cols 0-1)
            // row2: col0="match" → 1 match; row3: col1="match" → 1 match = 2 total
            results.Should().HaveCount(2);
            results.Should().OnlyContain(r => r.Row >= 2 && r.Row <= 3);
            results.Should().OnlyContain(r => r.Column >= 0 && r.Column <= 1);
        }

        [Fact]
        public void SearchInSheet_WithRegionName_SetsRegionNameOnResults()
        {
            // Arrange
            var file = CreateTestFileWithRegion(
                columnNames: new[] { "Name" },
                dataRows: new[] { new[] { "Alice" } },
                regionName: "People",
                regionDataStart: 1, regionDataEnd: 1,
                regionStartCol: 0, regionEndCol: 0);

            // Act
            var results = _searchService.SearchInSheet(file, "Sheet1", "Alice", regionName: "People");

            // Assert
            results.Should().HaveCount(1);
            results[0].RegionName.Should().Be("People");
        }

        [Fact]
        public void SearchInSheet_WithNonExistentRegion_ReturnsEmpty()
        {
            // Arrange
            var file = CreateTestFileWithRegion(
                columnNames: new[] { "A" },
                dataRows: new[] { new[] { "value" } },
                regionName: "Existing",
                regionDataStart: 1, regionDataEnd: 1,
                regionStartCol: 0, regionEndCol: 0);

            // Act
            var results = _searchService.SearchInSheet(file, "Sheet1", "value", regionName: "NonExistent");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void SearchInSheet_WithoutRegionName_SearchesWholeSheet()
        {
            // Arrange
            var file = CreateTestFileWithRegion(
                columnNames: new[] { "A" },
                dataRows: new[]
                {
                    new[] { "match" },
                    new[] { "match" },
                    new[] { "match" },
                },
                regionName: "Small",
                regionDataStart: 1, regionDataEnd: 1,
                regionStartCol: 0, regionEndCol: 0);

            // Act — no regionName
            var results = _searchService.SearchInSheet(file, "Sheet1", "match");

            // Assert — should find all 3 data rows
            results.Should().HaveCount(3);
        }

        [Fact]
        public void SearchInSheet_RegionWithColumnBounds_RespectsColumnFilter()
        {
            // Arrange — region only covers column 1
            var file = CreateTestFileWithRegion(
                columnNames: new[] { "A", "B", "C" },
                dataRows: new[]
                {
                    new[] { "val", "val", "val" },
                },
                regionName: "ColRegion",
                regionDataStart: 1, regionDataEnd: 1,
                regionStartCol: 1, regionEndCol: 1);

            // Act
            var results = _searchService.SearchInSheet(file, "Sheet1", "val", regionName: "ColRegion");

            // Assert — only column 1
            results.Should().HaveCount(1);
            results[0].Column.Should().Be(1);
        }

        private static ExcelFile CreateTestFileWithRegion(
            string[] columnNames,
            string[][] dataRows,
            string regionName,
            int regionDataStart,
            int regionDataEnd,
            int regionStartCol,
            int regionEndCol)
        {
            var sheet = new SASheetData("Sheet1", columnNames);

            // Header row
            var headerRow = columnNames
                .Select(n => new SACellData(SACellValue.FromText(n)))
                .ToArray();
            sheet.AddRow(headerRow);

            // Data rows
            foreach (var row in dataRows)
            {
                var cellRow = row
                    .Select(v => new SACellData(SACellValue.FromText(v)))
                    .ToArray();
                sheet.AddRow(cellRow);
            }

            // Add region
            var region = new DataRegion
            {
                Name = regionName,
                DataStartRow = regionDataStart,
                DataEndRow = regionDataEnd,
                StartColumn = regionStartCol,
                EndColumn = regionEndCol
            };
            sheet.AddDataRegion(region);

            var sheets = new Dictionary<string, SASheetData> { { "Sheet1", sheet } };
            return new ExcelFile(
                filePath: "test.xlsx",
                status: LoadStatus.Success,
                sheets: sheets,
                errors: new List<ExcelError>());
        }
    }
}
