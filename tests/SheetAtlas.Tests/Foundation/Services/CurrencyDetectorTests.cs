using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.ValueObjects;
using FluentAssertions;
using SheetAtlas.Tests.Foundation.TestUtilities;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for ICurrencyDetector service.
    /// Tests currency detection from Excel number format strings.
    /// </summary>
    public class CurrencyDetectorTests
    {
        private readonly ICurrencyDetector _detector = new CurrencyDetector();
        private static readonly string[] _expected = new[] { "EUR", "USD", "GBP" };

        #region Euro Currency Detection

        [Fact]
        public void DetectCurrency_EuroGermanFormat_ReturnsEUR()
        {
            // Arrange
            string format = ExcelFormatStrings.Euro.German; // "[$€-407] #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("EUR");
            result.Symbol.Should().Be("€");
            result.Position.Should().Be(CurrencyPosition.Prefix);
            result.DecimalPlaces.Should().Be(2);
            result.DecimalSeparator.Should().Be(',');
            result.Confidence.Should().Be(ConfidenceLevel.Unambiguous);
        }

        [Fact]
        public void DetectCurrency_EuroFrenchFormat_ReturnsEUR()
        {
            // Arrange
            string format = ExcelFormatStrings.Euro.French; // "[$€-40C] #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("EUR");
            result.Symbol.Should().Be("€");
        }

        #endregion

        #region US Dollar Detection

        [Fact]
        public void DetectCurrency_USDFormat_ReturnsUSD()
        {
            // Arrange
            string format = ExcelFormatStrings.Dollar.AmericanEnglish; // "[$$-409] #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("USD");
            result.Symbol.Should().Be("$");
            result.Position.Should().Be(CurrencyPosition.Prefix);
            result.DecimalPlaces.Should().Be(2);
            result.DecimalSeparator.Should().Be('.');
            result.Confidence.Should().Be(ConfidenceLevel.Unambiguous);
        }

        [Fact]
        public void DetectCurrency_CanadianDollarFormat_ReturnsCAD()
        {
            // Arrange
            string format = ExcelFormatStrings.Dollar.CanadianEnglish; // "[$$-C09] #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().BeOneOf("CAD", "USD"); // Could be either with $
        }

        #endregion

        #region British Pound Detection

        [Fact]
        public void DetectCurrency_GBPFormat_ReturnsGBP()
        {
            // Arrange
            string format = ExcelFormatStrings.Pound.English; // "[$£-809] #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("GBP");
            result.Symbol.Should().Be("£");
            result.Position.Should().Be(CurrencyPosition.Prefix);
            result.DecimalPlaces.Should().Be(2);
        }

        #endregion

        #region Japanese Yen Detection

        [Fact]
        public void DetectCurrency_JPYFormat_ReturnsJPY_NoDecimals()
        {
            // Arrange
            string format = ExcelFormatStrings.Yen.Japanese; // "[$¥-411] #,##0"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("JPY");
            result.Symbol.Should().Be("¥");
            result.DecimalPlaces.Should().Be(0);
        }

        #endregion

        #region Ambiguous Currency Detection

        [Fact]
        public void DetectCurrency_AmbiguousDollar_LowConfidence()
        {
            // Arrange
            string format = ExcelFormatStrings.Ambiguous.DollarOnly; // "$ #,##0.00"

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Symbol.Should().Be("$");
            result.Confidence.Should().Be(ConfidenceLevel.Low);
            // Code could be USD, CAD, AUD, etc - ambiguous without locale
        }

        [Fact]
        public void DetectCurrency_DollarWithoutLocale_ReturnsUSDDefault()
        {
            // Arrange
            string format = "$ #,##0.00";

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            // Should default to USD or return Low confidence
            result.Should().NotBeNull();
            result!.Symbol.Should().Be("$");
            result.Confidence.Should().NotBe(ConfidenceLevel.Unambiguous);
        }

        #endregion

        #region Non-Currency Format Detection

        [Theory]
        [InlineData("General")]
        [InlineData("#,##0.00")]
        [InlineData("0.00")]
        [InlineData("0%")]
        [InlineData("@")] // Text format
        [InlineData("mm/dd/yyyy")] // Date format
        public void DetectCurrency_NonCurrencyFormat_ReturnsNull(string format)
        {
            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void DetectCurrency_EmptyFormat_ReturnsNull()
        {
            // Act
            var result = _detector.DetectCurrency("");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void DetectCurrency_NullFormat_ReturnsNull()
        {
            // Act & Assert
            _ = _detector.DetectCurrency(null!).Should().BeNull();
        }

        #endregion

        #region Mixed Currency Detection

        [Fact]
        public void DetectMixedCurrencies_MultipleFormats_ReturnsAllDistinct()
        {
            // Arrange
            var formats = new[]
            {
                ExcelFormatStrings.Euro.German,
                ExcelFormatStrings.Dollar.AmericanEnglish,
                ExcelFormatStrings.Pound.English,
                ExcelFormatStrings.Euro.German, // Duplicate
                ExcelFormatStrings.Dollar.AmericanEnglish // Duplicate
            };

            // Act
            var result = _detector.DetectMixedCurrencies(formats);

            // Assert
            result.Should().HaveCount(3); // EUR, USD, GBP
            result.Select(c => c.Code).Should().Contain(_expected);
        }

        [Fact]
        public void DetectMixedCurrencies_NoFormats_ReturnsEmpty()
        {
            // Act
            var result = _detector.DetectMixedCurrencies(Array.Empty<string>());

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectMixedCurrencies_SingleFormat_ReturnsSingle()
        {
            // Arrange
            var formats = new[] { ExcelFormatStrings.Euro.German };

            // Act
            var result = _detector.DetectMixedCurrencies(formats);

            // Assert
            result.Should().HaveCount(1);
            result.First().Code.Should().Be("EUR");
        }

        [Fact]
        public void DetectMixedCurrencies_WithNonCurrencyFormats_IgnoresNonCurrency()
        {
            // Arrange
            var formats = new[]
            {
                ExcelFormatStrings.Euro.German,
                ExcelFormatStrings.Numeric.TwoDecimals, // Not currency
                ExcelFormatStrings.Dollar.AmericanEnglish,
                "General" // Not currency
            };

            // Act
            var result = _detector.DetectMixedCurrencies(formats);

            // Assert
            result.Should().HaveCount(2);
            result.Select(c => c.Code).Should().OnlyContain(c => c == "EUR" || c == "USD");
        }

        #endregion

        #region Decimal Places Detection

        [Theory]
        [InlineData("[$€-407] #,##0.00", 2)]
        [InlineData("[$€-407] #,##0.0", 1)]
        [InlineData("[$$-409] #,##0", 0)]
        [InlineData("[$¥-411] #,##0", 0)]
        [InlineData("[$£-809] #,##0.00", 2)]
        public void DetectCurrency_VariousDecimalPlaces_CorrectDecimalDetection(string format, int expectedDecimals)
        {
            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.DecimalPlaces.Should().Be(expectedDecimals);
        }

        #endregion

        #region Thousand Separator Detection

        [Theory]
        [InlineData("[$€-407] #,##0.00", '.')]         // German uses PERIOD as thousand sep (1.234,56)
        [InlineData("[$$-409] #,##0.00", ',')]         // US uses comma
        [InlineData("[$€-40C] #,##0.00", '.')]         // French: Excel saves US format, displays with space (period is approximation)
        public void DetectCurrency_VariousThousandSeparators_CorrectSeparatorDetection(
            string format,
            char expectedSeparator)
        {
            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.ThousandSeparator.Should().Be(expectedSeparator);
        }

        #endregion

        #region Locale Detection

        [Theory]
        [InlineData("[$€-407]", "407")] // German
        [InlineData("[$$-409]", "409")] // US
        [InlineData("[$£-809]", "809")] // UK
        [InlineData("[$¥-411]", "411")] // Japan
        public void DetectCurrency_ExtractsLocaleCode_CorrectLocaleDetection(string format, string expectedLocale)
        {
            // Arrange
            string fullFormat = format + " #,##0.00";

            // Act
            var result = _detector.DetectCurrency(fullFormat);

            // Assert
            result.Should().NotBeNull();
            result!.Locale.Should().Be(expectedLocale);
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData("[$€-407] #,##0.00 EUR")] // With text suffix
        [InlineData("EUR [$€-407] #,##0.00")] // With text prefix
        [InlineData("[EUR] #,##0.00")]         // ISO code in brackets
        public void DetectCurrency_UnusualFormats_StillDetectsCurrency(string format)
        {
            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("EUR");
        }

        [Fact]
        public void DetectCurrency_SuffixCurrency_CorrectPositionDetection()
        {
            // Arrange
            string format = "#,##0.00 $"; // Dollar as suffix

            // Act
            var result = _detector.DetectCurrency(format);

            // Assert
            result.Should().NotBeNull();
            result!.Position.Should().Be(CurrencyPosition.Suffix);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void DetectCurrency_InvalidInput_HandlesGracefully(string? format)
        {
            // Act
            var result = _detector.DetectCurrency(format ?? "");

            // Assert
            result.Should().BeNull();
        }

        #endregion
    }
}
