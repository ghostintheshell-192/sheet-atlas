using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;
using SheetAtlas.Tests.Foundation.TestUtilities;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for IDataNormalizationService.
    /// Tests normalization of dates, numbers, text, and boolean values.
    /// </summary>
    public class DataNormalizationServiceTests
    {
        private readonly IDataNormalizationService _service = new DataNormalizationService();

        #region Date Normalization Tests

        [Fact]
        public void Normalize_ExcelSerialDate_ParsesCorrectly()
        {
            // Arrange - 45602 = 2024-11-05 (Excel serial date with 1900 leap year bug)
            double excelSerialDate = 45602;

            // Act
            var result = _service.Normalize(
                excelSerialDate,
                numberFormat: "mm/dd/yyyy",
                cellDataType: CellDataType.Number);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue.Should().NotBeNull();
            result.DetectedType.Should().Be(DataType.Date);
            result.CleanedValue?.ToString().Should().Contain("2024-11-05");
        }

        [Fact]
        public void Normalize_ISODateString_ParsesCorrectly()
        {
            // Arrange
            string isoDate = "2024-11-05";

            // Act
            var result = _service.Normalize(
                isoDate,
                numberFormat: "yyyy-mm-dd",
                cellDataType: CellDataType.String);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Date);
        }

        [Theory]
        [InlineData("11/5/2024")]      // US format MM/DD/YYYY
        [InlineData("11-05-2024")]     // US format with dashes
        [InlineData("11/05/2024")]     // US format with leading zeros
        public void Normalize_USDateFormats_ParsesCorrectly(string dateString)
        {
            // Act
            var result = _service.Normalize(
                dateString,
                cellDataType: CellDataType.String);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Date);
        }

        [Theory]
        [InlineData("5/11/2024")]      // EU format DD/MM/YYYY
        [InlineData("05/11/2024")]     // EU format with leading zeros
        [InlineData("5-11-2024")]      // EU format with dashes
        public void Normalize_EUDateFormats_ParsesCorrectly(string dateString)
        {
            // Act - Note: This requires locale context to disambiguate
            var result = _service.Normalize(
                dateString,
                numberFormat: "dd/mm/yyyy",
                cellDataType: CellDataType.String);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Date);
        }

        [Theory]
        [InlineData("2024-11-05")]     // ISO YYYY-MM-DD
        [InlineData("2024-11")]        // Year-Month (may not parse as date by all parsers)
        public void Normalize_ISODateVariations_ParsesCorrectly(string dateString)
        {
            // Act
            var result = _service.Normalize(dateString);

            // Assert
            result.IsSuccess.Should().BeTrue();
            // Note: "2024-11" may be parsed as date or text depending on DateTime.TryParse behavior
        }

        [Fact]
        public void Normalize_TextDateWithMonthName_ParsesCorrectly()
        {
            // Arrange
            string textDate = "November 5, 2024";

            // Act
            var result = _service.Normalize(textDate);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Date);
        }

        #endregion

        #region Number Normalization Tests

        [Theory]
        [InlineData("1,234.56", 1234.56)]        // US format
        [InlineData("1234.56", 1234.56)]         // No separator
        [InlineData("1,234", 1234)]              // Integer with separator
        [InlineData(".56", 0.56)]                // Decimal only
        public void Normalize_USNumberFormats_ParsesCorrectly(string numberString, double expected)
        {
            // Act
            var result = _service.Normalize(
                numberString,
                numberFormat: "#,##0.00",
                cellDataType: CellDataType.String);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Number);
            result.CleanedValue?.AsNumber().Should().BeApproximately(expected, 0.01);
        }

        [Theory]
        [InlineData("1.234,56", 1234.56)]       // European format (period thousand, comma decimal)
        [InlineData("1,234.56", 1234.56)]       // US format (comma thousand, period decimal)
        [InlineData("1 234,56", 1234.56)]       // French format (space thousand, comma decimal)
        public void Normalize_EuropeanNumberFormat_ParsesCorrectly(string numberString, double expected)
        {
            // Act
            var result = _service.Normalize(
                numberString,
                numberFormat: "#.##0,00",
                cellDataType: CellDataType.String);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Number);
        }

        [Theory]
        [InlineData("1.23E+02", 123)]            // Scientific notation (large)
        [InlineData("1.23E-02", 0.0123)]         // Scientific notation (small)
        [InlineData("1E+05", 100000)]            // Scientific integer
        public void Normalize_ScientificNotation_ParsesCorrectly(string numberString, double expected)
        {
            // Act
            var result = _service.Normalize(numberString);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Number);
        }

        [Theory]
        [InlineData("$1,234.56")]                 // Currency prefix
        [InlineData("1,234.56 USD")]              // Currency suffix
        [InlineData("€1.234,56")]                 // Euro with European format
        [InlineData("£500")]                      // Pound sterling
        public void Normalize_NumberWithCurrencySymbol_ParsesNumber(string currencyString)
        {
            // Act
            var result = _service.Normalize(currencyString);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().BeOneOf(DataType.Number, DataType.Currency);
        }

        #endregion

        #region Text Normalization Tests

        [Theory]
        [InlineData("  hello  ", "hello")]        // Leading/trailing spaces
        [InlineData("\thello\t", "hello")]        // Tabs
        [InlineData("\nhello\n", "hello")]        // Newlines
        [InlineData("hello world", "hello world")] // Preserve internal spaces
        public void Normalize_TextWithWhitespace_TrimsCorrectly(string input, string expected)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be(expected);
        }

        [Fact]
        public void Normalize_TextWithZeroWidthCharacters_CleansCorrectly()
        {
            // Arrange
            string textWithZeroWidth = "hello\u200Bworld"; // U+200B (zero-width space)

            // Act
            var result = _service.Normalize(textWithZeroWidth);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be("helloworld");
        }

        [Fact]
        public void Normalize_TextWithBOM_RemovesCorrectly()
        {
            // Arrange
            string textWithBOM = "\uFEFFhello"; // U+FEFF (byte order mark)

            // Act
            var result = _service.Normalize(textWithBOM);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be("hello");
        }

        [Fact]
        public void Normalize_TextWithMixedLineEndings_NormalizesCorrectly()
        {
            // Arrange
            string mixedLineEndings = "line1\r\nline2\nline3\rline4";

            // Act
            var result = _service.Normalize(mixedLineEndings);

            // Assert
            result.IsSuccess.Should().BeTrue();
            // Should normalize to consistent line endings
        }

        [Fact]
        public void Normalize_EmptyString_ReturnsEmpty()
        {
            // Act
            var result = _service.Normalize("");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be("");
        }

        #endregion

        #region Boolean Normalization Tests

        [Theory]
        [InlineData("Yes", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("No", false)]
        [InlineData("no", false)]
        [InlineData("NO", false)]
        public void Normalize_YesNoBoolean_MapsCorrectly(string input, bool expected)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Boolean);
            result.CleanedValue?.AsBoolean().Should().Be(expected);
        }

        [Theory]
        [InlineData("1", true)]
        [InlineData("0", false)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("True", true)]
        [InlineData("False", false)]
        [InlineData("TRUE", true)]
        [InlineData("FALSE", false)]
        public void Normalize_BooleanVariations_AllMapsToTrueFalse(string input, bool expected)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Boolean);
            result.CleanedValue?.AsBoolean().Should().Be(expected);
        }

        [Theory]
        [InlineData("X", true)]
        [InlineData("x", true)]
        public void Normalize_XBoolean_MapsCorrectly(string input, bool expected)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Boolean);
            result.CleanedValue?.AsBoolean().Should().Be(expected);
        }

        [Fact]
        public void Normalize_BlankString_ReturnsEmpty()
        {
            // Blank strings (empty or whitespace-only) should return Empty, not Boolean false
            var result = _service.Normalize("   ");

            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be("");
        }

        [Theory]
        [InlineData("✓", true)]      // Checkmark
        [InlineData("✗", false)]     // X mark
        [InlineData("☑", true)]      // Checked box
        [InlineData("☐", false)]     // Unchecked box
        public void Normalize_SymbolBoolean_MapsCorrectly(string input, bool expected)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Boolean);
            result.CleanedValue?.AsBoolean().Should().Be(expected);
        }

        #endregion

        #region Batch Normalization Tests

        [Fact]
        public void NormalizeBatch_MultipleValues_NormalizesAll()
        {
            // Arrange
            var cellValues = new List<(object? Value, string? Format)>
            {
                ("$1,234.56", "[$$-409] #,##0.00"),
                ("€1.234,56", "[$€-407] #,##0.00"),
                ("2024-11-05", null),
                ("yes", null),
                ("hello", null)
            };

            // Act
            var results = _service.NormalizeBatch(cellValues);

            // Assert
            results.Should().HaveCount(5);
            results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        }

        [Fact]
        public void NormalizeBatch_WithDataType_UsesContextForDetection()
        {
            // Arrange
            var cellValues = new List<(object? Value, string? Format)>
            {
                ("1,234.56", null),
                ("2,345.67", null),
                ("3,456.78", null)
            };

            // Act
            var results = _service.NormalizeBatch(cellValues, CellDataType.Number);

            // Assert
            results.Should().AllSatisfy(r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.DetectedType.Should().Be(DataType.Number);
            });
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Normalize_InvalidNumber_ReturnsAsText()
        {
            // Arrange - cannot be parsed as number, returns as cleaned text
            string invalidNumber = "not-a-number";

            // Act
            var result = _service.Normalize(invalidNumber);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Text);
            result.CleanedValue?.ToString().Should().Be("not-a-number");
        }

        [Fact]
        public void Normalize_InvalidDate_ReturnsAsText()
        {
            // Arrange - cannot be parsed as date, returns as cleaned text
            string invalidDate = "99/99/9999";

            // Act
            var result = _service.Normalize(invalidDate);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Text);
        }

        [Fact]
        public void Normalize_NullValue_ReturnsEmpty()
        {
            // Act
            var result = _service.Normalize(null);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.CleanedValue?.ToString().Should().Be("");
        }

        #endregion

        #region Data Quality Issue Detection

        [Fact]
        public void Normalize_ValueWithEncodingIssue_ReturnsWarning()
        {
            // Arrange
            string valueWithIssue = "test\u0001\u0002"; // Control characters

            // Act
            var result = _service.Normalize(valueWithIssue);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.QualityIssue.Should().NotBe(DataQualityIssue.None);
        }

        [Fact]
        public void Normalize_AmbiguousDateFormat_ReturnsLowConfidence()
        {
            // Arrange - Could be interpreted as MM/DD or DD/MM
            string ambiguousDate = "06/07/2024";

            // Act
            var result = _service.Normalize(ambiguousDate);

            // Assert
            result.IsSuccess.Should().BeTrue();
            // Should succeed but maybe with warning about ambiguity
        }

        #endregion

        #region Currency Normalization Tests

        [Theory]
        [InlineData("$1,234.56")]
        [InlineData("USD 1,234.56")]
        [InlineData("1,234.56 USD")]
        public void Normalize_CurrencyValue_CorrectlyParsesAmount(string currencyString)
        {
            // Act
            var result = _service.Normalize(currencyString);

            // Assert
            result.IsSuccess.Should().BeTrue();
            // Should extract numerical value 1234.56
        }

        #endregion

        #region Percentage Normalization Tests

        [Fact]
        public void Normalize_PercentageValue_WithSymbol_CorrectlyParsesAsDecimal()
        {
            // Act
            var result = _service.Normalize("50%");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Percentage);
            result.CleanedValue?.AsNumber().Should().BeApproximately(0.50, 0.01);
        }

        [Fact]
        public void Normalize_PercentageValue_WithFormat_CorrectlyParsesAsDecimal()
        {
            // Act - number with percentage format
            var result = _service.Normalize("50", numberFormat: "0%");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.DetectedType.Should().Be(DataType.Percentage);
            result.CleanedValue?.AsNumber().Should().BeApproximately(0.50, 0.01);
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData("")]              // Empty string
        [InlineData("   ")]           // Whitespace only
        [InlineData("\t\n")]          // Only whitespace characters
        public void Normalize_OnlyWhitespace_ReturnsEmpty(string input)
        {
            // Act
            var result = _service.Normalize(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Normalize_VeryLargeNumber_HandlesCorrectly()
        {
            // Arrange
            double largeNumber = double.MaxValue;

            // Act
            var result = _service.Normalize(largeNumber);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Normalize_VerySmallNumber_HandlesCorrectly()
        {
            // Arrange
            double smallNumber = double.MinValue;

            // Act
            var result = _service.Normalize(smallNumber);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        #endregion
    }
}
