using System.Buffers.Binary;
using Avalonia;

namespace System7.Avalonia.Controls;

internal static class System7MenuAim
{
    private static readonly int[] Slopes = ReadSlopes();

    private static int[] ReadSlopes()
    {
        using var stream = typeof(System7MenuAim).Assembly.GetManifestResourceStream("System7.Avalonia.Assets.MenuSlopes.bin")!;
        var bytes = new byte[91 * 4];
        stream.ReadExactly(bytes);
        return Enumerable.Range(0, 91).Select(i => BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(i * 4))).ToArray();
    }

    internal static int Angle(int dx, int dy, bool color)
    {
        // The Plus FixRatio patch returns 1 for 0/0; the Mac II returns the positive saturation value.
        var ratio = dy == 0 ? dx == 0 && !color ? 65536 : dx < 0 ? int.MinValue : int.MaxValue
            : Math.Clamp(((long)dx << 16) / dy, int.MinValue, int.MaxValue);
        var target = Math.Abs(ratio) - 500;
        var index = 0;
        while (index < 90 && target > Slopes[index]) index++;
        return ratio < 0 ? index : 180 - index;
    }

    internal static bool Contains(Point anchor, Point point, Rect child, bool color)
    {
        var right = child.Left > anchor.X;
        if (right ? point.X < anchor.X : point.X > anchor.X) return false;
        var edge = right ? child.Left : child.Right;
        var direction = right ? 1 : -1;
        var angle = Angle((int)(point.X - anchor.X) * direction, (int)(point.Y - anchor.Y) * direction, color);
        var top = Angle((int)(edge - anchor.X) * direction, (int)(child.Top - anchor.Y) * direction, color);
        var bottom = Angle((int)(edge - anchor.X) * direction, (int)(child.Bottom - anchor.Y) * direction, color);
        return angle >= Math.Min(top, bottom) && angle <= Math.Max(top, bottom);
    }
}
