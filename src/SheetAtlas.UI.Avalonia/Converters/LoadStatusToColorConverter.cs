using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.UI.Avalonia.Converters;

public class LoadStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LoadStatus status)
        {
            return status switch
            {
                LoadStatus.Success => Brushes.Green,
                LoadStatus.PartialSuccess => Brushes.Orange,
                LoadStatus.Failed => Brushes.Red,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return LoadStatus.Success;
    }
}
