using Avalonia;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

public static class System7Geometry
{
    /// <summary>Horizontal inset of a QuickDraw oval corner for one scan line.</summary>
    public static int OvalInset(int row, int height, double radiusX, double radiusY)
    {
        if (radiusX <= 0 || radiusY <= 0) return 0;
        var dy = Math.Max(0, radiusY - Math.Min(row + 0.5, height - row - 0.5));
        return Math.Max(0, (int)Math.Ceiling(radiusX * (1 - Math.Sqrt(1 - dy * dy / (radiusY * radiusY))) - 0.5));
    }

    /// <summary>The outline of rows [0, height) whose left and right edges step in by <paramref name="inset"/>.</summary>
    public static Geometry Staircase(double x, double y, int width, int height, Func<int, int> inset)
    {
        var path = new StreamGeometry();
        if (width <= 0 || height <= 0) return path;
        using var context = path.Open();
        var current = inset(0);
        context.BeginFigure(new Point(x + current, y), true);
        context.LineTo(new Point(x + width - current, y));
        for (var row = 1; row < height; row++)
        {
            var next = inset(row);
            if (next == current) continue;
            context.LineTo(new Point(x + width - current, y + row));
            current = next;
            context.LineTo(new Point(x + width - current, y + row));
        }
        context.LineTo(new Point(x + width - current, y + height));
        context.LineTo(new Point(x + current, y + height));
        for (var row = height - 2; row >= 0; row--)
        {
            var next = inset(row);
            if (next == current) continue;
            context.LineTo(new Point(x + current, y + row + 1));
            current = next;
            context.LineTo(new Point(x + current, y + row + 1));
        }
        context.EndFigure(true);
        return path;
    }

    /// <summary>QuickDraw's filled round rectangle; WDEF 1 squares the top or bottom eight rows of its title and content regions.</summary>
    public static Geometry RoundRect(double x, double y, int width, int height, double diameter, bool squareTop = false, bool squareBottom = false) =>
        Staircase(x, y, width, height, RoundInset(width, height, diameter, squareTop, squareBottom));

    private static Func<int, int> RoundInset(int width, int height, double diameter, bool squareTop, bool squareBottom)
    {
        var rx = Math.Min(diameter, width) / 2;
        var ry = Math.Min(diameter, height) / 2;
        return row => squareTop && row < 8 || squareBottom && row >= height - 8 ? 0 : OvalInset(row, height, rx, ry);
    }

    /// <summary>The pixels of a round rectangle that touch its outside, as WDEF 1 outlines its regions.</summary>
    public static Geometry OutlineRoundRect(double x, double y, int width, int height, double diameter, bool squareTop = false, bool squareBottom = false)
    {
        var inset = RoundInset(width, height, diameter, squareTop, squareBottom);
        var outer = Staircase(x, y, width, height, inset);
        if (width <= 2 || height <= 2) return outer;
        var inner = Staircase(x + 1, y + 1, width - 2, height - 2, row => Math.Max(inset(row), Math.Max(inset(row + 1), inset(row + 2))));
        return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
    }

    /// <summary>QuickDraw's framed round rectangle for a square pen of <paramref name="pen"/> pixels.</summary>
    public static Geometry FrameRoundRect(double x, double y, int width, int height, double diameter, int pen = 1)
    {
        var outer = RoundRect(x, y, width, height, diameter);
        if (width <= 2 * pen || height <= 2 * pen) return outer;
        var rx = Math.Max(0, Math.Min(diameter, width) / 2 - pen);
        var ry = Math.Max(0, Math.Min(diameter, height) / 2 - pen);
        var innerHeight = height - 2 * pen;
        var inner = Staircase(x + pen, y + pen, width - 2 * pen, innerHeight, row => OvalInset(row, innerHeight, rx, ry));
        return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
    }

    /// <summary>A rectangular frame, its hole wound opposite the outline so it stays open under either fill rule.</summary>
    public static Geometry FrameRect(double x, double y, double width, double height, double pen = 1)
    {
        var geometry = new StreamGeometry();
        if (width <= 0 || height <= 0) return geometry;
        using var context = geometry.Open();
        context.BeginFigure(new Point(x, y), true);
        context.LineTo(new Point(x + width, y));
        context.LineTo(new Point(x + width, y + height));
        context.LineTo(new Point(x, y + height));
        context.EndFigure(true);
        if (width > 2 * pen && height > 2 * pen)
        {
            context.BeginFigure(new Point(x + pen, y + pen), true);
            context.LineTo(new Point(x + pen, y + height - pen));
            context.LineTo(new Point(x + width - pen, y + height - pen));
            context.LineTo(new Point(x + width - pen, y + pen));
            context.EndFigure(true);
        }
        return geometry;
    }
}
