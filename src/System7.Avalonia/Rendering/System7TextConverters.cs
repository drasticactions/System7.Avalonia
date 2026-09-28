using System.Globalization;
using Avalonia.Data.Converters;

namespace System7.Avalonia.Rendering;

/// <summary>Converters that fit Chicago 12 text into a width as the Toolbox does.</summary>
public static class System7TextConverters
{
    /// <summary>(text, width): the text fitted to the width, less the parameter.</summary>
    private static IMultiValueConverter Fit(Func<string, int, string> fit) => new System7MultiConverter((list, parameter) =>
        list.Count == 2 ? fit(Text(list[0]), Width(list[1], parameter)) : Text(list.FirstOrDefault()));

    private static string Text(object? value) => value as string ?? "";

    private static int Width(object? value, object? parameter) =>
        Math.Max(0, (int)Math.Round(value is double width ? width : 0) - (parameter is string inset ? int.Parse(inset, CultureInfo.InvariantCulture) : 0));

    /// <summary>Whether content is a string, which the theme draws as Chicago text.</summary>
    public static IValueConverter IsText { get; } = new FuncValueConverter<object?, bool>(content => content is string);

    /// <summary>Whether content is something other than a string, which the theme presents as it is.</summary>
    public static IValueConverter IsCustom { get; } = new FuncValueConverter<object?, bool>(content => content is not null and not string);

    /// <summary>(text, width): the text, truncated with an ellipsis when it does not fit. The parameter is subtracted from the width.</summary>
    public static IMultiValueConverter Label { get; } = Fit((text, width) => System7Font.Truncate(text, width));

    /// <summary>(item, placeholder, width): a pop-up's current item as CDEF 63 shows it in its box, or the placeholder without one.</summary>
    public static IMultiValueConverter PopupLabel { get; } = new System7MultiConverter((list, parameter) =>
        list.Count == 3 ? System7Font.Truncate(list[0] as string ?? (list[0] == null ? list[1] as string ?? "" : ""), Width(list[2], parameter)) : "");

    /// <summary>The text of a pop-up item, or of its container's content.</summary>
    internal static string ItemText(object? item) => (item is global::Avalonia.Controls.ContentControl container ? container.Content : item) as string ?? "";

    /// <summary>The width of a pop-up's menu: its widest item's text and MDEF 0's margins.</summary>
    internal static int MenuWidth(System.Collections.IEnumerable items) => items.Cast<object?>().Select(item => System7Font.Measure(ItemText(item)) + 24).DefaultIfEmpty(24).Max();

    /// <summary>(items, item count): the width of a pop-up's box, from its menu's widest item.</summary>
    public static IMultiValueConverter PopupWidth { get; } = new FuncMultiValueConverter<object?, double>(values =>
    {
        var list = values.ToList();
        return (list.Count > 0 && list[0] is System.Collections.IEnumerable items ? MenuWidth(items) : 24) + 17d;
    });

    /// <summary>Whether a pop-up's current item is enabled: an item control can be disabled.</summary>
    public static IValueConverter IsItemEnabled { get; } = new FuncValueConverter<object?, bool>(item => item is not global::Avalonia.Controls.Control { IsEffectivelyEnabled: false });

    /// <summary>(text, width): -1 when the text only fits condensed, as LDEF 0 draws a cell too narrow for it; otherwise 0. The parameter is subtracted from the width.</summary>
    public static IMultiValueConverter Condense { get; } = new System7MultiConverter((list, parameter) =>
        list.Count == 2 && System7Font.Measure(Text(list[0])) > Width(list[1], parameter) ? -1d : 0d);

    /// <summary>(text, width): the text, condensed and then truncated with an ellipsis when it does not fit, as LDEF 0 draws it.</summary>
    public static IMultiValueConverter CondensedLabel { get; } = Fit((text, width) =>
        System7Font.Measure(text) <= width || System7Font.Measure(text, -1) <= width ? text : System7Font.Truncate(text, width, -1));

    /// <summary>(text, width): the text shortened from the middle with an ellipsis until it fits, as the Finder labels icons.</summary>
    public static IMultiValueConverter MiddleLabel { get; } = Fit(System7Font.TruncateMiddle);

    /// <summary>(text, width): a left margin that centers the text's advance, leaving the odd pixel on the right.</summary>
    public static IMultiValueConverter CenterMargin { get; } = new System7MultiConverter((list, parameter) =>
        list.Count == 2 ? new global::Avalonia.Thickness((Width(list[1], parameter) - System7Font.Measure(Text(list[0]))) >> 1, 0, 0, 0) : default);

    /// <summary>(text, style, width): the text, truncated with an ellipsis when its styled width does not fit.</summary>
    public static IMultiValueConverter StyledLabel { get; } = new System7MultiConverter((list, parameter) =>
    {
        if (list.Count != 3 || list[1] is not System7TextStyle style) return Text(list.FirstOrDefault());
        var text = Text(list[0]);
        var width = Width(list[2], parameter);
        if (System7Font.Measure(text, style) <= width) return text;
        while (text.Length > 0 && System7Font.Measure(text + "…", style) > width) text = text[..^1];
        return text + "…";
    });

    /// <summary>(text, style): QuickDraw's styled glyphs for a line whose top is at y = 0 and whose pen starts at x = 0.</summary>
    public static IMultiValueConverter StyledGlyphs { get; } = new System7MultiConverter((list, _) =>
    {
        if (list.Count != 2 || list[1] is not System7TextStyle style) return null;
        var glyphs = System7Font.Build(Text(list[0]), style);
        glyphs.Transform = new global::Avalonia.Media.TranslateTransform(0, System7Font.Ascent);
        return glyphs;
    });

    /// <summary>(text, style): the pen advance of a styled line.</summary>
    public static IMultiValueConverter StyledWidth { get; } = new System7MultiConverter((list, _) =>
        list is [_, System7TextStyle style] ? (double)System7Font.Measure(Text(list[0]), style) : 0d);

    /// <summary>(text, style): the line height of a styled line.</summary>
    public static IMultiValueConverter StyledHeight { get; } = new System7MultiConverter((list, _) =>
        list is [_, System7TextStyle style] ? (double)System7Font.GetLineHeight(style) : System7Font.LineHeight);
}
