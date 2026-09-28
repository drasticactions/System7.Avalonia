using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace System7.Avalonia.Controls;

internal sealed class System7ButtonTracking : IDisposable
{
    private static readonly System7Attachments<Button, System7ButtonTracking> Instances =
        new(owner => new System7ButtonTracking(owner));
    private readonly Button button;
    private IPointer? pointer;

    public static void SetEnabled(Button button, bool enabled) => Instances.Set(button, enabled);

    public void Dispose()
    {
        Stop();
        button.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        button.RemoveHandler(InputElement.PointerMovedEvent, Moved);
        button.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        button.RemoveHandler(InputElement.PointerCaptureLostEvent, CaptureLost);
        button.PropertyChanged -= Changed;
        button.DetachedFromVisualTree -= Detached;
    }

    private System7ButtonTracking(Button button)
    {
        this.button = button;
        button.AddHandler(InputElement.PointerPressedEvent, Pressed, 
            RoutingStrategies.Bubble, true);
        button.AddHandler(InputElement.PointerMovedEvent, Moved, 
            RoutingStrategies.Bubble, true);
        button.AddHandler(InputElement.PointerReleasedEvent, Released, 
            RoutingStrategies.Bubble, true);
        button.AddHandler(InputElement.PointerCaptureLostEvent, CaptureLost, 
            RoutingStrategies.Bubble, true);
        button.PropertyChanged += Changed;
        button.DetachedFromVisualTree += Detached;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (pointer != null || !button.IsPressed || 
            !button.IsEffectivelyEnabled ||
            !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
        e.PreventGestureRecognition();
        pointer = e.Pointer;
        pointer.Capture(button);
        Update(e.GetPosition(button));
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer == pointer) Update(e.GetPosition(button));
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer == pointer) Stop();
    }

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == pointer) Stop();
    }

    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Button.IsPressedProperty && !button.IsPressed
            || e.Property == InputElement.IsEffectivelyEnabledProperty && !button.IsEffectivelyEnabled
            || e.Property == Visual.IsVisibleProperty && !button.IsVisible) Stop();
    }

    private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();

    private void Update(Point point) => button.SetValue(System7Theme.IsButtonPointerWithinProperty,
        point is { X: >= 0, Y: >= 0 } && point.X < button.Bounds.Width && point.Y < button.Bounds.Height);

    private void Stop()
    {
        var captured = pointer;
        pointer = null;
        button.SetValue(System7Theme.IsButtonPointerWithinProperty, true);
        if (captured?.Captured == button) captured.Capture(null);
    }
}
