using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace System7.Avalonia.Rendering;

/// <summary>Builds vector drawings of decoded pixel art, one unit per pixel.</summary>
public static class System7Drawings
{
    /// <summary>The set pixels of a mask, as rectangles merged down runs of equal columns.</summary>
    public static Geometry Mask(int width, int height, Func<int, int, bool> isSet)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var open = new Dictionary<(int Left, int Right), int>();
        for (var y = 0; y <= height; y++)
        {
            var row = new HashSet<(int, int)>();
            for (var x = 0; y < height && x < width;)
            {
                if (!isSet(x, y)) { x++; continue; }
                var start = x;
                while (x < width && isSet(x, y)) x++;
                row.Add((start, x));
            }
            foreach (var run in open.Keys.Where(run => !row.Contains(run)).ToList())
            {
                var top = open[run];
                context.BeginFigure(new Point(run.Left, top), true);
                context.LineTo(new Point(run.Right, top));
                context.LineTo(new Point(run.Right, y));
                context.LineTo(new Point(run.Left, y));
                context.EndFigure(true);
                open.Remove(run);
            }
            foreach (var run in row) open.TryAdd(run, y);
        }
        return geometry;
    }

    /// <summary>A picture in its final colors, one drawing per opaque color, whose bounds are the whole picture.</summary>
    public static DrawingImage Picture(int width, int height, Color[] colors)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, width, height)) });
        foreach (var color in colors.Where(color => color.A != 0).Distinct())
            group.Children.Add(new GeometryDrawing
            {
                Brush = new ImmutableSolidColorBrush(color),
                Geometry = Mask(width, height, (x, y) => colors[y * width + x] == color),
            });
        return new DrawingImage(group);
    }
}
