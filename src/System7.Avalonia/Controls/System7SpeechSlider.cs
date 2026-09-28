using Avalonia;

namespace System7.Avalonia.Controls;

public sealed class System7SpeechSlider : System7TrackingSlider
{
    private double grabOffset;

    protected override bool BeginDrag(Point point)
    {
        var thumb = 4 + Math.Round((Value - 20) * 100 / 380);
        if (point.X < thumb || point.X >= thumb + 15 || point.Y < 2 || point.Y >= 18) return false;
        grabOffset = point.X - thumb;
        return true;
    }

    protected override void UpdateValue(Point point)
    {
        var position = Math.Clamp(point.X - grabOffset - 4, 0, 100);
        Value = Math.Clamp(Math.Round(20 + position * 380 / 100), Minimum, Maximum);
    }
}
