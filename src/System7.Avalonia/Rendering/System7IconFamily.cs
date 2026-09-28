using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>A decoded ICN#, icl4, and icl8 family, drawn as Icon Utilities plots it on the screen's depth.</summary>
public sealed class System7IconFamily
{
    private readonly byte[] monochrome;
    private readonly byte[]? indexed4;
    private readonly byte[]? indexed8;
    internal byte[] Mask { get; }
    private static readonly byte[] Colors = System7ColorPalette.Load("IconFamilyColors.bin", 3264);

    private System7IconFamily(byte[] monochrome, byte[] mask, byte[]? indexed4, byte[]? indexed8)
    {
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
        var result = new Color[1024];
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
        return images[(depth, selected)] = System7Drawings.Picture(32, 32, Decode(depth, selected));
    }

    private static int Read(int offset) => Colors[offset] * 256 + Colors[offset+1];

    private static byte[] Unpack(ReadOnlySpan<byte> bytes, int bits) => System7Icon.Unpack(bytes, 32, 32, 4 * bits, bits);
}
