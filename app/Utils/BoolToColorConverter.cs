using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tessera.Utils;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            // True = Success (valid), False = Error (invalid)
            return b ? Brushes.ForestGreen : Brushes.Crimson;
            // Note: In a real app we might want to use DynamicResources, but for a simple converter Brushes are okay,
            // or we could return strings/Hex codes if binding to specific properties.
            // For now let's return standard brushes or maybe use the resource keys if we had access to Application.Current.
            // Let's stick to simple colors first or improve to look up resources.
            
            // Better approach for theming:
            // return b ? new SolidColorBrush(Color.Parse("#10B981")) : new SolidColorBrush(Color.Parse("#EF4444"));
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
