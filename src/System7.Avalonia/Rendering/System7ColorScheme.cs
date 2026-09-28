using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace System7.Avalonia.Rendering;

/// <summary>
/// The colors a definition procedure draws from: its own colors, the blends of them it adds to its color table, and the screen depth.
/// <see cref="Palette"/> is null where the screen cannot show the shades and the part is drawn in black and white.
/// </summary>
public abstract class System7ColorScheme : IEquatable<System7ColorScheme>
{
    private readonly Lazy<Color[]?> palette;
    private readonly int variant;

    protected System7ColorScheme(Color[] sourceColors, (int First, int Second, int Weight)[] blends, System7ColorDepth depth, int variant)
    {
        SourceColors = sourceColors;
        Blends = blends;
        Depth = depth;
        this.variant = variant;
        palette = new(CreatePalette);
    }

    /// <summary>Converts a scheme to the brush for the color-table role given as the parameter.</summary>
    public static IValueConverter Role { get; } = new FuncValueConverter<System7ColorScheme?, object?, IBrush?>((scheme, role) =>
        scheme?.Palette is { } palette && role is Enum entry ? new ImmutableSolidColorBrush(palette[Convert.ToInt32(entry, CultureInfo.InvariantCulture)]) : null);

    /// <summary>Whether a scheme draws in its colors.</summary>
    public static IValueConverter HasPalette { get; } = new FuncValueConverter<System7ColorScheme?, bool>(scheme => scheme?.Palette != null);

    /// <summary>Whether a scheme is drawn in black and white.</summary>
    public static IValueConverter HasNoPalette { get; } = new FuncValueConverter<System7ColorScheme?, bool>(scheme => scheme?.Palette == null);

    protected Color[] SourceColors { get; }
    protected (int First, int Second, int Weight)[] Blends { get; }
    public System7ColorDepth Depth { get; }
    public Color[]? Palette => palette.Value;

    protected abstract Color[]? CreatePalette();

    public bool Equals(System7ColorScheme? other) => other != null && other.GetType() == GetType() && Depth == other.Depth && variant == other.variant
        && SourceColors.AsSpan().SequenceEqual(other.SourceColors) && Blends.AsSpan().SequenceEqual(other.Blends);

    public override bool Equals(object? obj) => Equals(obj as System7ColorScheme);
    public override int GetHashCode() => HashCode.Combine(GetType(), Depth, variant, SourceColors.Length);
}
