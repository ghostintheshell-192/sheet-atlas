using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;

namespace SheetAtlas.Tests.Domain
{
    public class DataRegionTests
    {
        // === Factory Methods ===

        [Fact]
        public void AutoDetect_SetsNameAndDefaults()
        {
            var region = DataRegion.AutoDetect("Sheet1");

            region.Name.Should().Be("Sheet1");
            region.DataStartRow.Should().Be(0);
            region.IsAutoDetected.Should().BeTrue();
            region.StartColumn.Should().BeNull();
            region.EndColumn.Should().BeNull();
        }

        [Fact]
        public void Manual_SetsAllProperties()
        {
            var region = DataRegion.Manual("Sales", headerStart: 0, dataStart: 1, dataEnd: 50);

            region.Name.Should().Be("Sales");
            region.HeaderStartRow.Should().Be(0);
            region.DataStartRow.Should().Be(1);
            region.DataEndRow.Should().Be(50);
            region.IsAutoDetected.Should().BeFalse();
        }

        [Fact]
        public void FromDataRange_SetsNameAndRange()
        {
            var region = DataRegion.FromDataRange("Data", dataStart: 5, dataEnd: 100);

            region.Name.Should().Be("Data");
            region.DataStartRow.Should().Be(5);
            region.DataEndRow.Should().Be(100);
            region.IsAutoDetected.Should().BeFalse();
        }

        [Fact]
        public void WholeSheet_CoversEntireSheet()
        {
            var region = DataRegion.WholeSheet("Sheet1", rowCount: 100, colCount: 10);

            region.Name.Should().Be("Sheet1");
            region.HeaderStartRow.Should().Be(0);
            region.HeaderEndRow.Should().Be(0);
            region.DataStartRow.Should().Be(1);
            region.DataEndRow.Should().Be(99);
            region.StartColumn.Should().Be(0);
            region.EndColumn.Should().Be(9);
            region.IsAutoDetected.Should().BeTrue();
        }

        [Fact]
        public void WholeSheet_SingleRow_DataEndIsNull()
        {
            var region = DataRegion.WholeSheet("Sheet1", rowCount: 1, colCount: 5);

            region.DataEndRow.Should().BeNull();
        }

        // === IsValid ===

        [Fact]
        public void IsValid_ValidRegion_ReturnsTrue()
        {
            var region = DataRegion.WholeSheet("Sheet1", rowCount: 50, colCount: 10);

            region.IsValid().Should().BeTrue();
        }

        [Fact]
        public void IsValid_EmptyName_ReturnsFalse()
        {
            var region = new DataRegion { Name = "", DataStartRow = 0 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_WhitespaceName_ReturnsFalse()
        {
            var region = new DataRegion { Name = "  ", DataStartRow = 0 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_NegativeDataStartRow_ReturnsFalse()
        {
            var region = new DataRegion { Name = "Test", DataStartRow = -1 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_NegativeHeaderStartRow_ReturnsFalse()
        {
            var region = new DataRegion { Name = "Test", HeaderStartRow = -1, DataStartRow = 0 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_HeaderEndBeforeStart_ReturnsFalse()
        {
            var region = new DataRegion
            {
                Name = "Test",
                HeaderStartRow = 5,
                HeaderEndRow = 3,
                DataStartRow = 6
            };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_DataEndBeforeStart_ReturnsFalse()
        {
            var region = new DataRegion { Name = "Test", DataStartRow = 10, DataEndRow = 5 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_NegativeStartColumn_ReturnsFalse()
        {
            var region = new DataRegion { Name = "Test", DataStartRow = 0, StartColumn = -1 };

            region.IsValid().Should().BeFalse();
        }

        [Fact]
        public void IsValid_EndColumnBeforeStartColumn_ReturnsFalse()
        {
            var region = new DataRegion
            {
                Name = "Test",
                DataStartRow = 0,
                StartColumn = 5,
                EndColumn = 3
            };

            region.IsValid().Should().BeFalse();
        }

        // === OverlapsWith ===

        [Fact]
        public void OverlapsWith_IdenticalRegions_ReturnsTrue()
        {
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 5
            };

            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 5
            };

            region1.OverlapsWith(region2).Should().BeTrue();
        }

        [Fact]
        public void OverlapsWith_PartialOverlap_ReturnsTrue()
        {
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 5
            };

            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 5,
                DataEndRow = 15,
                StartColumn = 3,
                EndColumn = 8
            };

            region1.OverlapsWith(region2).Should().BeTrue();
        }

        [Fact]
        public void OverlapsWith_VerticallyStacked_ReturnsFalse()
        {
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 5
            };

            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 11,
                DataEndRow = 20,
                StartColumn = 0,
                EndColumn = 5
            };

            region1.OverlapsWith(region2).Should().BeFalse();
        }

        [Fact]
        public void OverlapsWith_HorizontallyAdjacent_ReturnsFalse()
        {
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 3
            };

            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 0,
                DataEndRow = 10,
                StartColumn = 4,
                EndColumn = 7
            };

            region1.OverlapsWith(region2).Should().BeFalse();
        }

        [Fact]
        public void OverlapsWith_SameRowsDifferentColumns_ReturnsFalse()
        {
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 20,
                StartColumn = 0,
                EndColumn = 2
            };

            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 0,
                DataEndRow = 20,
                StartColumn = 5,
                EndColumn = 8
            };

            region1.OverlapsWith(region2).Should().BeFalse();
        }

        // === HeaderRowCount ===

        [Fact]
        public void HeaderRowCount_MultiRowHeader_ReturnsCorrectCount()
        {
            var region = new DataRegion
            {
                Name = "Test",
                HeaderStartRow = 0,
                HeaderEndRow = 2,
                DataStartRow = 3
            };

            region.HeaderRowCount.Should().Be(3);
        }

        [Fact]
        public void HeaderRowCount_NoHeaderSpecified_ReturnsOne()
        {
            var region = new DataRegion { Name = "Test", DataStartRow = 0 };

            region.HeaderRowCount.Should().Be(1);
        }
    }
}
