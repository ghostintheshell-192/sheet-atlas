using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.UI.Avalonia.Converters;

public class LogSeverityToColorConverter : IValueConverter
{
    public static readonly LogSeverityToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Info => new SolidColorBrush(Color.Parse("#2196F3")), // Blue
                LogSeverity.Warning => new SolidColorBrush(Color.Parse("#FF6B35")), // Orange
                LogSeverity.Error => new SolidColorBrush(Color.Parse("#D32F2F")), // Red
                LogSeverity.Critical => new SolidColorBrush(Color.Parse("#B71C1C")), // Dark Red
                _ => new SolidColorBrush(Color.Parse("#757575")) // Gray
            };
        }
        return new SolidColorBrush(Color.Parse("#757575"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return LogSeverity.Info;
    }
}
