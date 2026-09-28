using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

public static class System7Font
{
    public const int Ascent = 12;
    public const int Descent = 3;
    public const int Leading = 1;
    public const int Height = 15;
    public const int LineHeight = Ascent + Descent + Leading;

    private static readonly Encoding MacRoman;
    private static readonly Typeface Face = new(new FontFamily("avares://System7.Avalonia/Assets/Chicago12.ttf#System Seven Chicago"));
    private static readonly Dictionary<char, (int Advance, Geometry? Glyph)> Glyphs = [];

    static System7Font()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        MacRoman = Encoding.GetEncoding(10000);
    }

    public static string Normalize(string text) => MacRoman.GetString(MacRoman.GetBytes(text));

    private static (int Advance, Geometry? Glyph) Glyph(char c)
    {
        if (Glyphs.TryGetValue(c, out var glyph)) return glyph;
        var formatted = new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 12, Brushes.Black);
        glyph = ((int)Math.Round(formatted.WidthIncludingTrailingWhitespace), formatted.BuildGeometry(new Point(0, -formatted.Baseline)));
        Glyphs[c] = glyph;
        return glyph;
    }

    public static int Measure(string text, int extra = 0)
    {
        var width = 0;
        foreach (var c in Normalize(text)) width += Glyph(c).Advance + extra;
        return width;
    }

    public static int Measure(string text, System7TextStyle style) => Measure(text, Extra(style));

    public static int Shadow(System7TextStyle style) => ((style & System7TextStyle.Outline) != 0 ? 1 : 0)
        + ((style & System7TextStyle.Shadow) != 0 ? 2 : 0);

    public static int Extra(System7TextStyle style) => Shadow(style) + ((style & System7TextStyle.Bold) != 0 ? 1 : 0)
        - ((style & System7TextStyle.Condensed) != 0 ? 1 : 0) + ((style & System7TextStyle.Extended) != 0 ? 1 : 0);

    public static int GetLineHeight(System7TextStyle style) => LineHeight + Shadow(style) + (Shadow(style) != 0 ? 1 : 0);

    public static string Truncate(string text, int width, int extra = 0)
    {
        if (Measure(text, extra) <= width) return text;
        var length = text.Length;
        while (length > 0 && Measure(text[..length] + "…", extra) > width) length--;
        return Measure("…", extra) <= width ? text[..length] + "…" : "";
    }

    /// <summary>Shortens a label from the middle, as the Finder does, until it fits.</summary>
    public static string TruncateMiddle(string text, int width)
    {
        if (Measure(text) < width) return text;
        var available = width - Measure("…");
        if (available < 0) return "";
        var left = 0;
        var right = text.Length;
        while (right > 0 && Measure(text[(right - 1)..]) <= available / 2) right--;
        var leftWidth = available - Measure(text[right..]);
        while (left < right && Measure(text[..(left + 1)]) <= leftWidth) left++;
        return text[..left] + "…" + text[right..];
    }

    /// <summary>Glyph outlines for a line whose pen starts at x = 0 with the baseline at y = 0.</summary>
    internal static Geometry Build(string text, int extra = 0)
    {
        var group = new GeometryGroup { FillRule = FillRule.NonZero };
        var x = 0;
        foreach (var c in Normalize(text))
        {
            var (advance, glyph) = Glyph(c);
            if (glyph != null)
            {
                var copy = glyph.Clone();
                copy.Transform = new TranslateTransform(x, 0);
                group.Children.Add(copy);
            }
            x += advance + extra;
        }
        return group;
    }

    /// <summary>QuickDraw StdText styling, built from the plain glyph outlines with geometry operations.</summary>
    internal static Geometry Build(string text, System7TextStyle style)
    {
        if (style == System7TextStyle.Plain) return Build(text);
        var extra = Extra(style);
        var shadow = Shadow(style);
        var bold = (style & System7TextStyle.Bold) != 0 ? 1 : 0;
        var italic = (style & System7TextStyle.Italic) != 0;
        var width = Measure(text, style);
        // Merging the per-pixel glyph squares first keeps the later boolean operations small.
        var ink = Union(Build(text, extra));
        if (bold != 0) ink = Union(ink, Translate(ink, 1, 0));
        if (italic)
        {
            Geometry? rows = null;
            for (var row = 0; row < Height; row++)
            {
                var band = new RectangleGeometry(new Rect(-4096, row - Ascent, 8192, 1));
                var slice = Translate(new CombinedGeometry(GeometryCombineMode.Intersect, ink, band), (Height - 1 - row) / 2, 0);
                rows = rows == null ? slice : Union(rows, slice);
            }
            ink = rows!;
        }
        var clipLeft = -(italic ? Descent / 2 : 0) - (shadow != 0 ? 1 : 0);
        var clipRight = width + (italic ? (Ascent - 1) / 2 : 0) + bold - extra + shadow;
        if ((style & System7TextStyle.Underline) != 0)
        {
            // StdText clears the underline wherever ink sits within one pixel of it.
            Geometry? near = null;
            for (var row = 0; row <= 2; row++)
            {
                var slice = Translate(new CombinedGeometry(GeometryCombineMode.Intersect, ink, new RectangleGeometry(new Rect(-4096, row, 8192, 1))), 0, 1 - row);
                near = near == null ? slice : Union(near, slice);
            }
            var spread = Dilate(near!, -1, 1, 0, 0);
            var padding = Height + 4;
            var line = new RectangleGeometry(new Rect(1 - padding, 1, width + 2 * padding - 2, 1));
            ink = Union(ink, new CombinedGeometry(GeometryCombineMode.Exclude, line, spread));
        }
        if (shadow != 0) ink = new CombinedGeometry(GeometryCombineMode.Exclude, Dilate(ink, -1, shadow, -1, shadow), ink);
        return new CombinedGeometry(GeometryCombineMode.Intersect, ink, new RectangleGeometry(new Rect(clipLeft, -4096, clipRight - clipLeft, 8192)));
    }

    private static Geometry Union(Geometry geometry) => new CombinedGeometry(GeometryCombineMode.Union, geometry, new RectangleGeometry());

    private static Geometry Union(Geometry first, Geometry second) => new CombinedGeometry(GeometryCombineMode.Union, first, second);

    /// <summary>The union of copies shifted by every offset in the ranges, grown one axis at a time.</summary>
    private static Geometry Dilate(Geometry geometry, int left, int right, int up, int down)
    {
        var result = geometry;
        for (var dx = left; dx <= right; dx++)
            if (dx != 0) result = Union(result, Translate(geometry, dx, 0));
        var row = result;
        for (var dy = up; dy <= down; dy++)
            if (dy != 0) result = Union(result, Translate(row, 0, dy));
        return result;
    }

    private static Geometry Translate(Geometry geometry, int x, int y)
    {
        var copy = geometry.Clone();
        copy.Transform = new MatrixTransform((geometry.Transform?.Value ?? Matrix.Identity) * Matrix.CreateTranslation(x, y));
        return copy;
    }
}
