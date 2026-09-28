using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>
/// What MDEF 0 draws for a menu item's icon: its cell, its thresholded mask and dark pixels, or its colors.
/// <see cref="IsMember"/> is set where a cicn falls back to its one-bit member, which keeps its bits when the item is selected.
/// </summary>
public sealed record System7MenuIconParts(double Width, double Height, double CellHeight, global::Avalonia.Thickness Margin,
    Geometry? Mask, Geometry? Ink, IImage? Image, bool IsMember);
