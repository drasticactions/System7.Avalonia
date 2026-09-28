using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>
/// (bounds, fraction): the parts of a CDEF 62 progress bar. "Fill" is the filled part inside the frame, from the left of a wide bar
/// or the bottom of a tall one; "Ink" adds the frame, where the XORed label shows in the background color.
/// </summary>
public sealed class System7ProgressGeometryConverter : IMultiValueConverter
{
    public static System7ProgressGeometryConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not Rect bounds || values[1] is not double fraction) return null;
        var width = (int)Math.Round(bounds.Width);
        var height = (int)Math.Round(bounds.Height);
        var innerWidth = width - 2;
        var innerHeight = height - 4;
        var fill = default(Rect);
        if (innerWidth > 0 && innerHeight > 0)
        {
            var horizontal = innerHeight < innerWidth;
            var filled = (int)Math.Floor((horizontal ? innerWidth : innerHeight) * fraction);
            fill = horizontal ? new Rect(1, 2, filled, innerHeight) : new Rect(1, height - 2 - filled, innerWidth, filled);
        }
        if (parameter as string != "Ink") return new RectangleGeometry(fill);
        var ink = new GeometryGroup { FillRule = FillRule.NonZero };
        ink.Children.Add(new RectangleGeometry(fill));
        ink.Children.Add(System7Geometry.FrameRect(0, 0, width, height));
        return ink;
    }
}
