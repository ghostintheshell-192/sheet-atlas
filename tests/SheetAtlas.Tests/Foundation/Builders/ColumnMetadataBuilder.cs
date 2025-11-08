using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Foundation.Builders
{
    /// <summary>
    /// Fluent builder for constructing ColumnMetadata test objects.
    /// Simplifies setup of column analysis results and metadata.
    /// </summary>
    /// <example>
    /// var metadata = new ColumnMetadataBuilder()
    ///     .WithDetectedType(DataType.Currency)
    ///     .WithTypeConfidence(0.95)
    ///     .WithCurrency(CurrencyInfo.EUR)
    ///     .Build();
    /// </example>
    public class ColumnMetadataBuilder
    {
        private double? _width = null;
        private bool _isHidden = false;
        private DataType? _detectedType = null;
        private double? _typeConfidence = null;
        private CurrencyInfo? _currency = null;
        private int _qualityWarningCount = 0;

        public ColumnMetadataBuilder WithWidth(double? width)
        {
            _width = width;
            return this;
        }

        public ColumnMetadataBuilder AsHidden()
        {
            _isHidden = true;
            return this;
        }

        public ColumnMetadataBuilder AsVisible()
        {
            _isHidden = false;
            return this;
        }

        public ColumnMetadataBuilder WithDetectedType(DataType detectedType)
        {
            _detectedType = detectedType;
            return this;
        }

        public ColumnMetadataBuilder WithTypeConfidence(double confidence)
        {
            if (confidence < 0.0 || confidence > 1.0)
                throw new ArgumentException("Confidence must be between 0.0 and 1.0", nameof(confidence));

            _typeConfidence = confidence;
            return this;
        }

        public ColumnMetadataBuilder WithCurrency(CurrencyInfo? currency)
        {
            _currency = currency;
            return this;
        }

        public ColumnMetadataBuilder WithQualityWarningCount(int count)
        {
            if (count < 0)
                throw new ArgumentException("Warning count cannot be negative", nameof(count));

            _qualityWarningCount = count;
            return this;
        }

        public ColumnMetadata Build()
        {
            return new ColumnMetadata
            {
                Width = _width,
                IsHidden = _isHidden,
                DetectedType = _detectedType,
                TypeConfidence = _typeConfidence,
                Currency = _currency,
                QualityWarningCount = _qualityWarningCount
            };
        }
    }

    /// <summary>
    /// Factory methods for common column metadata patterns.
    /// </summary>
    public static class ColumnMetadataFactory
    {
        public static ColumnMetadata NumericColumn(double confidence = 0.95)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Number)
                .WithTypeConfidence(confidence)
                .Build();

        public static ColumnMetadata DateColumn(double confidence = 0.90)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Date)
                .WithTypeConfidence(confidence)
                .Build();

        public static ColumnMetadata CurrencyColumn(
            CurrencyInfo currency,
            double confidence = 0.95)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Currency)
                .WithTypeConfidence(confidence)
                .WithCurrency(currency)
                .Build();

        public static ColumnMetadata TextColumn(double confidence = 0.85)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Text)
                .WithTypeConfidence(confidence)
                .Build();

        public static ColumnMetadata BooleanColumn(double confidence = 0.90)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Boolean)
                .WithTypeConfidence(confidence)
                .Build();

        public static ColumnMetadata MixedTypeColumn(double confidence = 0.50)
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Unknown)
                .WithTypeConfidence(confidence)
                .WithQualityWarningCount(5)
                .Build();

        public static ColumnMetadata UnknownColumn()
            => new ColumnMetadataBuilder()
                .WithDetectedType(DataType.Unknown)
                .WithTypeConfidence(0.0)
                .Build();
    }
}
