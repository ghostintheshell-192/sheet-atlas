using FluentAssertions;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Tests.Foundation.Builders;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for ITemplateValidationService.
    /// Tests template creation from files and file validation against templates.
    /// </summary>
    public class TemplateValidationServiceTests
    {
        private readonly ITemplateValidationService _service = new TemplateValidationService();

        #region CreateTemplateFromFileAsync Tests

        [Fact]
        public async Task CreateTemplateFromFileAsync_ValidFile_CreatesTemplateWithColumns()
        {
            // Arrange
            var file = CreateSampleExcelFile("SampleFile.xlsx", new[]
            {
                ("Amount", DataType.Number),
                ("Date", DataType.Date),
                ("Description", DataType.Text)
            });

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "TestTemplate");

            // Assert
            template.Name.Should().Be("TestTemplate");
            template.Columns.Should().HaveCount(3);
            template.SourceFilePath.Should().Be(file.FilePath);
            template.ExpectedSheetName.Should().Be("Sheet1");
        }

        [Fact]
        public async Task CreateTemplateFromFileAsync_DetectsColumnTypes()
        {
            // Arrange
            var file = CreateSampleExcelFile("NumberFile.xlsx", new[]
            {
                ("Amount", DataType.Number),
                ("Price", DataType.Number)
            });

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "NumberTemplate");

            // Assert
            template.Columns.Should().Contain(c => c.Name == "Amount");
            template.Columns.Should().Contain(c => c.Name == "Price");
            template.Columns.All(c => c.ExpectedType == DataType.Number).Should().BeTrue();
        }

        [Fact]
        public async Task CreateTemplateFromFileAsync_SetsDefaultRules()
        {
            // Arrange
            var file = CreateSampleExcelFile("RulesFile.xlsx", new[]
            {
                ("ID", DataType.Number)
            });

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "RulesTemplate");

            // Assert
            var column = template.Columns.First();
            column.Rules.Should().Contain(r => r.Type == RuleType.NotEmpty);
            column.Rules.Should().Contain(r => r.Type == RuleType.TypeMatch);
        }

        [Fact]
        public async Task CreateTemplateFromFileAsync_SpecificSheet_UsesCorrectSheet()
        {
            // Arrange
            var sheet1 = CreateSampleSheet("Sheet1", new[] { ("Col1", DataType.Text) });
            var sheet2 = CreateSampleSheet("Sheet2", new[] { ("Col2", DataType.Number), ("Col3", DataType.Date) });

            var file = new ExcelFile(
                "/test/MultiSheet.xlsx",
                LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet1 }, { "Sheet2", sheet2 } },
                new List<ExcelError>());

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "Sheet2Template", "Sheet2");

            // Assert
            template.ExpectedSheetName.Should().Be("Sheet2");
            template.Columns.Should().HaveCount(2);
            template.Columns.Should().Contain(c => c.Name == "Col2");
            template.Columns.Should().Contain(c => c.Name == "Col3");
        }

        #endregion

        #region ValidateAsync Tests

        [Fact]
        public async Task ValidateAsync_MatchingFile_ReturnsValidReport()
        {
            // Arrange
            var file = CreateSampleExcelFile("Valid.xlsx", new[]
            {
                ("Amount", DataType.Number),
                ("Description", DataType.Text)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Required("Description", DataType.Text));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Passed.Should().BeTrue();
            report.Status.Should().Be(ValidationStatus.Valid);
            report.TotalErrorCount.Should().Be(0);
        }

        [Fact]
        public async Task ValidateAsync_MissingRequiredColumn_ReturnsError()
        {
            // Arrange
            var file = CreateSampleExcelFile("MissingCol.xlsx", new[]
            {
                ("Amount", DataType.Number)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Required("MissingColumn", DataType.Text));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Passed.Should().BeFalse();
            report.Status.Should().Be(ValidationStatus.Invalid);
            report.AllIssues.Should().Contain(i => i.IssueType == ValidationIssueType.MissingColumn);
            report.MissingRequiredCount.Should().Be(1);
        }

        [Fact]
        public async Task ValidateAsync_MissingOptionalColumn_PassesWithInfo()
        {
            // Arrange
            var file = CreateSampleExcelFile("OptionalMissing.xlsx", new[]
            {
                ("Amount", DataType.Number)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Optional("OptionalCol", DataType.Text));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Passed.Should().BeTrue();
            report.AllIssues.Should().Contain(i =>
                i.IssueType == ValidationIssueType.MissingColumn &&
                i.Severity == ValidationSeverity.Warning);
        }

        [Fact]
        public async Task ValidateAsync_TypeMismatch_ReturnsError()
        {
            // Arrange
            var file = CreateSampleExcelFile("TypeMismatch.xlsx", new[]
            {
                ("Amount", DataType.Text) // Text instead of Number
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Passed.Should().BeFalse();
            report.AllIssues.Should().Contain(i => i.IssueType == ValidationIssueType.TypeMismatch);
        }

        [Fact]
        public async Task ValidateAsync_WrongPosition_ReturnsWarning()
        {
            // Arrange
            var file = CreateSampleExcelFile("WrongPos.xlsx", new[]
            {
                ("Description", DataType.Text), // At position 0
                ("Amount", DataType.Number)     // At position 1
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.RequiredAt("Amount", DataType.Number, 0)); // Expects Amount at position 0

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.AllIssues.Should().Contain(i => i.IssueType == ValidationIssueType.WrongPosition);
        }

        [Fact]
        public async Task ValidateAsync_ExtraColumnsNotAllowed_ReturnsInfo()
        {
            // Arrange
            var file = CreateSampleExcelFile("ExtraCols.xlsx", new[]
            {
                ("Amount", DataType.Number),
                ("ExtraColumn", DataType.Text)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .DisallowExtraColumns();

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.AllIssues.Should().Contain(i => i.IssueType == ValidationIssueType.ExtraColumn);
        }

        [Fact]
        public async Task ValidateAsync_SheetNotFound_ReturnsFailed()
        {
            // Arrange
            var file = CreateSampleExcelFile("Test.xlsx", new[]
            {
                ("Amount", DataType.Number)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .ForSheet("NonExistentSheet")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Status.Should().Be(ValidationStatus.Failed);
            report.Passed.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateAsync_ColumnWithAlternativeNames_MatchesAlternative()
        {
            // Arrange
            var file = CreateSampleExcelFile("AltNames.xlsx", new[]
            {
                ("Total Amount", DataType.Number) // Alternative name
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number)
                    .WithAlternatives("Total Amount", "Sum"));

            // Act
            var report = await _service.ValidateAsync(file, template);

            // Assert
            report.Passed.Should().BeTrue();
            var colResult = report.ColumnResults.First();
            colResult.Found.Should().BeTrue();
            colResult.ActualName.Should().Be("Total Amount");
        }

        #endregion

        #region QuickStructureCheck Tests

        [Fact]
        public void QuickStructureCheck_MatchingStructure_ReturnsTrue()
        {
            // Arrange
            var file = CreateSampleExcelFile("Valid.xlsx", new[]
            {
                ("Amount", DataType.Number),
                ("Description", DataType.Text)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Required("Description", DataType.Text));

            // Act
            var result = _service.QuickStructureCheck(file, template);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void QuickStructureCheck_MissingRequiredColumn_ReturnsFalse()
        {
            // Arrange
            var file = CreateSampleExcelFile("Missing.xlsx", new[]
            {
                ("Amount", DataType.Number)
            });

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Required("MissingColumn", DataType.Text));

            // Act
            var result = _service.QuickStructureCheck(file, template);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ValidateBatchAsync Tests

        [Fact]
        public async Task ValidateBatchAsync_MultipleFiles_ReturnsReportForEach()
        {
            // Arrange
            var files = new[]
            {
                CreateSampleExcelFile("File1.xlsx", new[] { ("Amount", DataType.Number) }),
                CreateSampleExcelFile("File2.xlsx", new[] { ("Amount", DataType.Number) }),
                CreateSampleExcelFile("File3.xlsx", new[] { ("Amount", DataType.Number) })
            };

            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number));

            // Act
            var reports = await _service.ValidateBatchAsync(files, template);

            // Assert
            reports.Should().HaveCount(3);
            reports.All(r => r.Passed).Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        #region DataRegion Tests

        [Fact]
        public async Task CreateTemplateFromFileAsync_WithRegion_UsesRegionHeaderRowCount()
        {
            // Arrange: sheet has 1 header row globally, but region has 2 header rows
            var columns = new[] { "ID", "Name", "Value" };
            var sheet = new SASheetData("Sheet1", columns);
            for (int i = 0; i < 12; i++) // rows 0-11
                sheet.AddRow(columns.Select(_ => new SACellData(SACellValue.FromText($"R{i}"))).ToArray());

            var region = new DataRegion
            {
                Name = "MultiHeader",
                HeaderStartRow = 0,
                HeaderEndRow = 1,  // 2-row header
                DataStartRow = 2,
                DataEndRow = 11
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("/test/test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "T", "Sheet1", "MultiHeader");

            // Assert: template HeaderRowCount should be 2 (region's HeaderRowCount)
            template.HeaderRowCount.Should().Be(2);
        }

        [Fact]
        public async Task CreateTemplateFromFileAsync_WithRegionColumnBounds_LimitsColumnsToRegion()
        {
            // Arrange: 5 columns, region covers columns 1-3
            var columns = new[] { "Extra", "Name", "Age", "City", "Tail" };
            var sheet = new SASheetData("Sheet1", columns);
            for (int i = 0; i < 6; i++)
                sheet.AddRow(columns.Select(_ => new SACellData(SACellValue.FromText($"V{i}"))).ToArray());

            var region = new DataRegion
            {
                Name = "Core",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                StartColumn = 1,
                EndColumn = 3
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("/test/test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "T", "Sheet1", "Core");

            // Assert: only columns 1-3
            template.Columns.Should().HaveCount(3);
            template.Columns.Should().Contain(c => c.Name == "Name");
            template.Columns.Should().Contain(c => c.Name == "Age");
            template.Columns.Should().Contain(c => c.Name == "City");
            template.Columns.Should().NotContain(c => c.Name == "Extra");
            template.Columns.Should().NotContain(c => c.Name == "Tail");
        }

        [Fact]
        public async Task CreateTemplateFromFileAsync_WithNullRegion_UsesSheetHeaderRowCount()
        {
            // Arrange
            var file = CreateSampleExcelFile("Test.xlsx", new[] { ("Amount", DataType.Number) });

            // Act
            var template = await _service.CreateTemplateFromFileAsync(file, "T", regionName: null);

            // Assert: default sheet HeaderRowCount is 1
            template.HeaderRowCount.Should().Be(1);
        }

        [Fact]
        public async Task ValidateAsync_WithRegionColumnBounds_ValidatesOnlyRegionColumns()
        {
            // Arrange: 4 columns, region covers only columns 0-1
            var columns = new[] { "ID", "Name", "HiddenA", "HiddenB" };
            var sheet = new SASheetData("Sheet1", columns);
            sheet.AddRow(columns.Select(n => new SACellData(SACellValue.FromText(n))).ToArray());
            for (int i = 0; i < 5; i++)
                sheet.AddRow(new[]
                {
                    new SACellData(SACellValue.FromInteger(i + 1)),
                    new SACellData(SACellValue.FromText($"Name{i}")),
                    new SACellData(SACellValue.FromText("hidden")),
                    new SACellData(SACellValue.FromText("hidden"))
                });

            var region = new DataRegion
            {
                Name = "Visible",
                HeaderStartRow = 0,
                HeaderEndRow = 0,
                DataStartRow = 1,
                StartColumn = 0,
                EndColumn = 1
            };
            sheet.AddDataRegion(region);

            var file = new ExcelFile("/test/test.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());

            // Template matches only the region's columns
            var template = ExcelTemplate.Create("T")
                .AddColumn(ExpectedColumn.Required("ID", DataType.Number))
                .AddColumn(ExpectedColumn.Required("Name", DataType.Text));

            // Act
            var report = await _service.ValidateAsync(file, template, regionName: "Visible");

            // Assert: validation passes — HiddenA/HiddenB are outside the region and not checked
            report.Passed.Should().BeTrue();
        }

        #endregion

        private static ExcelFile CreateSampleExcelFile(string fileName, (string Name, DataType Type)[] columns)
        {
            var sheet = CreateSampleSheet("Sheet1", columns);
            return new ExcelFile(
                $"/test/{fileName}",
                LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Sheet1", sheet } },
                new List<ExcelError>());
        }

        private static SASheetData CreateSampleSheet(string sheetName, (string Name, DataType Type)[] columns)
        {
            var columnNames = columns.Select(c => c.Name).ToArray();
            var sheet = new SASheetData(sheetName, columnNames, 20);

            // Add header row
            var headerRow = columnNames.Select(n => new SACellData(SACellValue.FromText(n))).ToArray();
            sheet.AddRow(headerRow);

            // Add 10 sample data rows
            for (int i = 0; i < 10; i++)
            {
                var dataRow = columns.Select(c => CreateSampleCell(c.Type, i)).ToArray();
                sheet.AddRow(dataRow);
            }

            return sheet;
        }

        private static SACellData CreateSampleCell(DataType type, int rowIndex)
        {
            return type switch
            {
                DataType.Number => new SACellData(
                    SACellValue.FromFloatingPoint(100.0 * (rowIndex + 1)),
                    new CellMetadata { NumberFormat = "#,##0.00" }),
                DataType.Date => new SACellData(
                    SACellValue.FromInteger(45292 + rowIndex),
                    new CellMetadata { NumberFormat = "mm/dd/yyyy" }),
                DataType.Currency => new SACellData(
                    SACellValue.FromFloatingPoint(100.0 * (rowIndex + 1)),
                    new CellMetadata { NumberFormat = "[$€-407] #,##0.00" }),
                DataType.Text => new SACellData(SACellValue.FromText($"Text Row {rowIndex + 1}")),
                DataType.Boolean => new SACellData(SACellValue.FromBoolean(rowIndex % 2 == 0)),
                _ => new SACellData(SACellValue.Empty)
            };
        }

        #endregion
    }
}
