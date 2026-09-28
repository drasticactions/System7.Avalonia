using Avalonia.Media;
using System7.Avalonia.Controls;

namespace System7.Avalonia.Rendering;

/// <summary>The inputs a color WDEF 0 frame is drawn from: its colors, the screen depth, and its kind.</summary>
public sealed class System7WindowScheme(
    Color[] colors,
    string blendRecipes,
    System7ColorDepth depth,
    System7WindowKind kind)
    : System7ColorScheme(colors, System7ColorPalette.ParseBlends(blendRecipes), depth, (int)kind)
{
    public System7WindowKind Kind { get; } = kind;

    protected override Color[]? CreatePalette()
    {
        if (Depth == System7ColorDepth.Monochrome) return null;
        var result = System7ColorPalette.Build(SourceColors, Blends, 72, Depth);
        bool Distinct(int start, int end) => System7ColorPalette.Distinct(result, start, end);
        if (Kind != System7WindowKind.MovableDialog && (!Distinct(16, 20) || !Distinct(21, 23) || !Distinct(34, 35))) return null;
        if (Kind is System7WindowKind.Dialog or System7WindowKind.MovableDialog && !Distinct(24, 28)) return null;
        for (var i = 0; i < 16; i++)
        {
            // WDEF 0 supplies one black color-table entry; CopyBits fills the remaining 15 with a ramp between two window colors.
            result[40 + i] = System7ColorPalette.WindowBitmapColor(SourceColors[7], SourceColors[8], i, Depth);
            result[56 + i] = System7ColorPalette.WindowBitmapColor(SourceColors[8], SourceColors[11], i, Depth);
        }
        return result;
    }
}
