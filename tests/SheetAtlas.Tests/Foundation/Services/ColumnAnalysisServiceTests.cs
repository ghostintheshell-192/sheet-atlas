using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;
using FluentAssertions;
using SheetAtlas.Tests.Foundation.Builders;
using SheetAtlas.Tests.Foundation.Fixtures;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for IColumnAnalysisService.
    /// Tests column type detection, header detection, and metadata generation.
    /// </summary>
    public class ColumnAnalysisServiceTests
    {
        private readonly IColumnAnalysisService _service = new ColumnAnalysisService();

        #region Column Type Detection Tests

        [Fact]
        public void AnalyzeColumn_UniformNumericData_HighConfidence()
        {
            // Arrange
            var columnIndex = 0;
            var columnName = "Amount";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromInteger(200),
                SACellValue.FromInteger(300),
                SACellValue.FromInteger(400),
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.TypeConfidence.Should().BeGreaterThan(0.8);
            result.ColumnName.Should().Be(columnName);
        }

        [Fact]
        public void AnalyzeColumn_UniformDateData_HighConfidence()
        {
            // Arrange
            var columnIndex = 1;
            var columnName = "Date";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(45292), // Excel serial dates
                SACellValue.FromInteger(45293),
                SACellValue.FromInteger(45294),
                SACellValue.FromInteger(45295),
                SACellValue.FromInteger(45296)
            };
            var numberFormats = Enumerable.Repeat("mm/dd/yyyy", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Date);
            result.TypeConfidence.Should().BeGreaterThan(0.8);
        }

        [Fact]
        public void AnalyzeColumn_CurrencyColumn_PopulatesCurrencyInfo()
        {
            // Arrange
            var columnIndex = 2;
            var columnName = "Revenue";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromNumber((double)1000m),
                SACellValue.FromNumber((double)2000m),
                SACellValue.FromNumber((double)3000m),
                SACellValue.FromNumber((double)4000m),
                SACellValue.FromNumber((double)5000m)
            };
            var numberFormats = Enumerable.Repeat("[$€-407] #,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Currency);
            result.Currency.Should().NotBeNull();
            result.Currency!.Code.Should().Be("EUR");
        }

        [Fact]
        public void AnalyzeColumn_MixedTypes_LowConfidence()
        {
            // Arrange
            var columnIndex = 3;
            var columnName = "Mixed";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromText("text"),
                SACellValue.FromBoolean(true),
                SACellValue.FromInteger(45292),
                SACellValue.FromText("2024-11-05")
            };
            var numberFormats = Enumerable.Repeat("General", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.TypeConfidence.Should().BeLessThan(0.8);
        }

        [Fact]
        public void AnalyzeColumn_TextColumn_CorrectDetection()
        {
            // Arrange
            var columnIndex = 4;
            var columnName = "Name";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromText("John"),
                SACellValue.FromText("Jane"),
                SACellValue.FromText("Bob"),
                SACellValue.FromText("Alice"),
                SACellValue.FromText("Charlie")
            };
            var numberFormats = Enumerable.Repeat("@", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Text);
            result.TypeConfidence.Should().BeGreaterThan(0.8);
        }

        [Fact]
        public void AnalyzeColumn_BooleanColumn_CorrectDetection()
        {
            // Arrange
            var columnIndex = 5;
            var columnName = "Active";
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromBoolean(true),
                SACellValue.FromBoolean(false),
                SACellValue.FromBoolean(true),
                SACellValue.FromBoolean(true),
                SACellValue.FromBoolean(false)
            };
            var numberFormats = Enumerable.Repeat("General", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(columnIndex, columnName, sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Boolean);
            result.TypeConfidence.Should().BeGreaterThan(0.8);
        }

        #endregion

        #region Type Distribution Tests

        [Fact]
        public void AnalyzeColumn_ReturnsTypeDistribution()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromInteger(200),
                SACellValue.FromInteger(300),
                SACellValue.FromInteger(400),
                SACellValue.FromText("text") // One text value in numeric column
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Mixed", sampleCells, numberFormats);

            // Assert
            result.TypeDistribution.Should().NotBeEmpty();
            result.TypeDistribution.Should().ContainKey(DataType.Number);
            result.TypeDistribution[DataType.Number].Should().Be(4);
        }

        #endregion

        #region Sample Values Tests

        [Fact]
        public void AnalyzeColumn_IncludesSampleValues()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromInteger(200),
                SACellValue.FromInteger(300)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 3).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Data", sampleCells, numberFormats);

            // Assert
            result.SampleValues.Should().NotBeEmpty();
            result.SampleValues.Should().HaveCountLessThanOrEqualTo(sampleCells.Count);
        }

        #endregion

        #region Quality Warning Tests

        [Fact]
        public void AnalyzeColumn_MixedFormats_ReturnsWarningCount()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromNumber((double)1000m),
                SACellValue.FromNumber((double)2000m),
                SACellValue.FromNumber((double)3000m),
                SACellValue.FromNumber((double)4000m),
                SACellValue.FromNumber((double)5000m)
            };
            var numberFormats = new List<string>
            {
                "[$€-407] #,##0.00",       // EUR
                "[$$-409] #,##0.00",       // USD
                "[$£-809] #,##0.00",       // GBP
                "[$€-407] #,##0.00",       // EUR
                "[$$-409] #,##0.00"        // USD
            };

            // Act
            var result = _service.AnalyzeColumn(0, "Currency", sampleCells, numberFormats);

            // Assert
            result.WarningCount.Should().BeGreaterThan(0);
            result.Anomalies.Should().NotBeEmpty("mixed currency formats should be flagged");
            result.Anomalies.Should().Contain(a => a.Issue == DataQualityIssue.InconsistentFormat);
        }

        [Fact]
        public void AnalyzeColumn_MixedCurrencies_AnomaliesHaveDetails()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromNumber(1000),
                SACellValue.FromNumber(2000),
                SACellValue.FromNumber(3000)
            };
            var numberFormats = new List<string>
            {
                "[$€-407] #,##0.00",       // EUR
                "[$$-409] #,##0.00",       // USD
                "[$£-809] #,##0.00"        // GBP
            };

            // Act
            var result = _service.AnalyzeColumn(0, "Currency", sampleCells, numberFormats);

            // Assert
            result.Anomalies.Should().NotBeEmpty();
            result.Anomalies.Should().AllSatisfy(anomaly =>
            {
                anomaly.RowIndex.Should().BeGreaterThanOrEqualTo(0);
                anomaly.Message.Should().NotBeNullOrEmpty();
                anomaly.Severity.Should().BeOneOf(LogSeverity.Warning, LogSeverity.Error);
            });
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void AnalyzeColumn_EmptySample_HandlesGracefully()
        {
            // Arrange
            var sampleCells = new List<SACellValue>();
            var numberFormats = new List<string>();

            // Act
            var result = _service.AnalyzeColumn(0, "Empty", sampleCells, numberFormats);

            // Assert
            result.Should().NotBeNull();
            result.DetectedType.Should().Be(DataType.Unknown);
        }

        [Fact]
        public void AnalyzeColumn_SingleCell_AnalyzesCorrectly()
        {
            // Arrange
            var sampleCells = new List<SACellValue> { SACellValue.FromInteger(1000) };
            var numberFormats = new List<string> { "#,##0.00" };

            // Act
            var result = _service.AnalyzeColumn(0, "Single", sampleCells, numberFormats);

            // Assert
            result.Should().NotBeNull();
            result.DetectedType.Should().NotBe(DataType.Unknown);
        }

        [Fact]
        public void AnalyzeColumn_AllNullValues_ReturnsUnknown()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.Empty,
                SACellValue.Empty,
                SACellValue.Empty
            };
            var numberFormats = new List<string> { "General", "General", "General" };

            // Act
            var result = _service.AnalyzeColumn(0, "AllNull", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Unknown);
            result.TypeConfidence.Should().BeLessThan(0.5);
        }

        #endregion

        #region Mixed Date Format Detection

        [Fact]
        public void AnalyzeColumn_MixedDateFormats_DetectsDateType()
        {
            // Arrange - Test various date format patterns recognition
            // Excel stores dates as serial numbers; numberFormat determines display/type
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(45292),  // Nov 5, 2024 (serial date)
                SACellValue.FromInteger(45293),  // Nov 6, 2024
                SACellValue.FromInteger(45294),  // Nov 7, 2024
                SACellValue.FromInteger(45295),  // Nov 8, 2024
                SACellValue.FromInteger(45296)   // Nov 9, 2024
            };

            // Different date format patterns - tests IsDateFormat() recognition
            var numberFormats = new List<string?>
            {
                "yyyy-mm-dd",    // ISO format (displays: 2024-11-05)
                "mm/dd/yyyy",    // US format (displays: 11/05/2024)
                "dd-mm-yyyy",    // EU format (displays: 05-11-2024)
                "mmmm dd, yyyy", // Long format (displays: November 05, 2024)
                "m/d/yy"         // Short format (displays: 11/5/24)
            };

            // Act
            var result = _service.AnalyzeColumn(0, "Date", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Date);
            result.TypeConfidence.Should().BeGreaterThan(0.8, "uniform serial dates with date formats");
        }

        #endregion

        #region Confidence Score Tests

        [Fact]
        public void AnalyzeColumn_StrongUniformData_HighConfidence()
        {
            // Arrange
            var uniformData = Enumerable.Range(0, 100)
                .Select(i => SACellValue.FromInteger(i * 10))
                .ToList();
            var numberFormats = Enumerable.Repeat("#,##0.00", 100).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Numbers", uniformData, numberFormats);

            // Assert
            result.TypeConfidence.Should().BeGreaterThan(0.9);
        }

        [Fact]
        public void AnalyzeColumn_WeakMixedData_LowConfidence()
        {
            // Arrange
            var mixedData = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromText("text"),
                SACellValue.FromBoolean(true),
                SACellValue.FromInteger(45292),
                SACellValue.FromNumber(3.14)
            };
            var numberFormats = new List<string>
            {
                "#,##0.00",
                "@",
                "General",
                "mm/dd/yyyy",
                "0.00"
            };

            // Act
            var result = _service.AnalyzeColumn(0, "Mixed", mixedData, numberFormats);

            // Assert
            result.TypeConfidence.Should().BeLessThan(0.7);
        }

        #endregion

        #region Context-Aware Detection Tests

        [Fact]
        public void AnalyzeColumn_IsolatedAnomaly_DetectedWithLocalContext()
        {
            // Arrange - Single text value in numeric column
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromInteger(200),
                SACellValue.FromText("invalid"),  // Row 2: anomaly
                SACellValue.FromInteger(400),
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Amount", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number, "dominant type should be Number");
            result.Anomalies.Should().ContainSingle(a => a.RowIndex == 2, "row 2 should be flagged");
            var anomaly = result.Anomalies.First(a => a.RowIndex == 2);
            anomaly.Issue.Should().Be(DataQualityIssue.TypeMismatch);
            anomaly.ExpectedType.Should().Be(DataType.Number);
            anomaly.ActualType.Should().Be(DataType.Text);
            anomaly.Severity.Should().Be(LogSeverity.Error);
        }

        [Fact]
        public void AnalyzeColumn_MultipleAnomaliesWithContext_AllDetected()
        {
            // Arrange - Multiple scattered anomalies
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromText("error1"),    // Row 1
                SACellValue.FromInteger(200),
                SACellValue.FromInteger(300),
                SACellValue.FromText("error2"),    // Row 4
                SACellValue.FromInteger(400),
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 7).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Values", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.Anomalies.Should().HaveCount(2, "two anomalies should be detected");
            result.Anomalies.Should().Contain(a => a.RowIndex == 1);
            result.Anomalies.Should().Contain(a => a.RowIndex == 4);
            result.TypeConfidence.Should().BeLessThan(0.8, "confidence should be reduced");
        }

        [Fact]
        public void AnalyzeColumn_FooterSection_DetectedAsLocalChange()
        {
            // Arrange - Numeric column with text footer
            var sampleCells = new List<SACellValue>();
            // Rows 0-7: numbers (dominant type)
            for (int i = 0; i < 8; i++)
                sampleCells.Add(SACellValue.FromInteger(i * 100));

            // Rows 8-9: text footer (local type change)
            sampleCells.Add(SACellValue.FromText("Total:"));
            sampleCells.Add(SACellValue.FromText("Summary"));

            var numberFormats = Enumerable.Repeat("#,##0.00", 10).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Amount", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number, "dominant type is Number");
            result.Anomalies.Should().Contain(a => a.RowIndex == 8, "footer row 8 flagged");
            result.Anomalies.Should().Contain(a => a.RowIndex == 9, "footer row 9 flagged");
            // Context: local type at rows 8-9 is Text, so severity might be Warning (not Error)
            result.Anomalies.Where(a => a.RowIndex >= 8).Should().AllSatisfy(a =>
                a.Issue.Should().Be(DataQualityIssue.TypeMismatch));
        }

        [Fact]
        public void AnalyzeColumn_FormulaErrorInNumericColumn_Warning()
        {
            // Arrange - Excel formula error in numeric context
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromInteger(200),
                SACellValue.FromText("#N/A"),  // Formula error
                SACellValue.FromInteger(400),
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Calculated", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.Anomalies.Should().ContainSingle(a => a.CellValue.AsText().Contains('#'));
            var anomaly = result.Anomalies.First();
            anomaly.Severity.Should().Be(LogSeverity.Warning, "formula errors are warnings, not errors");
            anomaly.Message.ToLowerInvariant().Should().Contain("formula error");
        }

        [Fact]
        public void AnalyzeColumn_EmptyCellsInNumericColumn_Warning()
        {
            // Arrange - Empty cells scattered in numeric column
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.Empty,              // Row 1: empty
                SACellValue.FromInteger(300),
                SACellValue.Empty,              // Row 3: empty
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Amount", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.Anomalies.Where(a => a.CellValue.IsEmpty).Should().HaveCount(2);
            result.Anomalies.Where(a => a.CellValue.IsEmpty).Should().AllSatisfy(a =>
            {
                a.Issue.Should().Be(DataQualityIssue.MissingRequired);
                a.Severity.Should().Be(LogSeverity.Warning);
            });
        }

        #endregion

        #region Percentage Type Tests

        [Fact]
        public void AnalyzeColumn_PercentageColumn_CorrectDetection()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromNumber(0.10),   // 10%
                SACellValue.FromNumber(0.25),   // 25%
                SACellValue.FromNumber(0.50),   // 50%
                SACellValue.FromNumber(0.75),   // 75%
                SACellValue.FromNumber(0.95)    // 95%
            };
            var numberFormats = Enumerable.Repeat("0.00%", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Rate", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Percentage);
            result.TypeConfidence.Should().BeGreaterThan(0.8);
            result.Anomalies.Should().BeEmpty("uniform percentages have no anomalies");
        }

        #endregion

        #region Formula Error Detection Tests

        [Fact]
        public void AnalyzeColumn_VariousFormulaErrors_AllWarnings()
        {
            // Arrange - Different Excel formula errors
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromText("#N/A"),   // Not available
                SACellValue.FromText("#REF!"),  // Invalid reference
                SACellValue.FromText("#DIV/0!"),// Division by zero
                SACellValue.FromInteger(500)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 5).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Calculated", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            var formulaErrors = result.Anomalies.Where(a => a.CellValue.AsText().StartsWith("#")).ToList();
            formulaErrors.Should().HaveCount(3, "three formula errors present");
            formulaErrors.Should().AllSatisfy(a =>
            {
                a.Severity.Should().Be(LogSeverity.Warning, "formula errors are warnings");
                a.ActualType.Should().Be(DataType.Error);
            });
        }

        [Fact]
        public void AnalyzeColumn_FormulaErrorTypes_DistinguishedInMessage()
        {
            // Arrange
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromText("#N/A"),
                SACellValue.FromText("#REF!"),
                SACellValue.FromText("#VALUE!")
            };
            var numberFormats = Enumerable.Repeat("General", 3).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Errors", sampleCells, numberFormats);

            // Assert
            result.Anomalies.Should().HaveCount(3);
            result.Anomalies.Should().Contain(a => a.Message.Contains("#N/A"));
            result.Anomalies.Should().Contain(a => a.Message.Contains("#REF!"));
            result.Anomalies.Should().Contain(a => a.Message.Contains("#VALUE!"));
        }

        #endregion

        #region Confidence Penalty Tests

        [Fact]
        public void AnalyzeColumn_SingleError_ModeratePenalty()
        {
            // Arrange - 1 error in 10 cells = 10% error rate
            var sampleCells = new List<SACellValue>();
            for (int i = 0; i < 9; i++)
                sampleCells.Add(SACellValue.FromInteger(i * 100));
            sampleCells.Add(SACellValue.FromText("invalid"));

            var numberFormats = Enumerable.Repeat("#,##0.00", 10).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Data", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.TypeConfidence.Should().BeGreaterThan(0.75, "90% clean data");
            result.TypeConfidence.Should().BeLessThan(0.95, "penalty applied for error");
        }

        [Fact]
        public void AnalyzeColumn_MultipleErrors_StrongerPenalty()
        {
            // Arrange - 3 errors in 10 cells = 30% error rate
            var sampleCells = new List<SACellValue>
            {
                SACellValue.FromInteger(100),
                SACellValue.FromText("error1"),
                SACellValue.FromInteger(300),
                SACellValue.FromText("error2"),
                SACellValue.FromInteger(500),
                SACellValue.FromInteger(600),
                SACellValue.FromText("error3"),
                SACellValue.FromInteger(800),
                SACellValue.FromInteger(900),
                SACellValue.FromInteger(1000)
            };
            var numberFormats = Enumerable.Repeat("#,##0.00", 10).ToList();

            // Act
            var result = _service.AnalyzeColumn(0, "Data", sampleCells, numberFormats);

            // Assert
            result.DetectedType.Should().Be(DataType.Number);
            result.TypeConfidence.Should().BeLessThan(0.75, "significant error rate lowers confidence");
            result.Anomalies.Should().HaveCount(3);
        }

        #endregion
    }
}
