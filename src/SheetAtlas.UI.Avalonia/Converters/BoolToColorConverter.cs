using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SheetAtlas.UI.Avalonia.Converters;

public class BoolToColorConverter : IValueConverter
{
    public IBrush TrueColor { get; set; } = Brushes.Green;
    public IBrush FalseColor { get; set; } = Brushes.Red;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }
        return FalseColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}
