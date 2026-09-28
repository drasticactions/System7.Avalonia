using Avalonia;

namespace System7.Avalonia.Controls;

public sealed class System7SoundSlider : System7TrackingSlider
{
    public System7SoundSlider() => Orientation = global::Avalonia.Layout.Orientation.Vertical;

    protected override void UpdateValue(Point point) => SetFraction(point.Y - 10, Bounds.Height - 20);
}
