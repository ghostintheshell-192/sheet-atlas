using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;

namespace SheetAtlas.Tests.Domain
{
    public class SASheetDataCoordinateTests
    {
        private SASheetData CreateTestSheet(int rows = 5, int cols = 3)
        {
            var columns = Enumerable.Range(0, cols).Select(i => $"Col{i}").ToArray();
            var sheet = new SASheetData("TestSheet", columns);

            for (int r = 0; r < rows; r++)
            {
                var row = new SACellData[cols];
                for (int c = 0; c < cols; c++)
                    row[c] = new SACellData(SACellValue.FromText($"R{r}C{c}"));
                sheet.AddRow(row);
            }

            return sheet;
        }

        // === Default Origin ===

        [Fact]
        public void Origin_DefaultsToZero()
        {
            var sheet = CreateTestSheet();

            sheet.OriginRow.Should().Be(0);
            sheet.OriginColumn.Should().Be(0);
        }

        // === SetOrigin ===

        [Fact]
        public void SetOrigin_SetsValues()
        {
            var sheet = CreateTestSheet();

            sheet.SetOrigin(3, 1);

            sheet.OriginRow.Should().Be(3);
            sheet.OriginColumn.Should().Be(1);
        }

        [Fact]
        public void SetOrigin_NegativeRow_Throws()
        {
            var sheet = CreateTestSheet();

            var act = () => sheet.SetOrigin(-1, 0);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SetOrigin_NegativeColumn_Throws()
        {
            var sheet = CreateTestSheet();

            var act = () => sheet.SetOrigin(0, -1);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // === Coordinate Translation ===

        [Theory]
        [InlineData(0, 0, 3, 1)] // Local [0,0] → Excel row 3, col 1
        [InlineData(1, 0, 4, 1)] // Local [1,0] → Excel row 4, col 1
        [InlineData(0, 1, 3, 2)] // Local [0,1] → Excel row 3, col 2
        public void ToExcel_AppliesOriginOffset(int localRow, int localCol, int expectedExcelRow, int expectedExcelCol)
        {
            var sheet = CreateTestSheet();
            sheet.SetOrigin(3, 1); // Data starts at Excel row 3, column B

            sheet.ToExcelRow(localRow).Should().Be(expectedExcelRow);
            sheet.ToExcelColumn(localCol).Should().Be(expectedExcelCol);
        }

        [Theory]
        [InlineData(3, 1, 0, 0)] // Excel row 3, col B → local [0,0]
        [InlineData(5, 3, 2, 2)] // Excel row 5, col D → local [2,2]
        public void ToLocal_SubtractsOriginOffset(int excelRow, int excelCol, int expectedLocalRow, int expectedLocalCol)
        {
            var sheet = CreateTestSheet();
            sheet.SetOrigin(3, 1);

            sheet.ToLocalRow(excelRow).Should().Be(expectedLocalRow);
            sheet.ToLocalColumn(excelCol).Should().Be(expectedLocalCol);
        }

        [Fact]
        public void ToExcel_WithZeroOrigin_ReturnsLocalIndices()
        {
            var sheet = CreateTestSheet();
            // Origin defaults to (0, 0)

            sheet.ToExcelRow(2).Should().Be(2);
            sheet.ToExcelColumn(1).Should().Be(1);
        }

        // === GetCellReference ===

        [Fact]
        public void GetCellReference_ZeroOrigin_ReturnsCorrectReference()
        {
            var sheet = CreateTestSheet();

            sheet.GetCellReference(0, 0).Should().Be("A1");
            sheet.GetCellReference(0, 2).Should().Be("C1");
            sheet.GetCellReference(4, 0).Should().Be("A5");
        }

        [Fact]
        public void GetCellReference_WithOriginOffset_ReturnsExcelReference()
        {
            var sheet = CreateTestSheet();
            sheet.SetOrigin(2, 1); // Data starts at Excel row 3, column B

            sheet.GetCellReference(0, 0).Should().Be("B3"); // Local [0,0] = Excel B3
            sheet.GetCellReference(0, 1).Should().Be("C3"); // Local [0,1] = Excel C3
            sheet.GetCellReference(1, 0).Should().Be("B4"); // Local [1,0] = Excel B4
        }

        [Fact]
        public void GetCellReference_LargeColumnIndex_ReturnsMultiLetterColumn()
        {
            var columns = Enumerable.Range(0, 30).Select(i => $"Col{i}").ToArray();
            var sheet = new SASheetData("TestSheet", columns);
            for (int r = 0; r < 2; r++)
            {
                var row = new SACellData[30];
                for (int c = 0; c < 30; c++)
                    row[c] = new SACellData(SACellValue.FromText($"R{r}C{c}"));
                sheet.AddRow(row);
            }

            // Column index 25 = Z, 26 = AA
            sheet.GetCellReference(0, 25).Should().Be("Z1");
            sheet.GetCellReference(0, 26).Should().Be("AA1");
            sheet.GetCellReference(0, 27).Should().Be("AB1");
        }

        // === GetColumnLetter (via GetCellReference) ===

        [Fact]
        public void GetCellReference_ColumnLetterConversion()
        {
            var sheet = CreateTestSheet();

            // Verify A=0, B=1, C=2 via cell references
            sheet.GetCellReference(0, 0).Should().StartWith("A");
            sheet.GetCellReference(0, 1).Should().StartWith("B");
            sheet.GetCellReference(0, 2).Should().StartWith("C");
        }

        // === Roundtrip ===

        [Theory]
        [InlineData(0, 0)]
        [InlineData(5, 10)]
        [InlineData(100, 25)]
        public void ToExcel_ToLocal_Roundtrip(int originRow, int originCol)
        {
            var sheet = CreateTestSheet();
            sheet.SetOrigin(originRow, originCol);

            for (int localRow = 0; localRow < sheet.RowCount; localRow++)
            {
                for (int localCol = 0; localCol < sheet.ColumnCount; localCol++)
                {
                    int excelRow = sheet.ToExcelRow(localRow);
                    int excelCol = sheet.ToExcelColumn(localCol);

                    sheet.ToLocalRow(excelRow).Should().Be(localRow);
                    sheet.ToLocalColumn(excelCol).Should().Be(localCol);
                }
            }
        }
    }
}
