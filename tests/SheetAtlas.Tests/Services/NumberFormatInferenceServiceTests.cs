using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Infrastructure.External.Readers;
using Xunit;

namespace SheetAtlas.Tests.Services
{
    public class NumberFormatInferenceServiceTests
    {
        private readonly NumberFormatInferenceService _service;

        public NumberFormatInferenceServiceTests()
        {
            _service = new NumberFormatInferenceService();
        }

        #region Percentage Tests

        [Fact]
        public void InferFormat_IntegerPercentage_ReturnsCorrectValueAndFormat()
        {
            // Arrange
            var text = "15%";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(0.15, result.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0%", result.InferredFormat);
        }

        [Fact]
        public void InferFormat_DecimalPercentage_ReturnsCorrectValueAndFormat()
        {
            // Arrange
            var text = "15.5%";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(0.155, result.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0.0%", result.InferredFormat);
        }

        [Fact]
        public void InferFormat_ZeroPercentage_ReturnsCorrectValueAndFormat()
        {
            // Arrange
            var text = "0%";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.0, result!.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0%", result.InferredFormat);
        }

        [Fact]
        public void InferFormat_HundredPercentage_ReturnsCorrectValue()
        {
            // Arrange
            var text = "100%";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1.0, result!.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0%", result.InferredFormat);
        }

        #endregion

        #region Scientific Notation Tests

        [Fact]
        public void InferFormat_ScientificNotationPositiveExponent_ReturnsNumberWithFormat()
        {
            // Arrange
            var text = "2.15639E+11";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(215639000000.0, result.ParsedValue.AsFloatingPoint(), precision: 1);
            Assert.Equal("0.00000E+00", result.InferredFormat);  // 5 decimals in mantissa
        }

        [Fact]
        public void InferFormat_ScientificNotationNegativeExponent_ReturnsNumberWithFormat()
        {
            // Arrange
            var text = "1.5e-3";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(0.0015, result.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0.00E+00", result.InferredFormat);  // 1 decimal + minimum 2 = 2 decimals
        }

        [Fact]
        public void InferFormat_ScientificNotationLowercaseE_ReturnsNumberWithFormat()
        {
            // Arrange
            var text = "3.14e+2";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(314.0, result.ParsedValue.AsFloatingPoint(), precision: 1);
            Assert.Equal("0.00E+00", result.InferredFormat);  // 2 decimals (minimum)
        }

        [Fact]
        public void InferFormat_ScientificNotationManyDecimals_PreservesDecimalCount()
        {
            // Arrange
            var text = "8.6823926503546e-05";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(0.000086823926503546, result.ParsedValue.AsFloatingPoint(), precision: 15);
            Assert.Equal("0.0000000000000E+00", result.InferredFormat);  // 13 decimals
        }

        #endregion

        #region Decimal Precision Tests

        [Fact]
        public void InferFormat_IntegerNumber_ReturnsFormatWithNoDecimals()
        {
            // Arrange
            var text = "42";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.ParsedValue.IsFloatingPoint);
            Assert.Equal(42.0, result.ParsedValue.AsFloatingPoint());
            Assert.Equal("0", result.InferredFormat);
        }

        [Fact]
        public void InferFormat_TwoDecimalPlaces_ReturnsCorrectFormat()
        {
            // Arrange
            var text = "123.45";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123.45, result!.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0.00", result.InferredFormat);
        }

        [Fact]
        public void InferFormat_FiveDecimalPlaces_ReturnsCorrectFormat()
        {
            // Arrange
            var text = "0.15000";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.15, result!.ParsedValue.AsFloatingPoint(), precision: 10);
            Assert.Equal("0.00000", result.InferredFormat);  // Preserves trailing zeros
        }

        [Fact]
        public void InferFormat_ManyDecimalPlaces_PreservesCount()
        {
            // Arrange
            var text = "3.141592653589793";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("0.000000000000000", result!.InferredFormat);  // 15 decimal places
        }

        #endregion

        #region Edge Cases and Nulls

        [Fact]
        public void InferFormat_EmptyString_ReturnsNull()
        {
            // Arrange
            var text = "";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void InferFormat_WhitespaceOnly_ReturnsNull()
        {
            // Arrange
            var text = "   ";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void InferFormat_NonNumericText_ReturnsNull()
        {
            // Arrange
            var text = "Hello World";

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void InferFormat_InvalidPercentage_ReturnsNull()
        {
            // Arrange
            var text = "15%extra";  // Invalid format

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void InferFormat_PercentageWithSpaces_ReturnsNull()
        {
            // Arrange
            var text = "15 %";  // Space before %

            // Act
            var result = _service.InferFormat(text);

            // Assert
            Assert.Null(result);  // Pattern requires no space
        }

        #endregion
    }
}
