using Avalonia;
using Avalonia.Controls.Primitives;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

/// <summary>Shows a <see cref="System7Icon"/> in its own colors, or a <see cref="System7IconFamily"/> as the screen's depth plots it.</summary>
public sealed partial class System7IconView : TemplatedControl
{
    [GeneratedStyledProperty]
    public partial object? Icon { get; set; }
}
