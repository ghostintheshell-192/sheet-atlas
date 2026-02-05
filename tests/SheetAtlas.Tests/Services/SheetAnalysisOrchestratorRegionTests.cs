using FluentAssertions;
using Moq;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using Xunit;

namespace SheetAtlas.Tests.Services
{
    public class SheetAnalysisOrchestratorRegionTests
    {
        private readonly Mock<IMergedCellResolver> _mockMergedCellResolver;
        private readonly Mock<IColumnAnalysisService> _mockColumnAnalysis;
        private readonly Mock<IDataNormalizationService> _mockNormalization;
        private readonly Mock<ILogService> _mockLogger;
        private readonly SheetAnalysisOrchestrator _orchestrator;

        public SheetAnalysisOrchestratorRegionTests()
        {
            _mockMergedCellResolver = new Mock<IMergedCellResolver>();
            _mockColumnAnalysis = new Mock<IColumnAnalysisService>();
            _mockNormalization = new Mock<IDataNormalizationService>();
            _mockLogger = new Mock<ILogService>();

            // Default: normalization returns empty (no cleaning needed)
            _mockNormalization
                .Setup(n => n.Normalize(
                    It.IsAny<object>(),
                    It.IsAny<string?>(),
                    It.IsAny<CellDataType>(),
                    It.IsAny<DateSystem>()))
                .Returns(NormalizationResult.Empty);

            // Default: column analysis returns Text type
            _mockColumnAnalysis
                .Setup(c => c.AnalyzeColumn(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<SACellValue>>(),
                    It.IsAny<IReadOnlyList<string?>>(),
                    It.IsAny<DataRegion?>()))
                .Returns(new ColumnAnalysisResult
                {
                    ColumnIndex = 0,
                    ColumnName = "Col",
                    DetectedType = DataType.Text,
                    TypeConfidence = 0.95
                });

            _orchestrator = new SheetAnalysisOrchestrator(
                _mockMergedCellResolver.Object,
                _mockColumnAnalysis.Object,
                _mockNormalization.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task EnrichRegionAsync_AnalyzesColumnsWithinRegionBounds()
        {
            // Arrange
            var sheet = CreateTestSheet(5, 3); // 5 data rows, 3 columns
            var region = DataRegion.Manual("TestRegion", headerStart: 0, dataStart: 1, dataEnd: 3);
            var errors = new List<ExcelError>();

            // Act
            await _orchestrator.EnrichRegionAsync(sheet, region, errors);

            // Assert — should analyze all 3 columns
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<SACellValue>>(),
                    It.IsAny<IReadOnlyList<string?>>(),
                    region),
                Times.Exactly(3));
        }

        [Fact]
        public async Task EnrichRegionAsync_StoresPerRegionMetadata()
        {
            // Arrange
            var sheet = CreateTestSheet(5, 2);
            var region = DataRegion.Manual("Sales", headerStart: 0, dataStart: 1);
            var errors = new List<ExcelError>();

            // Act
            await _orchestrator.EnrichRegionAsync(sheet, region, errors);

            // Assert — per-region metadata should be set
            var meta0 = sheet.GetColumnMetadata("Sales", 0);
            var meta1 = sheet.GetColumnMetadata("Sales", 1);
            meta0.Should().NotBeNull();
            meta0!.DetectedType.Should().Be(DataType.Text);
            meta1.Should().NotBeNull();
        }

        [Fact]
        public async Task EnrichRegionAsync_SetsRegionIdOnCellMetadata()
        {
            // Arrange
            var sheet = CreateTestSheet(3, 1);
            var region = DataRegion.Manual("MyRegion", headerStart: 0, dataStart: 1, dataEnd: 2);
            var errors = new List<ExcelError>();

            // Act
            await _orchestrator.EnrichRegionAsync(sheet, region, errors);

            // Assert — cells within region should have RegionId set
            var cellMeta1 = sheet.GetCellMetadata(1, 0);
            var cellMeta2 = sheet.GetCellMetadata(2, 0);
            cellMeta1.Should().NotBeNull();
            cellMeta1!.RegionId.Should().Be("MyRegion");
            cellMeta2.Should().NotBeNull();
            cellMeta2!.RegionId.Should().Be("MyRegion");
        }

        [Fact]
        public async Task EnrichRegionAsync_RegionWithColumnBounds_OnlyAnalyzesThoseColumns()
        {
            // Arrange
            var sheet = CreateTestSheet(3, 5); // 5 columns
            var region = new DataRegion
            {
                Name = "Subset",
                HeaderStartRow = 0,
                DataStartRow = 1,
                StartColumn = 1,
                EndColumn = 3
            };
            var errors = new List<ExcelError>();

            // Act
            await _orchestrator.EnrichRegionAsync(sheet, region, errors);

            // Assert — should analyze columns 1, 2, 3 only
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(0, It.IsAny<string>(), It.IsAny<IReadOnlyList<SACellValue>>(), It.IsAny<IReadOnlyList<string?>>(), It.IsAny<DataRegion?>()),
                Times.Never);
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(4, It.IsAny<string>(), It.IsAny<IReadOnlyList<SACellValue>>(), It.IsAny<IReadOnlyList<string?>>(), It.IsAny<DataRegion?>()),
                Times.Never);
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(1, It.IsAny<string>(), It.IsAny<IReadOnlyList<SACellValue>>(), It.IsAny<IReadOnlyList<string?>>(), region),
                Times.Once);
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(2, It.IsAny<string>(), It.IsAny<IReadOnlyList<SACellValue>>(), It.IsAny<IReadOnlyList<string?>>(), region),
                Times.Once);
            _mockColumnAnalysis.Verify(
                c => c.AnalyzeColumn(3, It.IsAny<string>(), It.IsAny<IReadOnlyList<SACellValue>>(), It.IsAny<IReadOnlyList<string?>>(), region),
                Times.Once);
        }

        [Fact]
        public async Task EnrichRegionAsync_NullRegion_Throws()
        {
            // Arrange
            var sheet = CreateTestSheet(3, 1);
            var errors = new List<ExcelError>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _orchestrator.EnrichRegionAsync(sheet, null!, errors));
        }

        private static SASheetData CreateTestSheet(int dataRows, int columnCount)
        {
            var columnNames = Enumerable.Range(0, columnCount)
                .Select(i => $"Col{i}")
                .ToArray();

            var sheet = new SASheetData("TestSheet", columnNames);

            // Header row
            var headerRow = columnNames
                .Select(n => new SACellData(SACellValue.FromText(n)))
                .ToArray();
            sheet.AddRow(headerRow);

            // Data rows
            for (int r = 0; r < dataRows; r++)
            {
                var row = Enumerable.Range(0, columnCount)
                    .Select(c => new SACellData(SACellValue.FromText($"R{r}C{c}")))
                    .ToArray();
                sheet.AddRow(row);
            }

            return sheet;
        }
    }
}
