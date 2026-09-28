using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>A menu item's <see cref="System7Icon"/>, decoded the way MDEF 0 plots it: shrunk for reduced items, then thresholded to one bit or mapped to the screen's colors.</summary>
internal sealed class System7MenuIcon
{
    private readonly System7Icon icon;
    public bool IsColor => icon.IsColor;
    public int Width { get; }
    public int Height { get; }

    public System7MenuIcon(System7Icon icon, System7MenuIconKind kind)
    {
        this.icon = icon;
        var halve = kind == System7MenuIconKind.Reduced || kind == System7MenuIconKind.Small && icon.IsColor;
        Width = Math.Max(1, halve ? icon.Width / 2 : icon.Width);
        Height = Math.Max(1, halve ? icon.Height / 2 : icon.Height);
    }

    private readonly Dictionary<(System7ColorDepth, bool), System7MenuIconParts> parts = [];

    /// <summary>The parts MDEF 0 draws for this icon on a screen of this depth.</summary>
    public System7MenuIconParts Parts(System7ColorDepth depth, bool enabled)
    {
        if (parts.TryGetValue((depth, enabled), out var result)) return result;
        var member = UsesColorIconMember(depth, enabled);
        var color = UsesColor(depth, enabled);
        var top = 1 + Math.Max(0, (15 - Height) / 2);
        return parts[(depth, enabled)] = new System7MenuIconParts(Width, Height, Height + 2, new global::Avalonia.Thickness(0, top, System7Font.Measure(" "), 0),
            color ? null : System7Drawings.Mask(Width, Height, (x, y) => Sample(member, false, x, y)),
            color ? null : System7Drawings.Mask(Width, Height, (x, y) => Sample(member, true, x, y)),
            color ? Image(depth, enabled) : null, member);
    }

    private IImage Image(System7ColorDepth depth, bool enabled)
    {
        var colors = new Color[Width * Height];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var (left, top, right, bottom) = Block(x, y);
                var index = 0;
                var solid = false;
                for (var sy = top; sy < bottom; sy++)
                    for (var sx = left; sx < right; sx++)
                    {
                        index = Math.Max(index, icon.Pixels[sy * icon.Width + sx]);
                        solid |= icon.Mask[sy * icon.Width + sx] != 0;
                    }
                colors[y * Width + x] = solid ? icon.Map(index, depth, !enabled) : Colors.Transparent;
            }
        return System7Drawings.Picture(Width, Height, colors);
    }

    /// <summary>Whether a thresholded pixel is opaque, or with <paramref name="dark"/> opaque and dark.</summary>
    private bool Sample(bool member, bool dark, int x, int y)
    {
        var (left, top, right, bottom) = Block(x, y);
        var index = 0;
        var solid = false;
        var black = false;
        for (var sy = top; sy < bottom; sy++)
            for (var sx = left; sx < right; sx++)
            {
                // A shrunk icon keeps the largest source index.
                var p = sy * icon.Width + sx;
                index = Math.Max(index, icon.Pixels[p]);
                if (member) black |= icon.MonochromePixels![p] != 0;
                solid |= icon.Mask[p] != 0;
            }
        if (!member)
        {
            var color = icon.Palette[index];
            black = color.R + color.G + color.B < 384;
            solid &= color.A != 0;
        }
        return solid && (!dark || black);
    }

    private (int Left, int Top, int Right, int Bottom) Block(int x, int y) =>
        (x * icon.Width / Width, y * icon.Height / Height, Math.Max(x * icon.Width / Width + 1, (x + 1) * icon.Width / Width),
            Math.Max(y * icon.Height / Height + 1, (y + 1) * icon.Height / Height));

    /// <summary>Whether this icon is drawn in its own colors on this device rather than thresholded.</summary>
    public bool UsesColor(System7ColorDepth depth, bool enabled) => IsColor && depth != System7ColorDepth.Monochrome && !UsesColorIconMember(depth, enabled);

    /// <summary>A cicn falls back to its one-bit member on a four-gray screen, and on sixteen colors while disabled.</summary>
    public bool UsesColorIconMember(System7ColorDepth depth, bool enabled) => IsColor
        && (depth == System7ColorDepth.Indexed2 || depth == System7ColorDepth.Indexed4 && !enabled);
}
