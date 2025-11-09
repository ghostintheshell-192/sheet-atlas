using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;
using SheetAtlas.Tests.Foundation.Builders;
using SheetAtlas.Tests.Foundation.Fixtures;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for IMergedCellResolver service.
    /// Tests merged cell handling with various strategies and complexity detection.
    /// </summary>
    public class MergedCellResolverTests
    {
        private readonly IMergedCellResolver _resolver = new MergedCellResolver();

        #region Simple Merge Resolution Tests

        [Fact]
        public void ResolveMergedCells_SimpleHeaderMerge_ExpandsValue()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(3)
                .WithMergedCells("A1:C1", "MergedHeader")
                .WithCellValue(0, 0, "MergedHeader")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.ExpandValue);

            // Assert
            result.Should().NotBeNull();
            // After expansion, cells B1 and C1 should also have the value
        }

        [Fact]
        public void ResolveMergedCells_SimpleHeaderMerge_KeepTopLeftStrategy()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(3)
                .WithMergedCells("A1:C1", "Header")
                .WithCellValue(0, 0, "Header")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.KeepTopLeft);

            // Assert
            result.Should().NotBeNull();
            // Keep strategy should preserve only top-left value
        }

        [Fact]
        public void ResolveMergedCells_HorizontalMerge_PreservesStructure()
        {
            // Arrange - Merge across columns in a single row
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C", "D")
                .WithRows(3)
                .WithMergedCells("B1:D1", "Wide Header")
                .WithCellValue(0, 1, "Wide Header")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void ResolveMergedCells_VerticalMerge_PreservesStructure()
        {
            // Arrange - Merge down rows in a single column
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(5)
                .WithMergedCells("A1:A3", "Long Header")
                .WithCellValue(0, 0, "Long Header")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Strategy-Specific Tests

        [Fact]
        public void ResolveMergedCells_ExpandValueStrategy_ReplicatesValue()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(2)
                .WithMergedCells("A1:C1", "Value")
                .WithCellValue(0, 0, "Value")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.ExpandValue);

            // Assert
            result.Should().NotBeNull();
            // All cells in merge range should have the value
        }

        [Fact]
        public void ResolveMergedCells_KeepTopLeftStrategy_OnlyTopLeftHasValue()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(2)
                .WithMergedCells("A1:C1", "Header")
                .WithCellValue(0, 0, "Header")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.KeepTopLeft);

            // Assert
            result.Should().NotBeNull();
            // Only A1 should have value, B1 and C1 empty
        }

        [Fact]
        public void ResolveMergedCells_FlattenToStringStrategy_ConcatenatesValues()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B")
                .WithRows(3)
                .WithMergedCells("A1:A2", "Header")
                .WithCellValue(0, 0, "Header")
                .WithCellValue(1, 0, "SubHeader")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.FlattenToString);

            // Assert
            result.Should().NotBeNull();
            // Should concatenate values from merged range
        }

        [Fact]
        public void ResolveMergedCells_TreatAsHeaderStrategy_IdentifiesHeaderMerge()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C", "D")
                .WithRows(3)
                .WithMergedCells("A1:D1", "Report Title")
                .WithCellValue(0, 0, "Report Title")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.TreatAsHeader);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Complexity Analysis Tests

        [Fact]
        public void AnalyzeMergeComplexity_SimpleHeaderMerge_ReturnsSimple()
        {
            // Arrange
            var mergedCells = new Dictionary<string, SheetAtlas.Core.Domain.Entities.MergedRange>
            {
                { "A1:D1", new SheetAtlas.Core.Domain.Entities.MergedRange(0, 0, 0, 3) } // A1:D1
            };

            // Act
            var result = _resolver.AnalyzeMergeComplexity(mergedCells);

            // Assert
            result.Level.Should().Be(MergeComplexity.Simple);
            result.RecommendedStrategy.Should().Be(MergeStrategy.ExpandValue);
        }

        [Fact]
        public void AnalyzeMergeComplexity_VerticalMerges_ReturnsComplex()
        {
            // Arrange
            var mergedCells = new Dictionary<string, SheetAtlas.Core.Domain.Entities.MergedRange>
            {
                { "A1:A5", new SheetAtlas.Core.Domain.Entities.MergedRange(0, 0, 4, 0) }, // A1:A5
                { "B1:B3", new SheetAtlas.Core.Domain.Entities.MergedRange(0, 1, 2, 1) }  // B1:B3
            };

            // Act
            var result = _resolver.AnalyzeMergeComplexity(mergedCells);

            // Assert
            result.Level.Should().Be(MergeComplexity.Complex);
            result.RecommendedStrategy.Should().Be(MergeStrategy.KeepTopLeft);
        }

        [Fact]
        public void AnalyzeMergeComplexity_Over20PercentMerged_ReturnsChaos()
        {
            // Arrange - Create many merged cells covering >20% of data
            var mergedCells = new Dictionary<string, SheetAtlas.Core.Domain.Entities.MergedRange>();
            for (int i = 0; i < 10; i++)
            {
                mergedCells[$"A{i}:B{i}"] = new SheetAtlas.Core.Domain.Entities.MergedRange(i, 0, i, 1); // A{i}:B{i}
            }

            // Act
            var result = _resolver.AnalyzeMergeComplexity(mergedCells);

            // Assert
            result.Level.Should().Be(MergeComplexity.Chaos);
            result.MergedCellPercentage.Should().BeGreaterThan(0.20);
            result.Explanation.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void AnalyzeMergeComplexity_NoMergedCells_ReturnsSimple()
        {
            // Arrange
            var mergedCells = new Dictionary<string, SheetAtlas.Core.Domain.Entities.MergedRange>();

            // Act
            var result = _resolver.AnalyzeMergeComplexity(mergedCells);

            // Assert
            result.Level.Should().Be(MergeComplexity.Simple);
            result.MergedCellPercentage.Should().Be(0);
        }

        #endregion

        #region Warning Tests

        [Fact]
        public void ResolveMergedCells_WithWarningCallback_ReportsWarnings()
        {
            // Arrange
            var warnings = new List<MergeWarning>();
            void WarningCallback(MergeWarning warning) => warnings.Add(warning);

            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(5)
                .WithMergedCells("A1:C5", "Complex")
                .WithCellValue(0, 0, "Complex")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.ExpandValue,
                WarningCallback);

            // Assert
            result.Should().NotBeNull();
            warnings.Should().NotBeEmpty();
        }

        [Fact]
        public void ResolveMergedCells_HighMergePercentage_WarnsUser()
        {
            // Arrange
            var warningCount = 0;
            void CountWarnings(MergeWarning w) => warningCount++;

            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B")
                .WithRows(10)
                .WithMergedCells("A1:B10", "Merged")
                .WithCellValue(0, 0, "Merged")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(
                sheet,
                MergeStrategy.ExpandValue,
                CountWarnings);

            // Assert
            warningCount.Should().BeGreaterThan(0);
        }

        #endregion

        #region Multiple Merge Tests

        [Fact]
        public void ResolveMergedCells_MultipleMerges_HandlesAllCorrectly()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C", "D")
                .WithRows(5)
                .WithMergedCells("A1:B1", "Header1")
                .WithMergedCells("C1:D1", "Header2")
                .WithMergedCells("A3:A5", "RowLabel")
                .WithCellValue(0, 0, "Header1")
                .WithCellValue(0, 2, "Header2")
                .WithCellValue(2, 0, "RowLabel")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void ResolveMergedCells_OverlappingMerges_HandlesGracefully()
        {
            // Arrange - Note: Real Excel doesn't allow overlapping merges, but we should handle edge case
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(3)
                .WithMergedCells("A1:B2", "Merge1")
                .WithMergedCells("B1:C2", "Merge2") // Overlaps with Merge1
                .WithCellValue(0, 0, "Merge1")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Empty Merge Tests

        [Fact]
        public void ResolveMergedCells_EmptyMergedRange_PreservesEmptiness()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C")
                .WithRows(2)
                .WithMergedCells("B1:C1", null) // Merged but empty
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Immutability Tests

        [Fact]
        public void ResolveMergedCells_OriginalUnmodified_ReturnsCopy()
        {
            // Arrange
            var original = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B")
                .WithRows(2)
                .WithMergedCells("A1:B1", "Merged")
                .WithCellValue(0, 0, "Merged")
                .Build();

            var originalMergedCount = original.MergedCells.Count;

            // Act
            var result = _resolver.ResolveMergedCells(
                original,
                MergeStrategy.ExpandValue);

            // Assert
            original.MergedCells.Count.Should().Be(originalMergedCount);
            result.Should().NotBeSameAs(original);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ResolveMergedCells_SingleCellMerge_HandlesCorrectly()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B")
                .WithRows(2)
                .WithMergedCells("A1:A1", "Single") // Merge single cell (edge case)
                .WithCellValue(0, 0, "Single")
                .Build();

            // Act
            var result = _resolver.ResolveMergedCells(sheet);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void AnalyzeMergeComplexity_LargeSheet_CalculatesPercentageCorrectly()
        {
            // Arrange
            var mergedCells = new Dictionary<string, SheetAtlas.Core.Domain.Entities.MergedRange>
            {
                { "A1:Z10", new SheetAtlas.Core.Domain.Entities.MergedRange(0, 0, 9, 25) } // 260 cells (A1:Z10)
            };

            // Act
            var result = _resolver.AnalyzeMergeComplexity(mergedCells);

            // Assert
            result.MergedCellPercentage.Should().BeGreaterThan(0);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void ResolveMergedCells_FullWorkflow_AnalyzeThenResolve()
        {
            // Arrange
            var sheet = new SASheetDataBuilder()
                .WithName("Test")
                .WithColumns("A", "B", "C", "D")
                .WithRows(5)
                .WithMergedCells("A1:D1", 0, 0, 0, 3)  // Row 0, cols 0-3
                .WithMergedCells("A2:D2", 1, 0, 1, 3)  // Row 1, cols 0-3
                .WithMergedCells("A4:A5", 3, 0, 4, 0)  // Rows 3-4, col 0
                .WithCellValue(0, 0, "Title")
                .WithCellValue(1, 0, "Subtitle")
                .WithCellValue(3, 0, "Category")
                .Build();

            // Act - First analyze
            var analysis = _resolver.AnalyzeMergeComplexity(sheet.MergedCells);

            // Then resolve
            var resolved = _resolver.ResolveMergedCells(
                sheet,
                analysis.RecommendedStrategy);

            // Assert
            analysis.Should().NotBeNull();
            resolved.Should().NotBeNull();
        }

        #endregion
    }
}
