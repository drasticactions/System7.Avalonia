using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>A decoded ICN#, icl4, and icl8 family, drawn as Icon Utilities plots it on the screen's depth.</summary>
public sealed class System7IconFamily
{
    /// <summary>The width and height of every member, 32 for an ICN# family or 16 for an ics# family.</summary>
    public int Size { get; }

    private System7Icon? monochromeIcon;

    /// <summary>The one-bit member with its mask, as a small icon list or a menu draws it in one colour.</summary>
    public System7Icon Monochrome => monochromeIcon ??= System7Icon.FromPixels(Size, monochrome, Mask);

    /// <summary>The ics#, ics4 and ics8 members that go with this family, when it has them.</summary>
    public System7IconFamily? Small { get; private init; }

    private readonly byte[] monochrome;
    private readonly byte[]? indexed4;
    private readonly byte[]? indexed8;
    internal byte[] Mask { get; }
    private static readonly byte[] Colors = System7ColorPalette.Load("IconFamilyColors.bin", 3264);

    private System7IconFamily(byte[] monochrome, byte[] mask, byte[]? indexed4, byte[]? indexed8, int size = 32)
    {
        Size = size;
        this.monochrome = monochrome;
        Mask = mask;
        this.indexed4 = indexed4;
        this.indexed8 = indexed8;
    }

    public static System7IconFamily FromResources(ReadOnlySpan<byte> iconAndMask, ReadOnlySpan<byte> color4 = default, ReadOnlySpan<byte> color8 = default)
    {
        if (iconAndMask.Length != 256 || color4.Length is not (0 or 512) || color8.Length is not (0 or 1024))
            throw new InvalidDataException("The ICN#, icl4, or icl8 resource has an invalid length.");
        return new(Unpack(iconAndMask[..128], 1), Unpack(iconAndMask[128..], 1),
            color4.IsEmpty ? null : Unpack(color4, 4), color8.IsEmpty ? null : color8.ToArray());
    }

    internal Color[] Decode(System7ColorDepth depth, bool selected)
    {
        // Icon Utilities rejects the selected color table on the 16-color device and draws the ICN# member.
        var pixels = depth == System7ColorDepth.Indexed4 && selected ? monochrome
            : (int)depth >= 8 ? indexed8 ?? indexed4 ?? monochrome : (int)depth >= 4 ? indexed4 ?? monochrome : monochrome;
        var result = new Color[Size * Size];
        var offset = ReferenceEquals(pixels, indexed8) ? (32 + (selected ? 256 : 0)) * 6 : (selected ? 16 : 0) * 6;
        for (var i = 0; i < result.Length; i++)
        {
            if (Mask[i] == 0) continue;
            if (ReferenceEquals(pixels, monochrome))
            {
                if (depth == System7ColorDepth.Monochrome)
                    result[i] = (pixels[i] != 0) != selected ? global::Avalonia.Media.Colors.Black : global::Avalonia.Media.Colors.White;
                else
                {
                    var component = pixels[i] != 0 ? 0 : selected ? 32767 : 65535;
                    result[i] = System7ColorPalette.ResourceIconColor(component, component, component, depth, false);
                }
            }
            else
            {
                var entry = offset + pixels[i] * 6;
                result[i] = System7ColorPalette.ResourceIconColor(Read(entry), Read(entry+2), Read(entry+4), depth, false);
            }
        }
        return result;
    }

    private readonly Dictionary<(System7ColorDepth, bool), IImage> images = [];

    /// <summary>The icon as Icon Utilities plots it on a device of this depth, decoded once per state.</summary>
    public IImage Image(System7ColorDepth depth, bool selected)
    {
        if (images.TryGetValue((depth, selected), out var image)) return image;
        return images[(depth, selected)] = System7Drawings.Picture(Size, Size, Decode(depth, selected));
    }

    /// <summary>
    /// Builds a family from 32 by 32 and 16 by 16 ARGB pixels, as a resource editor would: each member is dithered with Floyd-Steinberg
    /// error diffusion onto the standard icon color table through QuickDraw's inverse tables, and the mask is every pixel at least half opaque.
    /// Partly transparent pixels are composited over white first.
    /// </summary>
    public static System7IconFamily FromArgb(ReadOnlySpan<uint> pixels32, ReadOnlySpan<uint> pixels16)
    {
        if (pixels32.Length != 1024 || pixels16.Length is not (0 or 256))
            throw new ArgumentException("The icon needs 1024 pixels at 32 by 32 and none or 256 at 16 by 16.");
        var large = Quantize(pixels32, 32);
        return pixels16.IsEmpty ? large : new(large.monochrome, large.Mask, large.indexed4, large.indexed8) { Small = Quantize(pixels16, 16) };
    }

    private static System7IconFamily Quantize(ReadOnlySpan<uint> pixels, int size)
    {
        var mask = new byte[size * size];
        var red = new float[size * size];
        var green = new float[size * size];
        var blue = new float[size * size];
        for (var i = 0; i < pixels.Length; i++)
        {
            var argb = pixels[i];
            var alpha = (argb >> 24) / 255f;
            mask[i] = alpha >= 0.5f ? (byte)1 : (byte)0;
            red[i] = ((argb >> 16) & 0xff) * alpha + 255 * (1 - alpha);
            green[i] = ((argb >> 8) & 0xff) * alpha + 255 * (1 - alpha);
            blue[i] = (argb & 0xff) * alpha + 255 * (1 - alpha);
        }
        var monochrome = Dither(red, green, blue, mask, size, 2, System7ColorDepth.Monochrome, 0);
        var indexed4 = Dither(red, green, blue, mask, size, 16, System7ColorDepth.Indexed4, 0);
        var indexed8 = Dither(red, green, blue, mask, size, 256, System7ColorDepth.Indexed8, 32);
        return new(monochrome, mask, indexed4, indexed8, size);
    }

    private static byte[] Dither(float[] sourceRed, float[] sourceGreen, float[] sourceBlue, byte[] mask, int size, int count, System7ColorDepth depth, int table)
    {
        var red = (float[])sourceRed.Clone();
        var green = (float[])sourceGreen.Clone();
        var blue = (float[])sourceBlue.Clone();
        var result = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                if (mask[i] == 0) continue;
                var r = Math.Clamp(red[i], 0, 255);
                var g = Math.Clamp(green[i], 0, 255);
                var b = Math.Clamp(blue[i], 0, 255);
                int index;
                Color chosen;
                if (depth == System7ColorDepth.Monochrome)
                {
                    var black = 0.299f * r + 0.587f * g + 0.114f * b < 128;
                    index = black ? 1 : 0;
                    chosen = black ? global::Avalonia.Media.Colors.Black : global::Avalonia.Media.Colors.White;
                }
                else
                {
                    var mapped = System7ColorPalette.Map(Color.FromRgb((byte)r, (byte)g, (byte)b), depth);
                    index = Nearest(mapped, count, table);
                    chosen = Entry(table + index);
                }
                result[i] = (byte)index;
                Spread(red, r - chosen.R, x, y, size, mask);
                Spread(green, g - chosen.G, x, y, size, mask);
                Spread(blue, b - chosen.B, x, y, size, mask);
            }
        return result;
    }

    private static void Spread(float[] channel, float error, int x, int y, int size, byte[] mask)
    {
        void Add(int dx, int dy, float weight)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= size || ny >= size || mask[ny * size + nx] == 0) return;
            channel[ny * size + nx] += error * weight;
        }
        Add(1, 0, 7 / 16f);
        Add(-1, 1, 3 / 16f);
        Add(0, 1, 5 / 16f);
        Add(1, 1, 1 / 16f);
    }

    private static int Nearest(Color color, int count, int table)
    {
        var best = 0;
        var distance = int.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var entry = Entry(table + i);
            var d = Math.Abs(entry.R - color.R) + Math.Abs(entry.G - color.G) + Math.Abs(entry.B - color.B);
            if (d >= distance) continue;
            best = i;
            distance = d;
            if (d == 0) break;
        }
        return best;
    }

    private static Color Entry(int index) => Color.FromRgb((byte)(Read(index * 6) >> 8), (byte)(Read(index * 6 + 2) >> 8), (byte)(Read(index * 6 + 4) >> 8));

    /// <summary>Builds a family from its ICN#, icl4 and icl8 resources and the ics#, ics4 and ics8 resources of its small members.</summary>
    public static System7IconFamily FromResources(ReadOnlySpan<byte> iconAndMask, ReadOnlySpan<byte> color4, ReadOnlySpan<byte> color8,
        ReadOnlySpan<byte> smallIconAndMask, ReadOnlySpan<byte> smallColor4 = default, ReadOnlySpan<byte> smallColor8 = default)
    {
        var large = FromResources(iconAndMask, color4, color8);
        if (smallIconAndMask.Length != 64 || smallColor4.Length is not (0 or 128) || smallColor8.Length is not (0 or 256))
            throw new InvalidDataException("The ics#, ics4, or ics8 resource has an invalid length.");
        static byte[] Small(ReadOnlySpan<byte> bytes, int bits) => System7Icon.Unpack(bytes, 16, 16, 2 * bits, bits);
        var small = new System7IconFamily(Small(smallIconAndMask[..32], 1), Small(smallIconAndMask[32..], 1),
            smallColor4.IsEmpty ? null : Small(smallColor4, 4), smallColor8.IsEmpty ? null : smallColor8.ToArray(), 16);
        return new(large.monochrome, large.Mask, large.indexed4, large.indexed8) { Small = small };
    }

    private static readonly Lazy<Dictionary<string, System7IconFamily>> GenericIcons = new(LoadGeneric);

    /// <summary>
    /// One of the System's generic icons, as the Finder draws a file, folder, disk or application that has none of its own:
    /// document, folder, floppy, application, private-folder, trash, desk-accessory, stationery, trash-full, system-folder,
    /// apple-menu-folder, control-panels-folder, extensions-folder, preferences-folder, hard-disk and macintosh, and the alert icons stop, note and caution.
    /// </summary>
    public static System7IconFamily? Generic(string name) => GenericIcons.Value.GetValueOrDefault(name);

    private static Dictionary<string, System7IconFamily> LoadGeneric()
    {
        var data = System7ColorPalette.Load("GenericIcons.bin", -1);
        var result = new Dictionary<string, System7IconFamily>(StringComparer.Ordinal);
        var count = (data[0] << 8) | data[1];
        var offset = 2;
        ReadOnlySpan<byte> Take(int length)
        {
            var span = data.AsSpan(offset, length);
            offset += length;
            return span;
        }
        for (var i = 0; i < count; i++)
        {
            var name = System.Text.Encoding.ASCII.GetString(data, offset + 1, data[offset]);
            offset += 1 + data[offset];
            var flags = data[offset++];
            var icon = Take(256);
            var color4 = (flags & 1) != 0 ? Take(512) : default;
            var color8 = (flags & 2) != 0 ? Take(1024) : default;
            var small = (flags & 4) != 0 ? Take(64) : default;
            var small4 = (flags & 8) != 0 ? Take(128) : default;
            var small8 = (flags & 16) != 0 ? Take(256) : default;
            result[name] = small.IsEmpty ? FromResources(icon, color4, color8) : FromResources(icon, color4, color8, small, small4, small8);
        }
        return result;
    }

    private static int Read(int offset) => Colors[offset] * 256 + Colors[offset+1];

    private static byte[] Unpack(ReadOnlySpan<byte> bytes, int bits) => System7Icon.Unpack(bytes, 32, 32, 4 * bits, bits);
}
