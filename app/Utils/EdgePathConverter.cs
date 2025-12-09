using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Tessera.Models.Graph;

namespace Tessera.Utils;

public class EdgePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GraphEdge edge)
        {
            var start = edge.StartPoint;
            var end = edge.EndPoint;
            
            // Calculate bezier control points for smooth curve
            // Standard horizontal tree: control points are offset horizontally
            double dist = Math.Abs(end.X - start.X) / 2;
            
            var p1 = new Point(start.X + dist, start.Y);
            var p2 = new Point(end.X - dist, end.Y);
            
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, false);
                ctx.CubicBezierTo(p1, p2, end);
            }
            return geometry;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
