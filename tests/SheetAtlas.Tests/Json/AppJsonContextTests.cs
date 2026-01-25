using System.Text.Json;
using FluentAssertions;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Json;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;
using Xunit;

namespace SheetAtlas.Tests.Json
{
    /// <summary>
    /// Tests for AppJsonContext to ensure source-generated serialization works correctly.
    /// These tests are critical for PublishTrimmed=true builds.
    /// </summary>
    public class AppJsonContextTests
    {
        [Fact]
        public void UserSettings_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var settings = new UserSettings
            {
                Version = 1,
                Appearance = new AppearanceSettings { Theme = ThemePreference.Dark },
                DataProcessing = new DataProcessingSettings
                {
                    DefaultHeaderRowCount = 2,
                    DefaultExportFormat = ExportFormat.CSV,
                    NormalizedFileNaming = NamingPattern.DateSuffix
                },
                FileLocations = new FileLocationSettings
                {
                    OutputFolder = "/tmp/test"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.UserSettings);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserSettings);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Version.Should().Be(settings.Version);
            deserialized.Appearance.Theme.Should().Be(ThemePreference.Dark);
            deserialized.DataProcessing.DefaultHeaderRowCount.Should().Be(2);
            deserialized.DataProcessing.DefaultExportFormat.Should().Be(ExportFormat.CSV);
            deserialized.DataProcessing.NormalizedFileNaming.Should().Be(NamingPattern.DateSuffix);
            deserialized.FileLocations.OutputFolder.Should().Be("/tmp/test");
        }

        [Fact]
        public void ExcelTemplate_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var template = ExcelTemplate.Create("TestTemplate")
                .AddColumn(ExpectedColumn.Required("Name", DataType.Text))
                .AddColumn(ExpectedColumn.Optional("Age", DataType.Number))
                .AddGlobalRule(ValidationRule.NotEmpty())
                .WithHeaders(2);

            // Act
            var json = JsonSerializer.Serialize(template, AppJsonContext.Default.ExcelTemplate);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ExcelTemplate);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be("TestTemplate");
            deserialized.HeaderRowCount.Should().Be(2);
            deserialized.Columns.Should().HaveCount(2);
            deserialized.Columns[0].Name.Should().Be("Name");
            deserialized.Columns[0].ExpectedType.Should().Be(DataType.Text);
            deserialized.Columns[0].IsRequired.Should().BeTrue();
            deserialized.Columns[1].Name.Should().Be("Age");
            deserialized.Columns[1].ExpectedType.Should().Be(DataType.Number);
            deserialized.Columns[1].IsRequired.Should().BeFalse();
            deserialized.GlobalRules.Should().HaveCount(1);
            deserialized.GlobalRules[0].Type.Should().Be(RuleType.NotEmpty);
        }

        [Fact]
        public void ExpectedColumn_WithAllProperties_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var column = ExpectedColumn.RequiredAt("Price", DataType.Currency, 5)
                .WithCurrency("EUR")
                .WithSemanticName("ProductPrice")
                .WithAlternatives("Cost", "Amount")
                .WithMinConfidence(0.9)
                .WithRules(ValidationRule.Positive(), ValidationRule.NotEmpty());

            var columns = new List<ExpectedColumn> { column };

            // Act
            var json = JsonSerializer.Serialize(columns, AppJsonContext.Default.ListExpectedColumn);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListExpectedColumn);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Should().HaveCount(1);
            var deserializedColumn = deserialized[0];
            deserializedColumn.Name.Should().Be("Price");
            deserializedColumn.ExpectedType.Should().Be(DataType.Currency);
            deserializedColumn.Position.Should().Be(5);
            deserializedColumn.IsRequired.Should().BeTrue();
            deserializedColumn.ExpectedCurrency.Should().Be("EUR");
            deserializedColumn.SemanticName.Should().Be("ProductPrice");
            deserializedColumn.AlternativeNames.Should().BeEquivalentTo(new[] { "Cost", "Amount" });
            deserializedColumn.MinTypeConfidence.Should().Be(0.9);
            deserializedColumn.Rules.Should().HaveCount(2);
        }

        [Fact]
        public void ValidationRule_AllTypes_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var rules = new List<ValidationRule>
            {
                ValidationRule.NotEmpty(),
                ValidationRule.Required(),
                ValidationRule.Unique(ValidationSeverity.Warning),
                ValidationRule.Positive(),
                ValidationRule.InRange(0, 100, ValidationSeverity.Error),
                ValidationRule.Pattern(@"^\d{3}-\d{3}$", "Invalid format"),
                ValidationRule.MinLength(5),
                ValidationRule.MaxLength(50),
                ValidationRule.InList(new[] { "A", "B", "C" }),
                ValidationRule.DateFormat("yyyy-MM-dd"),
                ValidationRule.Currency("USD")
            };

            // Act
            var json = JsonSerializer.Serialize(rules, AppJsonContext.Default.ListValidationRule);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListValidationRule);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Should().HaveCount(11);
            deserialized[0].Type.Should().Be(RuleType.NotEmpty);
            deserialized[1].Type.Should().Be(RuleType.Required);
            deserialized[2].Type.Should().Be(RuleType.Unique);
            deserialized[2].Severity.Should().Be(ValidationSeverity.Warning);
            deserialized[3].Type.Should().Be(RuleType.Positive);
            deserialized[4].Type.Should().Be(RuleType.InRange);
            deserialized[4].Parameter.Should().Be("0|100");
            deserialized[5].Type.Should().Be(RuleType.Pattern);
            deserialized[5].Parameter.Should().Be(@"^\d{3}-\d{3}$");
            deserialized[5].ErrorMessage.Should().Be("Invalid format");
        }

        [Theory]
        [InlineData(ThemePreference.Light, "\"Light\"")]
        [InlineData(ThemePreference.Dark, "\"Dark\"")]
        [InlineData(ThemePreference.System, "\"System\"")]
        public void ThemePreference_SerializesToString(ThemePreference theme, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(theme, AppJsonContext.Default.ThemePreference);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Theory]
        [InlineData(ExportFormat.Excel, "\"Excel\"")]
        [InlineData(ExportFormat.CSV, "\"CSV\"")]
        public void ExportFormat_SerializesToString(ExportFormat format, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(format, AppJsonContext.Default.ExportFormat);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Theory]
        [InlineData(NamingPattern.DatePrefix, "\"DatePrefix\"")]
        [InlineData(NamingPattern.DateSuffix, "\"DateSuffix\"")]
        [InlineData(NamingPattern.DateTimePrefix, "\"DateTimePrefix\"")]
        public void NamingPattern_SerializesToString(NamingPattern pattern, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(pattern, AppJsonContext.Default.NamingPattern);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Theory]
        [InlineData(DataType.Unknown, "\"Unknown\"")]
        [InlineData(DataType.Number, "\"Number\"")]
        [InlineData(DataType.Date, "\"Date\"")]
        [InlineData(DataType.Currency, "\"Currency\"")]
        [InlineData(DataType.Percentage, "\"Percentage\"")]
        [InlineData(DataType.Text, "\"Text\"")]
        [InlineData(DataType.Boolean, "\"Boolean\"")]
        [InlineData(DataType.Error, "\"Error\"")]
        public void DataType_SerializesToString(DataType dataType, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(dataType, AppJsonContext.Default.DataType);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Theory]
        [InlineData(RuleType.NotEmpty, "\"NotEmpty\"")]
        [InlineData(RuleType.Required, "\"Required\"")]
        [InlineData(RuleType.Unique, "\"Unique\"")]
        [InlineData(RuleType.TypeMatch, "\"TypeMatch\"")]
        [InlineData(RuleType.Positive, "\"Positive\"")]
        public void RuleType_SerializesToString(RuleType ruleType, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(ruleType, AppJsonContext.Default.RuleType);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Theory]
        [InlineData(ValidationSeverity.Info, "\"Info\"")]
        [InlineData(ValidationSeverity.Warning, "\"Warning\"")]
        [InlineData(ValidationSeverity.Error, "\"Error\"")]
        [InlineData(ValidationSeverity.Critical, "\"Critical\"")]
        public void ValidationSeverity_SerializesToString(ValidationSeverity severity, string expectedJson)
        {
            // Act
            var json = JsonSerializer.Serialize(severity, AppJsonContext.Default.ValidationSeverity);

            // Assert
            json.Should().Be(expectedJson);
        }

        [Fact]
        public void FileInfoDto_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var fileInfo = new FileInfoDto
            {
                Name = "test.xlsx",
                OriginalPath = "/tmp/test.xlsx",
                SizeBytes = 1024,
                Hash = "abc123",
                LastModified = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            };

            // Act
            var json = JsonSerializer.Serialize(fileInfo, AppJsonContext.Default.FileInfoDto);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.FileInfoDto);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Name.Should().Be("test.xlsx");
            deserialized.OriginalPath.Should().Be("/tmp/test.xlsx");
            deserialized.SizeBytes.Should().Be(1024);
            deserialized.Hash.Should().Be("abc123");
        }

        [Fact]
        public void LoadAttemptInfo_SerializeAndDeserialize_ShouldRoundTrip()
        {
            // Arrange
            var loadAttempt = new LoadAttemptInfo
            {
                Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Status = "Success",
                DurationMs = 150,
                AppVersion = "0.5.1"
            };

            // Act
            var json = JsonSerializer.Serialize(loadAttempt, AppJsonContext.Default.LoadAttemptInfo);
            var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.LoadAttemptInfo);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Status.Should().Be("Success");
            deserialized.DurationMs.Should().Be(150);
            deserialized.AppVersion.Should().Be("0.5.1");
        }

        [Fact]
        public void Context_ShouldUseWriteIndentedTrue()
        {
            // Arrange
            var settings = UserSettings.CreateDefault();

            // Act
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.UserSettings);

            // Assert
            json.Should().Contain("\n"); // Should be indented with newlines
        }

        [Fact]
        public void Context_ShouldUseCamelCaseNaming()
        {
            // Arrange
            var settings = new UserSettings
            {
                Appearance = new AppearanceSettings { Theme = ThemePreference.Dark }
            };

            // Act
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.UserSettings);

            // Assert
            json.Should().Contain("\"appearance\""); // Not "Appearance"
            json.Should().Contain("\"theme\""); // Not "Theme"
        }

        [Fact]
        public void Context_ShouldIgnoreNullValues()
        {
            // Arrange
            var template = ExcelTemplate.Create("Test");
            template.Description = null;
            template.ExpectedSheetName = null;

            // Act
            var json = JsonSerializer.Serialize(template, AppJsonContext.Default.ExcelTemplate);

            // Assert
            json.Should().NotContain("\"description\"");
            json.Should().NotContain("\"expectedSheetName\"");
        }
    }
}
