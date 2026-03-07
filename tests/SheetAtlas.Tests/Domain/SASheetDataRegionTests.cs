using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;

namespace SheetAtlas.Tests.Domain
{
    public class SASheetDataRegionTests
    {
        private SASheetData CreateTestSheet(int rows = 10, int cols = 4)
        {
            var columns = Enumerable.Range(0, cols).Select(i => $"Col{i}").ToArray();
            var sheet = new SASheetData("TestSheet", columns);

            for (int r = 0; r < rows; r++)
            {
                var row = new SACellData[cols];
                for (int c = 0; c < cols; c++)
                {
                    row[c] = new SACellData(SACellValue.FromText($"R{r}C{c}"));
                }
                sheet.AddRow(row);
            }

            return sheet;
        }

        // === AddDataRegion ===

        [Fact]
        public void AddDataRegion_ValidRegion_AddsSuccessfully()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion
            {
                Name = "Sales",
                DataStartRow = 1,
                DataEndRow = 5,
                StartColumn = 0,
                EndColumn = 3
            };

            sheet.AddDataRegion(region);

            sheet.DataRegions.Should().ContainKey("Sales");
            sheet.DataRegions["Sales"].Should().Be(region);
        }

        [Fact]
        public void AddDataRegion_DuplicateName_ThrowsInvalidOperation()
        {
            var sheet = CreateTestSheet();
            var region1 = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 3 };
            var region2 = new DataRegion { Name = "Sales", DataStartRow = 5, DataEndRow = 8 };

            sheet.AddDataRegion(region1);

            sheet.Invoking(s => s.AddDataRegion(region2))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*already exists*");
        }

        [Fact]
        public void AddDataRegion_OverlappingRegion_ThrowsInvalidOperation()
        {
            var sheet = CreateTestSheet();
            var region1 = new DataRegion
            {
                Name = "A",
                DataStartRow = 0,
                DataEndRow = 5,
                StartColumn = 0,
                EndColumn = 3
            };
            var region2 = new DataRegion
            {
                Name = "B",
                DataStartRow = 3,
                DataEndRow = 8,
                StartColumn = 1,
                EndColumn = 3
            };

            sheet.AddDataRegion(region1);

            sheet.Invoking(s => s.AddDataRegion(region2))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*overlaps*");
        }

        [Fact]
        public void AddDataRegion_AdjacentRegions_DoNotOverlap()
        {
            var sheet = CreateTestSheet();
            var region1 = new DataRegion
            {
                Name = "Top",
                DataStartRow = 0,
                DataEndRow = 4,
                StartColumn = 0,
                EndColumn = 3
            };
            var region2 = new DataRegion
            {
                Name = "Bottom",
                DataStartRow = 5,
                DataEndRow = 9,
                StartColumn = 0,
                EndColumn = 3
            };

            sheet.AddDataRegion(region1);
            sheet.AddDataRegion(region2);

            sheet.DataRegions.Should().HaveCount(2);
        }

        [Fact]
        public void AddDataRegion_EmptyName_ThrowsArgumentException()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "", DataStartRow = 0 };

            sheet.Invoking(s => s.AddDataRegion(region))
                .Should().Throw<ArgumentException>()
                .WithMessage("*name*");
        }

        [Fact]
        public void AddDataRegion_NullRegion_ThrowsArgumentNullException()
        {
            var sheet = CreateTestSheet();

            sheet.Invoking(s => s.AddDataRegion(null!))
                .Should().Throw<ArgumentNullException>();
        }

        // === RemoveDataRegion ===

        [Fact]
        public void RemoveDataRegion_ExistingRegion_RemovesSuccessfully()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 5 };
            sheet.AddDataRegion(region);

            sheet.RemoveDataRegion("Sales");

            sheet.DataRegions.Should().NotContainKey("Sales");
        }

        [Fact]
        public void RemoveDataRegion_ClearsRegionColumnMetadata()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 5 };
            sheet.AddDataRegion(region);

            var metadata = new ColumnMetadata { DetectedType = DataType.Number, TypeConfidence = 0.95 };
            sheet.SetColumnMetadata("Sales", 0, metadata);

            // Verify metadata exists
            sheet.GetColumnMetadata("Sales", 0).Should().NotBeNull();

            // Remove region
            sheet.RemoveDataRegion("Sales");

            // Metadata should be gone
            sheet.GetColumnMetadata("Sales", 0).Should().BeNull();
        }

        [Fact]
        public void RemoveDataRegion_NonExistent_NoOp()
        {
            var sheet = CreateTestSheet();

            // Should not throw
            sheet.Invoking(s => s.RemoveDataRegion("NonExistent"))
                .Should().NotThrow();
        }

        // === GetDataRegion ===

        [Fact]
        public void GetDataRegion_ExistingRegion_ReturnsRegion()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 5 };
            sheet.AddDataRegion(region);

            var result = sheet.GetDataRegion("Sales");

            result.Should().Be(region);
        }

        [Fact]
        public void GetDataRegion_NonExistent_ReturnsNull()
        {
            var sheet = CreateTestSheet();

            sheet.GetDataRegion("NonExistent").Should().BeNull();
        }

        // === DataRegions property ===

        [Fact]
        public void DataRegions_EmptyByDefault()
        {
            var sheet = CreateTestSheet();

            sheet.DataRegions.Should().BeEmpty();
        }

        [Fact]
        public void DataRegions_ReturnsReadOnlyView()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 5 };
            sheet.AddDataRegion(region);

            var regions = sheet.DataRegions;

            regions.Should().HaveCount(1);
            regions.Should().ContainKey("Sales");
        }

        // === EnumerateDataRows with region ===

        [Fact]
        public void EnumerateDataRows_WithRegion_OnlyReturnsRowsInRegion()
        {
            var sheet = CreateTestSheet(rows: 10);
            var region = new DataRegion { Name = "Middle", DataStartRow = 3, DataEndRow = 6 };

            var rows = sheet.EnumerateDataRows(region).ToList();

            rows.Should().HaveCount(4); // rows 3, 4, 5, 6
            rows[0][0].EffectiveValue.AsText().Should().Be("R3C0");
            rows[3][0].EffectiveValue.AsText().Should().Be("R6C0");
        }

        [Fact]
        public void EnumerateDataRows_RegionBeyondSheet_ClampsToBounds()
        {
            var sheet = CreateTestSheet(rows: 5);
            var region = new DataRegion { Name = "Big", DataStartRow = 2, DataEndRow = 100 };

            var rows = sheet.EnumerateDataRows(region).ToList();

            rows.Should().HaveCount(3); // rows 2, 3, 4 (clamped to sheet bounds)
        }

        [Fact]
        public void EnumerateDataRows_RegionStartBeyondSheet_ReturnsEmpty()
        {
            var sheet = CreateTestSheet(rows: 5);
            var region = new DataRegion { Name = "Outside", DataStartRow = 100, DataEndRow = 200 };

            var rows = sheet.EnumerateDataRows(region).ToList();

            rows.Should().BeEmpty();
        }

        [Fact]
        public void EnumerateDataRows_RegionWithNoEndRow_GoesToEnd()
        {
            var sheet = CreateTestSheet(rows: 10);
            var region = new DataRegion { Name = "ToEnd", DataStartRow = 7 };

            var rows = sheet.EnumerateDataRows(region).ToList();

            rows.Should().HaveCount(3); // rows 7, 8, 9
        }

        // === Per-region ColumnMetadata ===

        [Fact]
        public void GetSetColumnMetadata_PerRegion_IndependentFromGlobal()
        {
            var sheet = CreateTestSheet();

            var globalMeta = new ColumnMetadata { DetectedType = DataType.Text, TypeConfidence = 0.9 };
            var regionMeta = new ColumnMetadata { DetectedType = DataType.Number, TypeConfidence = 0.95 };

            sheet.SetColumnMetadata(0, globalMeta);
            sheet.SetColumnMetadata("Sales", 0, regionMeta);

            sheet.GetColumnMetadata(0).Should().Be(globalMeta);
            sheet.GetColumnMetadata("Sales", 0).Should().Be(regionMeta);
        }

        [Fact]
        public void SetColumnMetadata_DifferentRegions_DoNotInterfere()
        {
            var sheet = CreateTestSheet();

            var metaA = new ColumnMetadata { DetectedType = DataType.Number };
            var metaB = new ColumnMetadata { DetectedType = DataType.Date };

            sheet.SetColumnMetadata("RegionA", 0, metaA);
            sheet.SetColumnMetadata("RegionB", 0, metaB);

            sheet.GetColumnMetadata("RegionA", 0).Should().Be(metaA);
            sheet.GetColumnMetadata("RegionB", 0).Should().Be(metaB);
        }

        [Fact]
        public void GetColumnMetadata_NoMetadataSet_ReturnsNull()
        {
            var sheet = CreateTestSheet();

            sheet.GetColumnMetadata("NonExistent", 0).Should().BeNull();
        }

        // === Dispose cleans up regions ===

        [Fact]
        public void Dispose_ClearsRegionData()
        {
            var sheet = CreateTestSheet();
            var region = new DataRegion { Name = "Sales", DataStartRow = 0, DataEndRow = 5 };
            sheet.AddDataRegion(region);
            sheet.SetColumnMetadata("Sales", 0, new ColumnMetadata { DetectedType = DataType.Number });

            sheet.Dispose();

            sheet.DataRegions.Should().BeEmpty();
            sheet.GetColumnMetadata("Sales", 0).Should().BeNull();
        }
    }
}
