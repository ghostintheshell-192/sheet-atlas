using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SheetAtlas.UI.Avalonia.Converters;

public class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;

        if (value == null)
            return !invert;

        if (value is ICollection collection)
        {
            bool isEmpty = collection.Count == 0;
            return invert ? !isEmpty : isEmpty;
        }

        if (value is IEnumerable enumerable)
        {
            bool hasItems = enumerable.GetEnumerator().MoveNext();
            return invert ? hasItems : !hasItems;
        }

        return !invert;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}
