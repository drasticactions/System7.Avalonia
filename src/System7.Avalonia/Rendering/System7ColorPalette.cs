using Avalonia.Media;

namespace System7.Avalonia.Rendering;

internal static class System7ColorPalette
{
    private static readonly byte[] Gray4 = Load("System7Color4.bin", 4 * 6 + 256);
    private static readonly byte[] Color16 = Load("System7Color16.bin", 16 * 6 + 4096);
    private static readonly byte[] Color256 = Load("System7Color256.bin", 256 * 6 + 4096);

    /// <summary>An embedded native table from the Assets folder, which must have the given length.</summary>
    internal static byte[] Load(string name, int length)
    {
        using var stream = typeof(System7ColorPalette).Assembly.GetManifestResourceStream("System7.Avalonia.Assets." + name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();
        if (data.Length != length) throw new InvalidDataException($"The native table {name} has an invalid length.");
        return data;
    }

    private readonly record struct Rgb16(int Red, int Green, int Blue)
    {
        public Color Color => Color.FromRgb((byte)(Red >> 8), (byte)(Green >> 8), (byte)(Blue >> 8));
        public int Distance(Rgb16 other) => Math.Max(Math.Abs(Red - other.Red), Math.Max(Math.Abs(Green - other.Green), Math.Abs(Blue - other.Blue)));
    }

    public static Color Map(Color color, System7ColorDepth depth) => Map(ToRgb16(color), depth).Color;

    internal static Color Highlight(Color color, Color background, Color highlight, System7ColorDepth depth)
    {
        color = Map(color, depth);
        background = Map(background, depth);
        highlight = Map(highlight, depth);
        if (highlight == background) highlight = Invert(background, depth);
        return color == background ? highlight : color == highlight ? background : color;
    }

    internal static Color ResourceIconColor(int red, int green, int blue, System7ColorDepth depth, bool disabled) => Map(disabled
        ? new Rgb16((red + 65535) >> 1, (green + 65535) >> 1, (blue + 65535) >> 1)
        : new Rgb16(red, green, blue), depth).Color;

    internal static Color WindowBitmapColor(Color foreground, Color background, int index, System7ColorDepth depth)
    {
        if (index == 0) return Map(foreground, depth);
        // WDEF 0 supplies one black color-table entry. CopyBits fills the remaining 15 entries with a 16.16 ramp.
        static int Component(byte first, byte last, int index)
        {
            var start = (long)first * 257 << 16;
            var step = ((long)(last - first) * 257 << 16) / 14;
            return (int)((start + 0x8000 + step * (15 - index)) >> 16);
        }
        return Map(new Rgb16(Component(foreground.R, background.R, index), Component(foreground.G, background.G, index),
            Component(foreground.B, background.B, index)), depth).Color;
    }

    internal static Color Invert(Color color, System7ColorDepth depth)
    {
        if (depth is not (System7ColorDepth.Indexed2 or System7ColorDepth.Indexed4 or System7ColorDepth.Indexed8))
            return Color.FromRgb((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
        var data = depth == System7ColorDepth.Indexed2 ? Gray4 : depth == System7ColorDepth.Indexed4 ? Color16 : Color256;
        var mapped = Map(color, depth);
        var count = 1 << (int)depth;
        for (var i = 0; i < count; i++)
            if (data[i * 6] == mapped.R && data[i * 6 + 2] == mapped.G && data[i * 6 + 4] == mapped.B)
            {
                var index = (i ^ (count - 1)) * 6;
                return Color.FromRgb(data[index], data[index + 2], data[index + 4]);
            }
        throw new InvalidOperationException("The mapped color is missing from the native palette.");
    }

    internal static (int First, int Second, int Weight)[] ParseBlends(string recipes) => recipes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(recipe => recipe.Split(',').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
        .Select(recipe => (recipe[0], recipe[1], recipe[2])).ToArray();

    /// <summary>A color table of the given size: the colors mapped to the screen, then their blends from entry 16.</summary>
    internal static Color[] Build(Color[] colors, (int First, int Second, int Weight)[] blends, int size, System7ColorDepth depth)
    {
        var result = new Color[size];
        for (var i = 0; i < colors.Length; i++) result[i] = Map(colors[i], depth);
        for (var i = 0; i < blends.Length; i++) result[16 + i] = Blend(colors[blends[i].First], colors[blends[i].Second], blends[i].Weight, depth);
        return result;
    }

    /// <summary>Whether each entry from start to end differs from the one before it.</summary>
    internal static bool Distinct(Color[] colors, int start, int end)
    {
        for (var i = start + 1; i <= end; i++) if (colors[i] == colors[i - 1]) return false;
        return true;
    }

    private static Color Blend(Color first, Color second, int fraction, System7ColorDepth depth)
    {
        static int Component(byte first, byte second, int fraction)
        {
            var difference = (second - first) * 257;
            var step = (int)((long)Math.Abs(difference) * fraction * 0x1111 >> 16);
            return first * 257 + Math.Sign(difference) * step;
        }
        return Map(new Rgb16(Component(first.R, second.R, fraction), Component(first.G, second.G, fraction),
            Component(first.B, second.B, fraction)), depth).Color;
    }

    public static bool TryGetGray(Color foreground, Color background, System7ColorDepth depth, out Color gray)
    {
        // System 7 GetGray compares the mapped midpoint with both mapped endpoints.
        var front = Map(ToRgb16(foreground), depth);
        var back = Map(ToRgb16(background), depth);
        var midpoint = new Rgb16(Midpoint(front.Red, back.Red), Midpoint(front.Green, back.Green), Midpoint(front.Blue, back.Blue));
        var mapped = Map(midpoint, depth);
        var error = mapped.Distance(midpoint);
        var usable = error < (mapped.Distance(front) >> 1) && error < (mapped.Distance(back) >> 1);
        gray = usable ? mapped.Color : front.Color;
        return usable;
    }

    private static int Midpoint(int first, int second)
    {
        var value = (first + second) >> 1;
        return value < 0x8000 ? value + 2 : value;
    }

    private static Rgb16 ToRgb16(Color color) => new(color.R * 257, color.G * 257, color.B * 257);

    private static Rgb16 Map(Rgb16 color, System7ColorDepth depth)
    {
        var (red, green, blue) = color;
        if (depth is System7ColorDepth.Indexed2 or System7ColorDepth.Indexed4 or System7ColorDepth.Indexed8)
        {
            var data = depth == System7ColorDepth.Indexed2 ? Gray4 : depth == System7ColorDepth.Indexed4 ? Color16 : Color256;
            var entry = ((red >> 12) << 8) + ((green >> 12) << 4) + (blue >> 12);
            if (depth == System7ColorDepth.Indexed2)
            {
                // QuickDraw's grayscale search averages four times before its 256-entry lookup.
                entry = (red + green) >> 1;
                entry = (entry + blue) >> 1;
                entry = (entry + red) >> 1;
                entry = ((entry + green) >> 1) >> 8;
            }
            var index = data[(1 << (int)depth) * 6 + entry] * 6;
            return new Rgb16(Read(data, index), Read(data, index + 2), Read(data, index + 4));
        }
        if (depth == System7ColorDepth.Rgb555)
            return new Rgb16(Expand(red), Expand(green), Expand(blue));
        return new Rgb16((red >> 8) * 257, (green >> 8) * 257, (blue >> 8) * 257);
    }

    private static int Read(byte[] data, int index) => (data[index] << 8) | data[index + 1];
    private static int Expand(int value)
    {
        var bits = value >> 11;
        return (bits << 11) | (bits << 6) | (bits << 1) | (bits >> 4);
    }
}
