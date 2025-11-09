using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Foundation.Builders
{
    /// <summary>
    /// Fluent builder for constructing SACellData test objects.
    /// Simplifies creation of cells with various metadata configurations.
    /// </summary>
    /// <example>
    /// var cell = new SACellDataBuilder()
    ///     .WithValue(1234.56m)
    ///     .WithNumberFormat("[$€-407] #,##0.00")
    ///     .WithDataType(CellDataType.Number)
    ///     .WithCurrency(CurrencyInfo.EUR)
    ///     .Build();
    /// </example>
    public class SACellDataBuilder
    {
        private object? _value = null;
        private string? _numberFormat = null;
        private CellDataType _dataType = CellDataType.General;
        private CurrencyInfo? _currency = null;
        private DataType? _detectedType = null;
        private string? _comment = null;

        public SACellDataBuilder WithValue(object? value)
        {
            _value = value;
            return this;
        }

        public SACellDataBuilder WithNumberFormat(string? format)
        {
            _numberFormat = format;
            return this;
        }

        public SACellDataBuilder WithDataType(CellDataType dataType)
        {
            _dataType = dataType;
            return this;
        }

        public SACellDataBuilder WithCurrency(CurrencyInfo? currency)
        {
            _currency = currency;
            return this;
        }

        public SACellDataBuilder WithDetectedType(DataType? detectedType)
        {
            _detectedType = detectedType;
            return this;
        }

        public SACellDataBuilder WithComment(string? comment)
        {
            _comment = comment;
            return this;
        }

        public SACellDataBuilder AsError()
        {
            _dataType = CellDataType.Error;
            return this;
        }

        public SACellData Build()
        {
            // Convert value to SACellValue using factory methods
            var cellValue = _value switch
            {
                null => SACellValue.Empty,
                double d => SACellValue.FromNumber(d),
                decimal m => SACellValue.FromNumber((double)m),
                int i => SACellValue.FromInteger(i),
                long l => SACellValue.FromInteger(l),
                bool b => SACellValue.FromBoolean(b),
                DateTime dt => SACellValue.FromDateTime(dt),
                string s => SACellValue.FromText(s),
                _ => SACellValue.FromText(_value.ToString() ?? string.Empty)
            };

            // Create metadata if any metadata fields are set
            CellMetadata? metadata = null;
            if (_currency != null || _detectedType != null || _numberFormat != null || _comment != null)
            {
                metadata = new CellMetadata
                {
                    Currency = _currency,
                    DetectedType = _detectedType
                };
            }

            return new SACellData(cellValue, metadata);
        }
    }

    /// <summary>
    /// Factory methods for common test cell patterns.
    /// </summary>
    public static class SACellDataFactory
    {
        public static SACellData Numeric(decimal value)
            => new SACellData(SACellValue.FromNumber((double)value));

        public static SACellData Text(string value)
            => new SACellData(SACellValue.FromText(value));

        public static SACellData Date(DateTime value)
            => new SACellData(SACellValue.FromDateTime(value));

        public static SACellData Boolean(bool value)
            => new SACellData(SACellValue.FromBoolean(value));

        public static SACellData Currency(decimal value, string currencyCode)
        {
            var cellValue = SACellValue.FromNumber((double)value);
            var currency = currencyCode.ToUpperInvariant() switch
            {
                "EUR" => CurrencyInfo.EUR,
                "USD" => CurrencyInfo.USD,
                "GBP" => CurrencyInfo.GBP,
                "JPY" => CurrencyInfo.JPY,
                _ => new CurrencyInfo(currencyCode, currencyCode) // Fallback
            };
            var metadata = new CellMetadata
            {
                Currency = currency
            };
            return new SACellData(cellValue, metadata);
        }

        public static SACellData Empty()
            => new SACellData(SACellValue.Empty);

        public static SACellData Error(string errorCode)
            => new SACellData(SACellValue.FromText(errorCode));
    }
}
