using System.Globalization;
using System.Runtime.InteropServices;

namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Efficient cell value storage using explicit union layout (NO BOXING).
    /// All value types are stored directly in overlapping memory using FieldOffset.
    /// Memory layout: 8 bytes (union of double/long/DateTime) + 8 bytes (string ref) + 1 byte (type) = 17 bytes (+ padding).
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct SACellValue : IEquatable<SACellValue>
    {
        // Union of value types - all share the same 8 bytes at offset 0
        [FieldOffset(0)] private readonly double _numberValue;
        [FieldOffset(0)] private readonly long _integerValue;
        [FieldOffset(0)] private readonly DateTime _dateTimeValue;
        [FieldOffset(0)] private readonly bool _booleanValue;

        // String is a reference type, stored separately at offset 8
        [FieldOffset(8)] private readonly string? _textValue;

        // Type discriminator at offset 16 (after 8-byte union + 8-byte string ref)
        [FieldOffset(16)] private readonly CellType _type;

        private SACellValue(double value)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = CellType.Empty;

            _numberValue = value;
            _type = CellType.Number;
        }

        private SACellValue(long value)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = CellType.Empty;

            _integerValue = value;
            _type = CellType.Integer;
        }

        private SACellValue(bool value)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = CellType.Empty;

            _booleanValue = value;
            _type = CellType.Boolean;
        }

        private SACellValue(DateTime value)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = CellType.Empty;

            _dateTimeValue = value;
            _type = CellType.DateTime;
        }

        private SACellValue(string value)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = CellType.Empty;

            _textValue = value ?? string.Empty;
            _type = CellType.Text;
        }

        private SACellValue(CellType type)
        {
            _numberValue = 0;
            _integerValue = 0;
            _dateTimeValue = default;
            _booleanValue = false;
            _textValue = null;
            _type = type;
        }

        public static SACellValue FromNumber(double value) => new(value);
        public static SACellValue FromInteger(long value) => new(value);
        public static SACellValue FromBoolean(bool value) => new(value);
        public static SACellValue FromDateTime(DateTime value) => new(value);
        public static SACellValue FromText(string value) => new(value);
        public static SACellValue Empty => new(CellType.Empty);

        public static SACellValue FromString(string value, StringPool? stringPool = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Empty;

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
                return FromInteger(longValue);

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
                return FromNumber(doubleValue);

            if (bool.TryParse(value, out bool boolValue))
                return FromBoolean(boolValue);

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                return FromDateTime(dateValue);

            if (stringPool != null)
            {
                value = stringPool.Intern(value);
            }

            return FromText(value);
        }

        public CellType Type => _type;
        public bool IsEmpty => _type == CellType.Empty;
        public bool IsNumber => _type == CellType.Number;
        public bool IsInteger => _type == CellType.Integer;
        public bool IsBoolean => _type == CellType.Boolean;
        public bool IsDateTime => _type == CellType.DateTime;
        public bool IsText => _type == CellType.Text;

        public double AsNumber() => _type == CellType.Number ? _numberValue : 0.0;
        public long AsInteger() => _type == CellType.Integer ? _integerValue : 0L;
        public bool AsBoolean() => _type == CellType.Boolean && _booleanValue;
        public DateTime AsDateTime() => _type == CellType.DateTime ? _dateTimeValue : DateTime.MinValue;
        public string AsText() => _type == CellType.Text ? _textValue ?? string.Empty : string.Empty;

        public override string ToString()
        {
            return _type switch
            {
                CellType.Number => _numberValue.ToString(CultureInfo.InvariantCulture),
                CellType.Integer => _integerValue.ToString(CultureInfo.InvariantCulture),
                CellType.Boolean => _booleanValue.ToString(),
                CellType.DateTime => _dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CellType.Text => _textValue ?? string.Empty,
                CellType.Empty => string.Empty,
                _ => string.Empty
            };
        }

        public bool Equals(SACellValue other)
        {
            if (_type != other._type)
                return false;

            return _type switch
            {
                CellType.Number => Math.Abs(AsNumber() - other.AsNumber()) < double.Epsilon,
                CellType.Integer => AsInteger() == other.AsInteger(),
                CellType.Boolean => AsBoolean() == other.AsBoolean(),
                CellType.DateTime => AsDateTime() == other.AsDateTime(),
                CellType.Text => string.Equals(AsText(), other.AsText(), StringComparison.Ordinal),
                CellType.Empty => true,
                _ => false
            };
        }

        public override bool Equals(object? obj) => obj is SACellValue other && Equals(other);

        public override int GetHashCode()
        {
            return _type switch
            {
                CellType.Number => HashCode.Combine(_type, _numberValue),
                CellType.Integer => HashCode.Combine(_type, _integerValue),
                CellType.Boolean => HashCode.Combine(_type, _booleanValue),
                CellType.DateTime => HashCode.Combine(_type, _dateTimeValue),
                CellType.Text => HashCode.Combine(_type, _textValue),
                CellType.Empty => HashCode.Combine(_type),
                _ => HashCode.Combine(_type)
            };
        }

        public static bool operator ==(SACellValue left, SACellValue right) => left.Equals(right);
        public static bool operator !=(SACellValue left, SACellValue right) => !left.Equals(right);
    }

    /// <summary>
    /// Cell data type discriminator.
    /// Used by CellValue to determine the actual type stored.
    /// </summary>
    public enum CellType : byte
    {
        Empty = 0,    // No value (null/whitespace)
        Text = 1,     // String data (reference type - unavoidable for text)
        Integer = 2,  // Whole numbers (long, 8 bytes)
        Number = 3,   // Decimal numbers (double, 8 bytes)
        Boolean = 4,  // True/False (bool, 1 byte)
        DateTime = 5  // Date/time values (DateTime, 8 bytes)
    }
}
