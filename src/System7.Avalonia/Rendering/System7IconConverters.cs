using Avalonia.Data.Converters;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>Converters that draw decoded icons with stock <see cref="global::Avalonia.Controls.Image"/> and <see cref="global::Avalonia.Controls.Shapes.Path"/> elements.</summary>
public static class System7IconConverters
{
    /// <summary>
    /// (icon, depth): a <see cref="System7Icon"/> in its own colors, or a <see cref="System7IconFamily"/> as the screen plots it;
    /// with the parameter "Selected", as it plots a selected icon.
    /// </summary>
    public static IMultiValueConverter Image { get; } = new System7MultiConverter((list, parameter) =>
    {
        var depth = list.Count > 1 && list[1] is System7ColorDepth d ? d : System7ColorDepth.Monochrome;
        var selected = parameter as string == "Selected";
        return list.FirstOrDefault() switch
        {
            System7Icon icon => icon.Image,
            System7IconFamily family => family.Image(depth, selected),
            _ => null,
        };
    });

    /// <summary>The set pixels of a one-bit <see cref="System7Icon"/>, to fill in any color.</summary>
    public static IValueConverter Ink { get; } = new FuncValueConverter<System7Icon?, Geometry?>(icon => icon?.InkGeometry);
}
