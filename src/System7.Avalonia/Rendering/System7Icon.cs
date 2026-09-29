using System.Buffers.Binary;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>A decoded ICON, ics#, or cicn resource. Menus, lists, and <see cref="Controls.System7IconView"/> draw it as vector drawings.</summary>
public sealed class System7Icon
{
    internal int Width { get; }
    internal int Height { get; }
    internal byte[] Pixels { get; }
    internal byte[] Mask { get; }
    internal Color[] Palette { get; }
    internal bool IsColor { get; }
    internal byte[]? MonochromePixels { get; }
    private readonly ushort[]? components;

    private System7Icon(int width, int height, byte[] pixels, byte[] mask, Color[] palette, bool color,
        ushort[]? components = null, byte[]? monochrome = null)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        Mask = mask;
        Palette = palette;
        IsColor = color;
        this.components = components;
        MonochromePixels = monochrome;
    }

    internal static System7Icon FromPixels(int size, byte[] pixels, byte[] mask) =>
        new(size, size, pixels, mask, [Colors.White, Colors.Black], false);

    public static System7Icon FromMonochrome(ReadOnlySpan<byte> resource, int size = 32)
    {
        if (size is not (16 or 32)) throw new ArgumentOutOfRangeException(nameof(size));
        if (resource.Length < size * size / 8) throw new InvalidDataException("The icon bitmap is incomplete.");
        var mask = new byte[size * size];
        Array.Fill(mask, (byte)1);
        return new System7Icon(size, size, Unpack(resource, size, size, size / 8, 1), mask, [Colors.White, Colors.Black], false);
    }

    public static System7Icon FromColorIcon(ReadOnlySpan<byte> resource)
    {
        if (resource.Length < 82) throw new InvalidDataException("The color icon header is incomplete.");
        var width = (short)Read(resource, 12) - (short)Read(resource, 8);
        var height = (short)Read(resource, 10) - (short)Read(resource, 6);
        var rowBytes = Read(resource, 4) & 0x3fff;
        var bits = Read(resource, 32);
        var maskStride = Read(resource, 54);
        var monoStride = Read(resource, 68);
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || bits is not (1 or 2 or 4 or 8)
            || rowBytes * 8 < width * bits || maskStride * 8 < width || monoStride * 8 < width)
            throw new InvalidDataException("The color icon has invalid indexed bitmap dimensions.");
        var table = checked(82 + (maskStride + monoStride) * height);
        if (resource.Length < table + 8) throw new InvalidDataException("The color icon bitmaps are incomplete.");
        var colors = Read(resource, table + 6) + 1;
        var offset = checked(table + 8 + colors * 8);
        if (colors > 256 || resource.Length < offset + rowBytes * height)
            throw new InvalidDataException("The color icon palette or pixels are incomplete.");
        var palette = new Color[256];
        var components = new ushort[256 * 3];
        for (var i = 0; i < colors; i++)
        {
            var entry = table + 8 + i * 8;
            var index = Read(resource, entry);
            if (index >= palette.Length) throw new InvalidDataException("The color icon palette index is invalid.");
            palette[index] = Color.FromRgb(resource[entry + 2], resource[entry + 4], resource[entry + 6]);
            for (var channel = 0; channel < 3; channel++) components[index * 3 + channel] = (ushort)Read(resource, entry + 2 + channel * 2);
        }
        return new System7Icon(width, height, Unpack(resource[offset..], width, height, rowBytes, bits),
            Unpack(resource[82..], width, height, maskStride, 1), palette, true, components,
            Unpack(resource[(82 + maskStride * height)..], width, height, monoStride, 1));
    }

    internal Color Map(int index, System7ColorDepth depth, bool disabled) => components == null ? Palette[index]
        : System7ColorPalette.ResourceIconColor(components[index * 3], components[index * 3 + 1], components[index * 3 + 2], depth, disabled);

    private static int Read(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    internal static byte[] Unpack(ReadOnlySpan<byte> data, int width, int height, int stride, int bits)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = (byte)((data[y * stride + x * bits / 8] >> (8 - bits - x * bits % 8)) & ((1 << bits) - 1));
        return pixels;
    }

    private Geometry? inkGeometry;
    private IImage? image;

    /// <summary>The set pixels of a 1-bit icon, for drawing in any color.</summary>
    public Geometry InkGeometry => inkGeometry ??= System7Drawings.Mask(Width, Height, (x, y) => Mask[y * Width + x] != 0 && Pixels[y * Width + x] != 0);

    /// <summary>The icon in its own colors.</summary>
    public IImage Image => image ??= System7Drawings.Picture(Width, Height,
        Pixels.Select((index, i) => Mask[i] != 0 ? Palette[index] : Colors.Transparent).ToArray());
}
