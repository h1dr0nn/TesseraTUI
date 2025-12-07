using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tessera.Utils;

public class StringToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && double.TryParse(s, out var result))
        {
            return result;
        }
        return 14.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString();
    }
}

public class BoolToTextWrappingConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return TextWrapping.Wrap;
        }
        return TextWrapping.NoWrap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is TextWrapping t && t == TextWrapping.Wrap;
    }
}

public class CategoryToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedCategory && parameter is string targetCategory)
        {
            // "General" typically implies showing the first section or broadly applicable stuff
            // For this implementation, we map "General" to the Appearance section if param is 'General'
            
            return selectedCategory == targetCategory;
        }
        return true; // Default to visible if something is wrong
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IntToDoubleOrNanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i && i > 0)
        {
            return (double)i;
        }
        return double.NaN; // Auto
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d && !double.IsNaN(d))
        {
            return (int)d;
        }
        return 0;
    }
}
