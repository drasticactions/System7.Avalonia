using Avalonia;
using Avalonia.Controls.Primitives;

namespace System7.Avalonia.Controls;

/// <summary>The gray outline WDEF 0 and WDEF 1 XOR onto the screen while a window is dragged or resized.</summary>
public sealed partial class System7WindowOutline : TemplatedControl
{
    [GeneratedStyledProperty]
    public partial System7WindowKind Kind { get; set; }

    /// <summary>Whether this is the grow outline, with the title bar and scroll bar lines, rather than the drag outline.</summary>
    [GeneratedStyledProperty]
    public partial bool IsResize { get; set; }

    [GeneratedStyledProperty(DefaultValue = 16)]
    public partial int CornerDiameter { get; set; }

    public System7WindowOutline()
    {
        IsHitTestVisible = false;
        ZIndex = int.MaxValue;
    }
}
