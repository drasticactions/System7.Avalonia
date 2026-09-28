using Avalonia;

namespace System7.Avalonia.Controls;

public sealed class System7MemorySlider : System7TrackingSlider
{
    protected override void UpdateValue(Point point) => SetFraction(point.X - 10, Bounds.Width - 19);
}
