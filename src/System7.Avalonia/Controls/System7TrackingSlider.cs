using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace System7.Avalonia.Controls;

/// <summary>A control panel slider that tracks the pointer itself, as the panel's code does, instead of through its Track.</summary>
public abstract class System7TrackingSlider : Slider
{
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch) return;
        if (!BeginDrag(e.GetPosition(this))) return;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured == this) UpdateValue(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured != this) return;
        UpdateValue(e.GetPosition(this));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>Starts a drag at a pressed point, or returns false to ignore the press.</summary>
    protected virtual bool BeginDrag(Point point)
    {
        UpdateValue(point);
        return true;
    }

    protected abstract void UpdateValue(Point point);

    /// <summary>Sets the value the given distance along a travel of the given length, rounded to a whole step.</summary>
    protected void SetFraction(double offset, double travel)
    {
        if (travel <= 0 || Maximum <= Minimum) return;
        Value = Math.Round(Minimum + Math.Clamp(offset / travel, 0, 1) * (Maximum - Minimum));
    }
}
