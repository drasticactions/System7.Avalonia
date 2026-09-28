using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>Converters for the parts MDEF 0 draws in a menu item.</summary>
public static class System7MenuConverters
{
    private static readonly ConditionalWeakTable<System7Icon, Dictionary<System7MenuIconKind, System7MenuIcon>> Icons = new();

    /// <summary>(text style, font weight, font style): the item's QuickDraw style, adding bold and italic for the font.</summary>
    public static IMultiValueConverter TextStyle { get; } = new FuncMultiValueConverter<object?, System7TextStyle>(values =>
    {
        var list = values.ToList();
        var style = list.Count > 0 && list[0] is System7TextStyle s ? s : System7TextStyle.Plain;
        if (list.Count > 1 && list[1] is FontWeight and >= FontWeight.Bold) style |= System7TextStyle.Bold;
        if (list.Count > 2 && list[2] is FontStyle fontStyle && fontStyle != FontStyle.Normal) style |= System7TextStyle.Italic;
        return style;
    });

    /// <summary>(mark, checked): the item's mark character, or the check mark for a checked item without one.</summary>
    public static IMultiValueConverter Mark { get; } = new FuncMultiValueConverter<object?, string>(values =>
    {
        var list = values.ToList();
        var mark = list.Count > 0 && list[0] is char c ? c : '\0';
        return mark != '\0' ? mark.ToString() : list.Count > 1 && list[1] is true ? "\u0012" : "";
    });

    /// <summary>The command key and letter or digit of an item's gesture.</summary>
    public static IValueConverter Shortcut { get; } = new FuncValueConverter<KeyGesture?, string>(gesture => gesture?.Key switch
    {
        >= Key.A and <= Key.Z => "\u0011" + (char)('A' + gesture.Key - Key.A),
        >= Key.D0 and <= Key.D9 => "\u0011" + (char)('0' + gesture.Key - Key.D0),
        _ => "",
    });

    /// <summary>
    /// (icon, one-bit icon, kind, depth, enabled): the <see cref="System7MenuIconParts"/> of an item's <see cref="System7Icon"/>,
    /// taking the one-bit icon on a one-bit screen when there is one.
    /// </summary>
    public static IMultiValueConverter Icon { get; } = new FuncMultiValueConverter<object?, System7MenuIconParts?>(values =>
    {
        var list = values.ToList();
        if (list.Count < 5 || list[3] is not System7ColorDepth depth) return null;
        var source = depth == System7ColorDepth.Monochrome ? list[1] ?? list[0] : list[0];
        if (source is not System7Icon icon) return null;
        var kind = list[2] is System7MenuIconKind k ? k : System7MenuIconKind.Normal;
        var sizes = Icons.GetValue(icon, _ => []);
        if (!sizes.TryGetValue(kind, out var menuIcon)) sizes[kind] = menuIcon = new System7MenuIcon(icon, kind);
        return menuIcon.Parts(depth, list[4] is not false);
    });

    /// <summary>(icon parts, style): where the text of an item sits; beside an icon it is centered on a plain line.</summary>
    public static IMultiValueConverter TextMargin { get; } = new FuncMultiValueConverter<object?, Thickness>(values =>
    {
        var list = values.ToList();
        if (list.Count < 2 || list[0] is not System7MenuIconParts icon) return default;
        var style = list[1] is System7TextStyle s ? s : System7TextStyle.Plain;
        return new Thickness(0, (Math.Max(System7Font.GetLineHeight(style), (int)icon.CellHeight) - 16) / 2, 0, 0);
    });

    /// <summary>
    /// (item height, icon parts): where a disabled item's gray pattern falls on a one-bit screen. MDEF 0 grays the plain row height,
    /// so an item taller than 16 pixels without an icon grays the bottom of the item above it instead of its own.
    /// </summary>
    public static IMultiValueConverter GrayMargin { get; } = new FuncMultiValueConverter<object?, Thickness>(values =>
    {
        var list = values.ToList();
        if (list.Count < 2 || list[0] is not double height || list[1] is System7MenuIconParts) return default;
        var overlap = Math.Max(0, (int)Math.Round(height) - 16);
        return new Thickness(0, -overlap, 0, overlap);
    });

    /// <summary>
    /// (icon parts, submenu item count, gesture): the room right of an item's text, which MDEF 0 measures as ten pixels, less the
    /// space after an icon, and 24 more for a submenu arrow or command key.
    /// </summary>
    public static IMultiValueConverter RowMargin { get; } = new FuncMultiValueConverter<object?, Thickness>(values =>
    {
        var list = values.ToList();
        var right = 10 - (list.Count > 0 && list[0] is System7MenuIconParts ? System7Font.Measure(" ") : 0);
        var accessory = list.Count > 1 && list[1] is int and > 0
            || list.Count > 2 && Shortcut.Convert(list[2], typeof(string), null, System.Globalization.CultureInfo.InvariantCulture) is string { Length: > 0 };
        return new Thickness(0, 0, right + (accessory ? 24 : 0), 0);
    });

    /// <summary>Whether a title is the Apple menu's.</summary>
    public static IValueConverter IsApple { get; } = new FuncValueConverter<object?, bool>(header => header as string == "\u0014");

    /// <summary>(title width): where the color Apple icon sits in its title, a quarter of the way across.</summary>
    public static IValueConverter AppleMargin { get; } = new FuncValueConverter<double, Thickness>(width => new Thickness((int)Math.Round(width) / 4, 1, 0, 0));

    /// <summary>Whether an item has a submenu, which shows an arrow in place of a command key.</summary>
    public static IValueConverter HasSubmenu { get; } = new FuncValueConverter<int, bool>(count => count > 0);

    /// <summary>Whether an item has no submenu.</summary>
    public static IValueConverter HasNoSubmenu { get; } = new FuncValueConverter<int, bool>(count => count == 0);

    /// <summary>The check mark of a checked item.</summary>
    public static IValueConverter CheckMark { get; } = new FuncValueConverter<bool, string>(check => check ? "\u0012" : "");

    /// <summary>Whether an item is a separator line.</summary>
    public static IValueConverter IsSeparator { get; } = new FuncValueConverter<object?, bool>(header => header as string == "-");

    /// <summary>(icon, one-bit icon, kind, depth, enabled): whether a cicn falls back to its one-bit member, which keeps its bits when selected.</summary>
    public static IMultiValueConverter IsIconMember { get; } = new FuncMultiValueConverter<object?, bool>(values =>
        Icon.Convert(values.ToList(), typeof(object), null, System.Globalization.CultureInfo.InvariantCulture) is System7MenuIconParts { IsMember: true });
}
