using System7.Avalonia.Controls;

namespace System7.Avalonia.Rendering;

/// <summary>Hit testing for WDEF 0 and WDEF 1 window shapes.</summary>
public static class System7WindowGeometry
{
    public static bool ContainsPoint(int width, int height, int diameter, int x, int y, bool squareTop = false)
    {
        if ((uint)x >= width || (uint)y >= height) return false;
        var inset = squareTop && y < 8 ? 0 : System7Geometry.OvalInset(y, height, Math.Min(diameter, width) / 2d, Math.Min(diameter, height) / 2d);
        return x >= inset && x < width - inset;
    }

    /// <summary>Whether a point lies in a window's structure region, which leaves out the corners its drop shadow skips.</summary>
    public static bool ContainsPoint(System7WindowKind kind, int width, int height, int x, int y, int cornerDiameter = 16)
    {
        if (kind == System7WindowKind.RoundedDocument) return ContainsPoint(width, height, cornerDiameter, x, y);
        if ((uint)x >= width || (uint)y >= height) return false;
        var shadow = kind switch
        {
            System7WindowKind.Document => 1,
            System7WindowKind.ShadowDialog => 2,
            _ => 0
        };
        return !(x >= width - shadow && y < shadow) && !(x < shadow && y >= height - shadow);
    }

    public static System7WindowPart HitTestRounded(int width, int height, int diameter, int x, int y, bool active, bool canClose)
    {
        if (!ContainsPoint(width, height, diameter, x, y)) return System7WindowPart.None;
        if (ContainsPoint(width - 2, height - 20, diameter, x - 1, y - 19, squareTop: true)) return System7WindowPart.Content;
        if (x < 1 || x >= width - 1 || y >= height - 1) return System7WindowPart.None;
        if (active && canClose && x is >= 9 and < 20 && y is >= 5 and < 16) return System7WindowPart.Close;
        return System7WindowPart.TitleBar;
    }

    /// <summary>
    /// The part of a square window at a point, as WDEF 0 finds it: the width and height are the whole structure, drop shadow included.
    /// The close box, zoom box and grow box are found only while the window is active and has them.
    /// </summary>
    public static System7WindowPart HitTest(System7WindowKind kind, int width, int height, int x, int y, bool active, bool canClose, bool canZoom, bool canResize)
    {
        if (kind == System7WindowKind.RoundedDocument) return HitTestRounded(width, height, 16, x, y, active, canClose);
        if (!ContainsPoint(kind, width, height, x, y)) return System7WindowPart.None;
        var titled = kind is System7WindowKind.Document or System7WindowKind.MovableDialog;
        if (!titled) return System7WindowPart.Content;
        var shadow = kind == System7WindowKind.Document ? 1 : 0;
        if (x >= width - shadow || y >= height - shadow) return System7WindowPart.None;
        if (y < 19)
        {
            if (active && canClose && kind == System7WindowKind.Document && x is >= 9 and < 20 && y is >= 5 and < 16) return System7WindowPart.Close;
            if (active && canZoom && x >= width - 21 && x < width - 10 && y is >= 5 and < 16) return System7WindowPart.Zoom;
            return System7WindowPart.TitleBar;
        }
        if (active && canResize && kind == System7WindowKind.Document && x >= width - 16 && y >= height - 16 && x < width - 2 && y < height - 2)
            return System7WindowPart.Grow;
        return System7WindowPart.Content;
    }
}
