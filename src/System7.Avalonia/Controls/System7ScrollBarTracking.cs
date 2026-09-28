using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

/// <summary>
/// Tracks a CDEF 1 scroll bar: the part held down, the thumb's drag outline, and where the thumb sits, which CDEF 1 rounds
/// differently from a <see cref="Track"/>. It also resolves the bar's color scheme from its brushes and the theme's shades.
/// </summary>
internal sealed class System7ScrollBarTracking : IDisposable
{
    private static readonly System7Attachments<ScrollBar, System7ScrollBarTracking> Instances = new(owner => new System7ScrollBarTracking(owner));
    private static readonly string[] ShadeKeys =
    [
        "System7ScrollBarBodyLightBrush", "System7ScrollBarBodyDarkBrush", "System7ScrollBarThumbLightBrush", "System7ScrollBarThumbDarkBrush",
        "System7ScrollBarArrowLightBrush", "System7ScrollBarArrowDarkBrush", "System7ScrollBarTrackLightBrush", "System7ScrollBarTrackDarkBrush",
        "System7ScrollBarTintLightBrush", "System7ScrollBarTintDarkBrush",
    ];

    private readonly ScrollBar bar;
    private readonly DispatcherTimer repeat = new();
    private System7ScrollPart pressed;
    private Point pointer;
    private Point dragStart;
    private int thumbStart;
    private int? dragPosition;
    private bool dragOutside;
    private bool showDragOutline;
    private IPointer? capturedPointer;

    private bool Vertical => bar.Orientation == Orientation.Vertical;
    private int Length => (int)Math.Round(Vertical ? bar.Bounds.Height : bar.Bounds.Width);
    private int Breadth => (int)Math.Round(Vertical ? bar.Bounds.Width : bar.Bounds.Height);
    private bool Live => bar.IsEffectivelyEnabled && bar.Maximum > bar.Minimum;
    private int ThumbPosition => ThumbPositionFor(Length, Breadth, bar.Minimum, bar.Maximum, bar.Value);

    public static void SetEnabled(ScrollBar bar, bool enabled) => Instances.Set(bar, enabled);

    private System7ScrollBarTracking(ScrollBar bar)
    {
        this.bar = bar;
        repeat.Tick += (_, _) => Step();
        bar.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Bubble);
        bar.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Bubble);
        bar.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Bubble);
        bar.AddHandler(InputElement.PointerCaptureLostEvent, CaptureLost, RoutingStrategies.Bubble);
        bar.PropertyChanged += Changed;
        bar.ResourcesChanged += ResourcesChanged;
        bar.DetachedFromVisualTree += Detached;
        UpdateColors();
        Update();
    }

    public void Dispose()
    {
        StopTracking();
        bar.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        bar.RemoveHandler(InputElement.PointerMovedEvent, Moved);
        bar.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        bar.RemoveHandler(InputElement.PointerCaptureLostEvent, CaptureLost);
        bar.PropertyChanged -= Changed;
        bar.ResourcesChanged -= ResourcesChanged;
        bar.DetachedFromVisualTree -= Detached;
    }

    // CDEF 1 +$79c rounds upward only when the remainder exceeds half the divisor.
    public static int RoundPosition(double position) => (int)Math.Ceiling(position - 0.5);

    public static int ThumbPositionFor(int length, int thickness, double minimum, double maximum, double value) =>
        thickness + (maximum > minimum
            ? RoundPosition(Math.Max(0, length - 3 * thickness) * Math.Clamp((value - minimum) / (maximum - minimum), 0, 1))
            : 0);

    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsEffectivelyEnabledProperty && !bar.IsEffectivelyEnabled) capturedPointer?.Capture(null);
        if (e.Property == System7Theme.IsActiveProperty && !System7Theme.GetIsActive(bar)) capturedPointer?.Capture(null);
        if (e.Property == TemplatedControl.BorderBrushProperty || e.Property == TemplatedControl.BackgroundProperty
            || e.Property == System7Theme.ColorDepthProperty)
            UpdateColors();
        if (e.Property == ScrollBar.OrientationProperty || e.Property == RangeBase.MinimumProperty || e.Property == RangeBase.MaximumProperty
            || e.Property == RangeBase.ValueProperty || e.Property == InputElement.IsEffectivelyEnabledProperty || e.Property == Visual.BoundsProperty)
            Update();
    }

    private void ResourcesChanged(object? sender, ResourcesChangedEventArgs e) => UpdateColors();

    private void Detached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        capturedPointer?.Capture(null);
        StopTracking();
    }

    private void UpdateColors()
    {
        var colors = new Color[15];
        colors[0] = (bar.BorderBrush as ISolidColorBrush)?.Color ?? Colors.Black;
        colors[1] = (bar.Background as ISolidColorBrush)?.Color ?? Colors.White;
        for (var i = 0; i < ShadeKeys.Length; i++)
            if (bar.TryFindResource(ShadeKeys[i], out var shade) && shade is ISolidColorBrush brush) colors[i + 5] = brush.Color;
        var recipes = bar.TryFindResource("System7ScrollBarBlendRecipes", out var value) ? value as string ?? "" : "";
        var scheme = new System7ScrollBarScheme(colors, recipes, System7Theme.GetColorDepth(bar));
        if (!scheme.Equals(System7Theme.GetScrollBarScheme(bar))) bar.SetValue(System7Theme.ScrollBarSchemeProperty, scheme);
    }

    private void Update()
    {
        var highlight = HitTestPart(pointer) == pressed ? pressed : System7ScrollPart.None;
        var live = Live;
        var breadth = Breadth;
        var position = ThumbPosition;
        bar.SetValue(System7Theme.IsScrollLiveProperty, live);
        bar.SetValue(System7Theme.ScrollHighlightProperty, live ? highlight : System7ScrollPart.None);
        bar.SetValue(System7Theme.ScrollThumbMarginProperty, Vertical ? new Thickness(0, position, 0, 0) : new Thickness(position, 0, 0, 0));
        var dragging = live && System7Theme.GetIsActive(bar) && showDragOutline && dragPosition is not null;
        var ring = !dragging ? default : Vertical ? new Rect(1, dragPosition!.Value, breadth - 2, breadth) : new Rect(dragPosition!.Value, 1, breadth, breadth - 2);
        bar.SetValue(System7Theme.ScrollDragOutlineProperty, dragging ? System7Geometry.FrameRect(ring.X, ring.Y, ring.Width, ring.Height) : null);
    }

    private System7ScrollPart HitTestPart(Point point)
    {
        if (!bar.IsEffectivelyEnabled || !System7Theme.GetIsActive(bar) || bar.Maximum <= bar.Minimum || !Contains(new Rect(bar.Bounds.Size), point))
            return System7ScrollPart.None;
        var position = Vertical ? point.Y : point.X;
        var cross = Vertical ? point.X : point.Y;
        if (position <= Breadth) return System7ScrollPart.LineUp;
        if (Length - position <= Breadth) return System7ScrollPart.LineDown;
        if (position >= ThumbPosition && position < ThumbPosition + Breadth && cross >= 1 && cross < Breadth - 1)
            return System7ScrollPart.Thumb;
        return position < ThumbPosition + Breadth / 2 ? System7ScrollPart.PageUp : System7ScrollPart.PageDown;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed || capturedPointer != null) return;
        pointer = e.GetPosition(bar);
        pressed = HitTestPart(pointer);
        if (pressed == System7ScrollPart.None) return;
        e.PreventGestureRecognition();
        capturedPointer = e.Pointer;
        e.Pointer.Capture(bar);
        if (pressed == System7ScrollPart.Thumb)
        {
            dragStart = pointer;
            thumbStart = ThumbPosition;
            dragPosition = thumbStart;
            dragOutside = false;
            showDragOutline = true;
            bar.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent, Vector = default });
        }
        else
        {
            Step();
            var interval = bar.TryFindResource("System7ScrollRepeatInterval", out var value) && value is int ms ? ms : 16;
            repeat.Interval = TimeSpan.FromMilliseconds(Math.Max(1, interval));
            repeat.Start();
        }
        Update();
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != capturedPointer) return;
        pointer = e.GetPosition(bar);
        if (pressed == System7ScrollPart.Thumb)
        {
            var axis = Vertical ? pointer.Y - dragStart.Y : pointer.X - dragStart.X;
            var position = Math.Clamp(thumbStart + (int)Math.Round(axis), Breadth, Math.Max(Breadth, Length - 2 * Breadth));
            // CDEF 1 +$7ca and +$7ea set the drag limits across and along the scrollbar.
            var slop = Vertical ? new Thickness(24, 128 - Breadth) : new Thickness(128 - Breadth, 24);
            dragOutside = !Contains(new Rect(bar.Bounds.Size).Inflate(slop), pointer);
            if (dragOutside) showDragOutline = false;
            else if (position != dragPosition) showDragOutline = true;
            dragPosition = position;
        }
        Update();
        e.Handled = true;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != capturedPointer) return;
        if (pressed == System7ScrollPart.Thumb && !dragOutside && dragPosition is { } position)
        {
            var travel = Length - 3 * Breadth;
            var value = bar.Minimum + (travel <= 0 ? 0 : RoundPosition((position - Breadth) * (bar.Maximum - bar.Minimum) / travel));
            bar.SetCurrentValue(RangeBase.ValueProperty, Math.Clamp(value, bar.Minimum, bar.Maximum));
            bar.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = pointer - dragStart });
        }
        if (pressed == System7ScrollPart.Thumb)
            bar.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = pointer - dragStart });
        StopTracking();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer != capturedPointer) return;
        if (pressed == System7ScrollPart.Thumb)
            bar.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = default });
        StopTracking();
    }

    private void Step()
    {
        if (HitTestPart(pointer) != pressed) return;
        switch (pressed)
        {
            case System7ScrollPart.LineUp: bar.LineUp(); break;
            case System7ScrollPart.LineDown: bar.LineDown(); break;
            case System7ScrollPart.PageUp: bar.PageUp(); break;
            case System7ScrollPart.PageDown: bar.PageDown(); break;
        }
        Update();
    }

    private static bool Contains(Rect rect, Point point) =>
        point.X >= rect.X && point.Y >= rect.Y && point.X < rect.Right && point.Y < rect.Bottom;

    private void StopTracking()
    {
        repeat.Stop();
        pressed = System7ScrollPart.None;
        capturedPointer = null;
        dragPosition = null;
        Update();
    }
}
