using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace System7.Avalonia.Rendering;

/// <summary>Converters that show a control's brushes as a screen of the bound <see cref="System7ColorDepth"/> does.</summary>
public static class System7Colors
{
    private static Color ColorOf(object? value, Color fallback) => (value as ISolidColorBrush)?.Color ?? fallback;

    private static IBrush Brush(Color color) => new ImmutableSolidColorBrush(color);

    /// <summary>(brush, depth): the nearest color the screen can show. A one-bit screen keeps the brush as it is.</summary>
    public static IMultiValueConverter Map { get; } = new FuncMultiValueConverter<object?, object?>(values =>
    {
        var list = values.ToList();
        if (list.Count < 2 || list[0] is not ISolidColorBrush brush || list[1] is not System7ColorDepth depth) return list.FirstOrDefault();
        if (depth == System7ColorDepth.Monochrome || brush.Color.A == 0) return brush;
        return Brush(System7ColorPalette.Map(brush.Color, depth));
    });

    /// <summary>(brush, depth): the brush on a one-bit screen and black on a color one, as MDEF 0 draws its frame.</summary>
    public static IMultiValueConverter BlackInColor { get; } = new FuncMultiValueConverter<object?, object?>(values =>
    {
        var list = values.ToList();
        return list is [_, System7ColorDepth depth] && depth != System7ColorDepth.Monochrome ? Brushes.Black : list.FirstOrDefault();
    });

    /// <summary>(foreground, background, depth): the true gray between two colors, or the mapped foreground where the screen has none.</summary>
    public static IMultiValueConverter Gray { get; } = new FuncMultiValueConverter<object?, object?>(values =>
    {
        var list = values.ToList();
        if (list.Count < 3 || list[2] is not System7ColorDepth depth) return list.FirstOrDefault();
        if (depth == System7ColorDepth.Monochrome) return list[0];
        var foreground = ColorOf(list[0], Colors.Black);
        return Brush(System7ColorPalette.TryGetGray(foreground, ColorOf(list[1], Colors.White), depth, out var gray)
            ? gray : System7ColorPalette.Map(foreground, depth));
    });

    /// <summary>(foreground, background, depth): whether a disabled part is grayed through a pattern because the screen has no true gray for it.</summary>
    public static IMultiValueConverter Dithers { get; } = new FuncMultiValueConverter<object?, bool>(values =>
    {
        var list = values.ToList();
        if (list.Count < 3 || list[2] is not System7ColorDepth depth) return false;
        return depth == System7ColorDepth.Monochrome
            || !System7ColorPalette.TryGetGray(ColorOf(list[0], Colors.Black), ColorOf(list[1], Colors.White), depth, out _);
    });

    /// <summary>(foreground, background, depth): the gray pattern where the screen has no true gray between two colors, otherwise solid.</summary>
    public static IMultiValueConverter DithersPattern { get; } = new FuncMultiValueConverter<object?, System7PatternKind>(values =>
        Dithers.Convert(values.ToList(), typeof(bool), null, CultureInfo.InvariantCulture) is true ? System7PatternKind.Gray : System7PatternKind.Solid);

    /// <summary>(color, background, highlight, depth): a color as a highlighted List Manager cell shows it, swapping the background and highlight.</summary>
    public static IMultiValueConverter Highlight { get; } = new FuncMultiValueConverter<object?, object?>(values =>
    {
        var list = values.ToList();
        if (list.Count < 4 || list[3] is not System7ColorDepth depth) return list.FirstOrDefault();
        return Brush(System7ColorPalette.Highlight(ColorOf(list[0], Colors.Black), ColorOf(list[1], Colors.White),
            ColorOf(list[2], Colors.Black), depth));
    });

    /// <summary>(depth): the gray the Finder draws disabled small-icon cells in.</summary>
    public static IValueConverter DisabledGray { get; } = new FuncValueConverter<System7ColorDepth, IBrush>(depth =>
        Brush(System7ColorPalette.Map(Color.FromRgb(128, 128, 128), depth)));

    /// <summary>
    /// (icon brush, foreground, background, highlight, depth, disabled): a small-icon cell's icon color, or with the parameter
    /// "Highlighted" a selected cell's. A one-bit screen thresholds it to black or white; a disabled cell grays it, or leaves it
    /// black where the cell is grayed through a pattern.
    /// </summary>
    public static IMultiValueConverter SmallListIcon { get; } = new System7MultiConverter((list, parameter) =>
    {
        if (list.Count < 6 || list[4] is not System7ColorDepth depth) return null;
        var icon = ColorOf(list[0] ?? list[1], Colors.Black);
        var disabled = list[5] is true;
        var dither = disabled && depth is System7ColorDepth.Monochrome or System7ColorDepth.Indexed2;
        var color = dither ? Colors.Black
            : disabled ? System7ColorPalette.Map(Color.FromRgb(128, 128, 128), depth)
            : depth == System7ColorDepth.Monochrome ? (icon.R * 299 + icon.G * 587 + icon.B * 114 >= 128000 ? Colors.White : Colors.Black)
            : System7ColorPalette.Map(icon, depth);
        return Brush(parameter as string == "Highlighted"
            ? System7ColorPalette.Highlight(color, ColorOf(list[2], Colors.White), ColorOf(list[3], Colors.Black), depth)
            : System7ColorPalette.Map(color, depth));
    });

    /// <summary>A color as a brush.</summary>
    public static IValueConverter Solid { get; } = new FuncValueConverter<Color, IBrush>(Brush);
}
