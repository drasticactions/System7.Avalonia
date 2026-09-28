using Avalonia;

namespace System7.Avalonia.Controls;

public sealed class System7NetworkSlider : System7TrackingSlider
{
    /// <summary>Where the Network control panel draws the thumb for each of its eleven settings.</summary>
    public static readonly int[] ThumbPositions = [4, 14, 22, 30, 38, 46, 54, 63, 71, 79, 85];
    public static readonly DirectProperty<System7NetworkSlider, double> ThumbPositionProperty =
        AvaloniaProperty.RegisterDirect<System7NetworkSlider, double>(nameof(ThumbPosition), x => x.ThumbPosition);

    /// <summary>The thumb's left edge for the current value.</summary>
    public double ThumbPosition => PositionFor(Value);

    private static double PositionFor(double value) => ThumbPositions[Math.Clamp((int)Math.Round(value), 0, ThumbPositions.Length - 1)];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            RaisePropertyChanged(ThumbPositionProperty, PositionFor(change.GetOldValue<double>()), ThumbPosition);
    }

    protected override void UpdateValue(Point point)
    {
        var positions = ThumbPositions;
        var best = 0;
        var distance = double.PositiveInfinity;
        for (var index = 0; index < positions.Length; index++)
        {
            var difference = Math.Abs(point.X - positions[index] - 5.5);
            if (difference >= distance) continue;
            best = index;
            distance = difference;
        }
        Value = best;
    }
}
