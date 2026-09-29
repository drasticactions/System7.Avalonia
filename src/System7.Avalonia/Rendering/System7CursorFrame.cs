namespace System7.Avalonia.Rendering;

/// <summary>One 16 by 16 CURS image: one-bit data and mask, and the hot spot.</summary>
public sealed class System7CursorFrame
{
    private readonly byte[] data;
    private readonly byte[] mask;

    internal System7CursorFrame(ReadOnlySpan<byte> record)
    {
        data = record[..32].ToArray();
        mask = record[32..64].ToArray();
        HotSpotY = (short)((record[64] << 8) | record[65]);
        HotSpotX = (short)((record[66] << 8) | record[67]);
    }

    /// <summary>The width and height of every cursor image.</summary>
    public const int Size = 16;

    public int HotSpotX { get; }

    public int HotSpotY { get; }

    /// <summary>Whether the data bit at (x, y) is set.</summary>
    public bool IsData(int x, int y) => Bit(data, x, y);

    /// <summary>Whether the mask bit at (x, y) is set.</summary>
    public bool IsMask(int x, int y) => Bit(mask, x, y);

    /// <summary>
    /// The pixel as premultiplied ARGB: black where data and mask are set, white where only the mask is set, transparent where neither is.
    /// A data bit outside the mask inverts the screen on a Macintosh; it is black here, since a composited cursor cannot invert.
    /// </summary>
    public uint Argb(int x, int y) => IsData(x, y) ? 0xFF000000u : IsMask(x, y) ? 0xFFFFFFFFu : 0u;

    private static bool Bit(byte[] bits, int x, int y) => (uint)x < Size && (uint)y < Size && (bits[y * 2 + x / 8] & (0x80 >> (x % 8))) != 0;
}
