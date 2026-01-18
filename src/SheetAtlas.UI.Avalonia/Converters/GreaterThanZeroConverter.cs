using System.Globalization;
using Avalonia.Data.Converters;

namespace SheetAtlas.UI.Avalonia.Converters;

/// <summary>
/// Converts a nullable int to bool (true if value > 0).
/// Used for badge visibility on sidebar icons.
/// </summary>
public class GreaterThanZeroConverter : IValueConverter
{
    public static readonly GreaterThanZeroConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count > 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
