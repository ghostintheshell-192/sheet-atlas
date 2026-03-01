using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using FluentAssertions;
using Moq;

namespace SheetAtlas.Tests.Services
{
    public class RegionDetectionServiceTests
    {
        private readonly RegionDetectionService _service;

        public RegionDetectionServiceTests()
        {
            var mockLogger = new Mock<ILogService>();
            _service = new RegionDetectionService(mockLogger.Object);
        }

        /// <summary>
        /// Helper: create a sheet with explicit header row and data rows.
        /// Row 0 = headers from columnNames, subsequent rows = data.
        /// </summary>
        private static SASheetData CreateSheet(string sheetName, string[] columnNames, string[][] dataRows)
        {
            var sheet = new SASheetData(sheetName, columnNames, dataRows.Length + 1);

            // Add header row
            var headerCells = columnNames.Select(n => new SACellData(SACellValue.FromText(n))).ToArray();
            sheet.AddRow(headerCells);

            // Add data rows
            foreach (var row in dataRows)
            {
                var cells = new SACellData[columnNames.Length];
                for (int c = 0; c < columnNames.Length; c++)
                {
                    cells[c] = c < row.Length
                        ? new SACellData(SACellValue.FromText(row[c]))
                        : new SACellData(SACellValue.Empty);
                }
                sheet.AddRow(cells);
            }

            sheet.SetHeaderRowCount(1);
            return sheet;
        }

        /// <summary>
        /// Helper: create a sheet with a mix of data and empty rows.
        /// Accepts null arrays to represent completely empty rows.
        /// </summary>
        private static SASheetData CreateSheetWithEmptyRows(string sheetName, string[] columnNames, string?[]?[] allRows)
        {
            var sheet = new SASheetData(sheetName, columnNames, allRows.Length);

            foreach (var row in allRows)
            {
                var cells = new SACellData[columnNames.Length];
                for (int c = 0; c < columnNames.Length; c++)
                {
                    if (row != null && c < row.Length && row[c] != null)
                        cells[c] = new SACellData(SACellValue.FromText(row[c]!));
                    else
                        cells[c] = new SACellData(SACellValue.Empty);
                }
                sheet.AddRow(cells);
            }

            return sheet;
        }

        // === Header Matching ===

        [Fact]
        public void DetectRegion_ExactHeaderMatch_ReturnsFound()
        {
            var columns = new[] { "Name", "Age", "City" };
            // Source: 5 data rows (large enough to not cap the 3-row target)
            var source = CreateSheet("Source", columns, new[]
            {
                new[] { "Alice", "30", "Rome" },
                new[] { "Bob", "25", "Milan" },
                new[] { "Carol", "28", "Turin" },
                new[] { "Dan", "33", "Naples" },
                new[] { "Eve", "22", "Florence" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "People",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 5,
                StartColumn = 0,
                EndColumn = 2
            };

            var target = CreateSheet("Target", columns, new[]
            {
                new[] { "Charlie", "40", "Naples" },
                new[] { "Dave", "35", "Turin" },
                new[] { "Eve", "28", "Florence" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion.Should().NotBeNull();
            result.DetectedRegion!.Name.Should().Be("People");
            result.DetectedRegion.HeaderStartRow.Should().Be(0);
            result.DetectedRegion.DataStartRow.Should().Be(1);
            result.DetectedRegion.DataEndRow.Should().Be(3); // 3 data rows (rows 1-3)
            result.DetectedRegion.StartColumn.Should().Be(0);
            result.DetectedRegion.EndColumn.Should().Be(2);
        }

        [Fact]
        public void DetectRegion_CaseInsensitiveMatch_ReturnsFound()
        {
            var sourceColumns = new[] { "Name", "AGE", "City" };
            var source = CreateSheet("Source", sourceColumns, new[]
            {
                new[] { "Alice", "30", "Rome" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "People",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 0,
                EndColumn = 2
            };

            var targetColumns = new[] { "name", "age", "city" };
            var target = CreateSheet("Target", targetColumns, new[]
            {
                new[] { "Bob", "25", "Milan" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion.Should().NotBeNull();
        }

        [Fact]
        public void DetectRegion_NoMatchingHeaders_ReturnsNotFound()
        {
            var source = CreateSheet("Source", new[] { "Name", "Age" }, new[]
            {
                new[] { "Alice", "30" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "People",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 0,
                EndColumn = 1
            };

            var target = CreateSheet("Target", new[] { "Product", "Price" }, new[]
            {
                new[] { "Widget", "9.99" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeFalse();
            result.DetectedRegion.Should().BeNull();
            result.Message.Should().Be("Headers not found");
            result.WasTruncated.Should().BeFalse();
        }

        // === Boundary Detection: Empty Row ===

        [Fact]
        public void DetectRegion_StopsAtEmptyRow()
        {
            var columns = new[] { "A", "B" };

            // Source: 10 data rows (large cap, won't interfere)
            var source = CreateSheet("Source", columns,
                Enumerable.Range(1, 10).Select(i => new[] { $"s{i}", $"s{i}" }).ToArray());
            var sourceRegion = new DataRegion
            {
                Name = "Data",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 1
            };

            // Target: headers at row 0, 3 data rows, then empty row, then more data
            var target = CreateSheetWithEmptyRows("Target", columns, new string?[]?[]
            {
                new[] { "A", "B" },          // row 0 = headers
                new[] { "d1", "d2" },        // row 1
                new[] { "d3", "d4" },        // row 2
                new[] { "d5", "d6" },        // row 3
                null,                         // row 4 = empty (boundary)
                new[] { "other", "stuff" },  // row 5 = after boundary
            });
            target.SetHeaderRowCount(1);

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.DataStartRow.Should().Be(1);
            result.DetectedRegion.DataEndRow.Should().Be(3); // stops before empty row
            result.WasTruncated.Should().BeFalse();
        }

        [Fact]
        public void DetectRegion_EmptyRowBeforeSourceCap_StopsAtEmptyRow()
        {
            var columns = new[] { "A", "B" };

            // Source: 10 data rows (large cap)
            var source = CreateSheet("Source", columns,
                Enumerable.Range(1, 10).Select(i => new[] { $"s{i}", $"s{i}" }).ToArray());
            var sourceRegion = new DataRegion
            {
                Name = "Data",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 1
            };

            // Target: 3 data rows, then empty, then more — empty row is the definite break
            var target = CreateSheetWithEmptyRows("Target", columns, new string?[]?[]
            {
                new[] { "A", "B" },
                new[] { "d1", "d2" },
                new[] { "d3", "d4" },
                new[] { "d5", "d6" },
                null,
                new[] { "more", "data" },
            });
            target.SetHeaderRowCount(1);

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.DataEndRow.Should().Be(3);
        }

        [Fact]
        public void DetectRegion_HeadersMatchButNoDataRows_ReturnsFoundWithNullEndRow()
        {
            var columns = new[] { "A", "B" };
            var source = CreateSheet("Source", columns, new[] { new[] { "1", "2" } });
            var sourceRegion = new DataRegion
            {
                Name = "Test",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 0,
                EndColumn = 1
            };

            // Target: headers at row 0, immediately followed by empty row
            var target = CreateSheetWithEmptyRows("Target", columns, new string?[]?[]
            {
                new[] { "A", "B" },  // row 0 = headers match
                null,                 // row 1 = empty (no data)
            });
            target.SetHeaderRowCount(1);

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.HeaderStartRow.Should().Be(0);
            result.DetectedRegion.DataStartRow.Should().Be(1);
            result.DetectedRegion.DataEndRow.Should().BeNull(); // no data rows detected
        }

        // === Boundary Detection: Source Row Count Cap (Fallback) ===

        [Fact]
        public void DetectRegion_SourceRowCountCapsTarget_WhenNoEmptyRow()
        {
            var columns = new[] { "Name", "Value" };

            // Source: 2 data rows
            var source = CreateSheet("Source", columns, new[]
            {
                new[] { "A", "1" },
                new[] { "B", "2" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "Sales",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 2,
                StartColumn = 0,
                EndColumn = 1
            };

            // Target: 5 data rows, no empty row
            // Source has 2 data rows → fallback caps at 2
            var target = CreateSheet("Target", columns, new[]
            {
                new[] { "C", "3" },
                new[] { "D", "4" },
                new[] { "E", "5" },
                new[] { "F", "6" },
                new[] { "G", "7" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.DataStartRow.Should().Be(1);
            result.DetectedRegion.DataEndRow.Should().Be(2); // capped at source row count
            result.Message.Should().Contain("2 data rows");
            result.WasTruncated.Should().BeTrue();
        }

        [Fact]
        public void DetectRegion_StopsAtEndOfSheet_WhenWithinSourceRowCount()
        {
            var columns = new[] { "X", "Y" };
            // Source: 10 data rows — target has fewer, so cap doesn't kick in
            var source = CreateSheet("Source", columns,
                Enumerable.Range(1, 10).Select(i => new[] { $"s{i}", $"s{i}" }).ToArray());
            var sourceRegion = new DataRegion
            {
                Name = "Data",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 10,
                StartColumn = 0,
                EndColumn = 1
            };

            // Target has 5 data rows (fewer than source) with no empty row — should go to end
            var target = CreateSheet("Target", columns, new[]
            {
                new[] { "1", "2" },
                new[] { "3", "4" },
                new[] { "5", "6" },
                new[] { "7", "8" },
                new[] { "9", "10" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.DataEndRow.Should().Be(5); // all 5 rows, within source cap
            result.WasTruncated.Should().BeFalse();
        }

        // === Header Position ===

        [Fact]
        public void DetectRegion_HeaderNotAtRow0_FindsCorrectRow()
        {
            var columns = new[] { "Col1", "Col2", "Col3" };
            // Source: 3 data rows (large enough to not cap the 2-row target)
            var source = CreateSheet("Source", columns, new[]
            {
                new[] { "a", "b", "c" },
                new[] { "d", "e", "f" },
                new[] { "g", "h", "i" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "Data",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 3,
                StartColumn = 0,
                EndColumn = 2
            };

            // Target: headers at row 2 (some blank rows before)
            var target = CreateSheetWithEmptyRows("Target", columns, new string?[]?[]
            {
                null,                            // row 0 = empty
                null,                            // row 1 = empty
                new[] { "Col1", "Col2", "Col3" }, // row 2 = headers
                new[] { "x", "y", "z" },         // row 3 = data
                new[] { "p", "q", "r" },         // row 4 = data
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.HeaderStartRow.Should().Be(2);
            result.DetectedRegion.DataStartRow.Should().Be(3);
            result.DetectedRegion.DataEndRow.Should().Be(4);
        }

        // === Edge Cases ===

        [Fact]
        public void DetectRegion_TargetHasFewerColumns_ReturnsNotFound()
        {
            var source = CreateSheet("Source", new[] { "A", "B", "C" }, new[]
            {
                new[] { "1", "2", "3" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "Wide",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 0,
                EndColumn = 2
            };

            // Target only has 2 columns
            var target = CreateSheet("Target", new[] { "A", "B" }, new[]
            {
                new[] { "1", "2" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeFalse();
        }

        [Fact]
        public void DetectRegion_IsAutoDetectedTrue()
        {
            var columns = new[] { "Col1" };
            var source = CreateSheet("Source", columns, new[] { new[] { "data" } });
            var sourceRegion = new DataRegion
            {
                Name = "Test",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = 1,
                StartColumn = 0,
                EndColumn = 0,
                IsAutoDetected = false // source is manual
            };

            var target = CreateSheet("Target", columns, new[] { new[] { "value" } });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.IsAutoDetected.Should().BeTrue();
        }

        [Fact]
        public void DetectRegion_EmptyTargetSheet_ReturnsNotFound()
        {
            var columns = new[] { "A" };
            var source = CreateSheet("Source", columns, new[] { new[] { "data" } });
            var sourceRegion = new DataRegion
            {
                Name = "Test",
                HeaderStartRow = 0,
                DataStartRow = 1,
                StartColumn = 0,
                EndColumn = 0
            };

            // Target with only empty rows
            var target = CreateSheetWithEmptyRows("Target", columns, new string?[]?[]
            {
                null,
                null,
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeFalse();
        }

        [Fact]
        public void DetectRegion_SourceWithNullDataEndRow_NoCap()
        {
            var columns = new[] { "A", "B" };

            // Source with null DataEndRow (unbounded)
            var source = CreateSheet("Source", columns, new[]
            {
                new[] { "s1", "s2" },
            });
            var sourceRegion = new DataRegion
            {
                Name = "Data",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                DataEndRow = null, // unbounded — no cap applied
                StartColumn = 0,
                EndColumn = 1
            };

            // Target: 5 data rows, no empty row — should use all since no cap
            var target = CreateSheet("Target", columns, new[]
            {
                new[] { "t1", "t2" },
                new[] { "t3", "t4" },
                new[] { "t5", "t6" },
                new[] { "t7", "t8" },
                new[] { "t9", "t10" },
            });

            var result = _service.DetectRegion(sourceRegion, source, target);

            result.Found.Should().BeTrue();
            result.DetectedRegion!.DataEndRow.Should().Be(5); // all rows, no cap
            result.WasTruncated.Should().BeFalse();
        }
    }
}
