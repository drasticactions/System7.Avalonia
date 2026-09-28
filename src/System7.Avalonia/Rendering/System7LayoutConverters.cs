using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace System7.Avalonia.Rendering;

/// <summary>Converters that place parts on whole pixels as the Toolbox rounds them.</summary>
public static class System7LayoutConverters
{
    /// <summary>A top margin that centers a part as tall as the parameter in the bound height, leaving the odd pixel below it.</summary>
    public static IValueConverter CenterTop { get; } = new FuncValueConverter<double, object?, Thickness>((height, parameter) =>
        new Thickness(0, Math.Floor(((int)Math.Round(height) - int.Parse((string)parameter!, CultureInfo.InvariantCulture)) / 2d), 0, 0));

    /// <summary>(box width, pop-up tracking): a pop-up list at least as wide as its box, unless pop-up tracking sizes it as MDEF 0 does.</summary>
    public static IMultiValueConverter PopupMinWidth { get; } = new FuncMultiValueConverter<object?, double>(values =>
    {
        var list = values.ToList();
        return list is [Rect bounds, false] ? bounds.Width : 0;
    });
}
