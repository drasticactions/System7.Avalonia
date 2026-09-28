using Avalonia.Data.Converters;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>The colors a color CDEF 1 scroll bar is drawn from.</summary>
public sealed class System7ScrollBarScheme : System7ColorScheme
{
    private readonly bool dragMask;
    private System7ScrollBarScheme? dragMaskScheme;

    public System7ScrollBarScheme(Color[] colors, string blendRecipes, System7ColorDepth depth)
        : base(colors, System7ColorPalette.ParseBlends(blendRecipes), depth, 0)
    {
    }

    private System7ScrollBarScheme(System7ScrollBarScheme source)
        : base(source.SourceColors, source.Blends, source.Depth, 1) => dragMask = true;

    /// <summary>The drag-outline variant of a scheme.</summary>
    public static IValueConverter DragMasked { get; } = new FuncValueConverter<System7ScrollBarScheme?, System7ScrollBarScheme?>(scheme => scheme?.DragMask());

    /// <summary>The same colors with every one that shows the tracking gray turned black, as the thumb's drag outline leaves them.</summary>
    public System7ScrollBarScheme DragMask() => dragMaskScheme ??= new(this);

    protected override Color[]? CreatePalette()
    {
        if ((int)Depth < 4) return null;
        var result = System7ColorPalette.Build(SourceColors, Blends, 38, Depth);
        // CDEF 1 +$862 requires distinct adjacent shades in all three groups.
        if (!System7ColorPalette.Distinct(result, 26, 30) || !System7ColorPalette.Distinct(result, 31, 33) || !System7ColorPalette.Distinct(result, 34, 37)) return null;
        if (dragMask)
        {
            var target = System7ColorPalette.Map(Color.FromRgb(221, 221, 221), Depth);
            for (var i = 0; i < result.Length; i++)
                if (result[i] == target) result[i] = Colors.Black;
        }
        return result;
    }
}
