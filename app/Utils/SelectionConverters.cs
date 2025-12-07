using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tessera.Utils;

/// <summary>
/// Converts a boolean IsSelected value to a brush for column header backgrounds.
/// </summary>
public class BoolToBrushConverter : IMultiValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();
    
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#403080F0"));
    private static readonly IBrush NormalBrush = Brushes.Transparent;
    
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is bool isSelected)
        {
            return isSelected ? SelectedBrush : NormalBrush;
        }
        return NormalBrush;
    }
}

/// <summary>
/// Converts a boolean IsSelected value to FontWeight for column headers.
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return FontWeight.SemiBold;
        }
        return FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
