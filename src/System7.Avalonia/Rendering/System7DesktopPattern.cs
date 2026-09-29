namespace System7.Avalonia.Rendering;

/// <summary>
/// A desktop pattern: the eight-byte one-bit pattern a black-and-white screen shows, and the color pixmap a ppat carries for a color screen.
/// The set is the System's desktop pattern, the 38 General Controls patterns of System 7.0.1, and the ppats of System 7.5's Desktop Patterns.
/// </summary>
public sealed class System7DesktopPattern
{
    private static readonly IReadOnlyList<System7DesktopPattern> Loaded = Load();
    private readonly byte[] bits;
    private readonly uint[]? palette;
    private readonly byte[]? indices;

    private System7DesktopPattern(string name, byte[] bits, int width, int height, uint[]? palette, byte[]? indices)
    {
        Name = name;
        this.bits = bits;
        Width = width;
        Height = height;
        this.palette = palette;
        this.indices = indices;
    }

    /// <summary>Every pattern, with the System's desktop pattern first.</summary>
    public static IReadOnlyList<System7DesktopPattern> All => Loaded;

    /// <summary>The System's own desktop pattern, the one a new System 7 installation shows.</summary>
    public static System7DesktopPattern Default => Loaded[0];

    public string Name { get; }

    /// <summary>Whether the pattern has a color pixmap.</summary>
    public bool HasColor => palette != null;

    /// <summary>The width of the color pixmap, or 8 without one.</summary>
    public int Width { get; }

    /// <summary>The height of the color pixmap, or 8 without one.</summary>
    public int Height { get; }

    /// <summary>The eight rows of the one-bit pattern, most significant bit leftmost.</summary>
    public ReadOnlySpan<byte> Bits => bits;

    /// <summary>Whether the one-bit pattern paints black at (x, y), repeating every 8 pixels.</summary>
    public bool IsSet(int x, int y) => (bits[((y % 8) + 8) % 8] & (0x80 >> (((x % 8) + 8) % 8))) != 0;

    /// <summary>The color pixmap's ARGB at (x, y), repeating over its size, or the one-bit pattern in black and white without one.</summary>
    public uint Argb(int x, int y)
    {
        if (palette == null || indices == null) return IsSet(x, y) ? 0xFF000000u : 0xFFFFFFFFu;
        x = ((x % Width) + Width) % Width;
        y = ((y % Height) + Height) % Height;
        return palette[indices[y * Width + x]];
    }

    public static System7DesktopPattern? Find(string name)
    {
        foreach (var pattern in Loaded)
            if (string.Equals(pattern.Name, name, StringComparison.OrdinalIgnoreCase)) return pattern;
        return null;
    }

    private static IReadOnlyList<System7DesktopPattern> Load()
    {
        using var stream = typeof(System7DesktopPattern).Assembly.GetManifestResourceStream("System7.Avalonia.Assets.DesktopPatterns.bin")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();
        int Read16(int at) => (data[at] << 8) | data[at + 1];
        var count = Read16(0);
        var offset = 2;
        var result = new System7DesktopPattern[count];
        for (var i = 0; i < count; i++)
        {
            var name = System.Text.Encoding.ASCII.GetString(data, offset + 1, data[offset]);
            offset += 1 + data[offset];
            var bits = data.AsSpan(offset, 8).ToArray();
            offset += 8;
            var width = Read16(offset);
            var height = Read16(offset + 2);
            offset += 4;
            if (width == 0 || height == 0)
            {
                result[i] = new System7DesktopPattern(name, bits, 8, 8, null, null);
                continue;
            }
            var colors = new uint[Read16(offset)];
            offset += 2;
            for (var c = 0; c < colors.Length; c++, offset += 4)
                colors[c] = (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
            var indices = data.AsSpan(offset, width * height).ToArray();
            offset += width * height;
            result[i] = new System7DesktopPattern(name, bits, width, height, colors, indices);
        }
        return result;
    }
}
