using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

public sealed partial class System7Desktop : Canvas
{
    public static readonly AttachedProperty<Size> ResizeMinimumProperty =
        AvaloniaProperty.RegisterAttached<System7Desktop, System7WindowFrame, Size>("ResizeMinimum", new Size(1, 1));
    public static Size GetResizeMinimum(System7WindowFrame frame) => frame.GetValue(ResizeMinimumProperty);
    public static void SetResizeMinimum(System7WindowFrame frame, Size value) => frame.SetValue(ResizeMinimumProperty, value);

    public static readonly AttachedProperty<Size> ResizeMaximumProperty =
        AvaloniaProperty.RegisterAttached<System7Desktop, System7WindowFrame, Size>("ResizeMaximum", new Size(32767, 32767));
    public static Size GetResizeMaximum(System7WindowFrame frame) => frame.GetValue(ResizeMaximumProperty);
    public static void SetResizeMaximum(System7WindowFrame frame, Size value) => frame.SetValue(ResizeMaximumProperty, value);

    [GeneratedStyledProperty]
    public partial Rect? WorkArea { get; set; }

    private sealed class ZoomState { public Rect Restore; public Rect Standard; }
    private readonly ConditionalWeakTable<System7WindowFrame, ZoomState> zoomStates = new();
    private readonly System7WindowOutline outline = new() { IsVisible = false };
    private System7WindowFrame? tracking;
    private IPointer? pointer;
    private Point start;
    private Rect original;
    private Rect proposed;
    private bool resizing;
    private bool valid;
    private Rect outlineBounds;
    private Rect AvailableArea => WorkArea ?? new Rect(Bounds.Size);

    public System7Desktop()
    {
        ClipToBounds = true;
        LogicalChildren.Add(outline);
        VisualChildren.Add(outline);
        AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        AddHandler(System7WindowFrame.ZoomRequestedEvent, Zoom);
        AddHandler(System7WindowFrame.CloseRequestedEvent, Close);
        Children.CollectionChanged += (_, _) =>
        {
            if (tracking != null && !Children.Contains(tracking)) Finish(false);
            var active = Children.OfType<System7WindowFrame>().Where(f => f is { IsVisible: true, IsActive: true }).LastOrDefault();
            active ??= Children.OfType<System7WindowFrame>().LastOrDefault(f => f.IsVisible);
            if (active != null) Activate(active);
        };
    }

    public void Activate(System7WindowFrame frame)
    {
        if (!Children.Contains(frame)) throw new ArgumentException("The window must belong to this desktop.", nameof(frame));
        var frames = Children.OfType<System7WindowFrame>().OrderBy(f => f.ZIndex).ToArray();
        var index = 1;
        foreach (var item in frames)
        {
            item.SetCurrentValue(System7WindowFrame.IsActiveProperty, item == frame);
            if (item != frame) item.ZIndex = index++;
        }
        frame.ZIndex = index;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        outline.Measure(outlineBounds.Size);
        outline.Arrange(outlineBounds);
        return finalSize;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || tracking != null) return;
        var source = e.Source as Visual;
        var frame = source as System7WindowFrame ?? source?.FindAncestorOfType<System7WindowFrame>();
        if (frame == null || !Children.Contains(frame) || !frame.IsEffectivelyEnabled) return;
        var local = e.GetPosition(frame);
        var size = frame.Bounds.Size;
        if (!System7WindowGeometry.ContainsPoint(frame.Kind, (int)size.Width, (int)size.Height, (int)Math.Floor(local.X), (int)Math.Floor(local.Y), frame.CornerDiameter))
        {
            e.Handled = true;
            return;
        }
        var title = frame.HasTitleBar && local.Y is >= 0 and < 19;
        if (frame.Kind == System7WindowKind.RoundedDocument)
        {
            var part = System7WindowGeometry.HitTestRounded((int)size.Width, (int)size.Height, frame.CornerDiameter,
                (int)Math.Floor(local.X), (int)Math.Floor(local.Y), frame.IsActive, frame.HasCloseButton);
            if (part == System7WindowPart.None) { e.Handled = true; return; }
            title = part is System7WindowPart.TitleBar or System7WindowPart.Close;
        }
        var active = frame.IsActive;
        var close = active && frame.HasCloseButton && new Rect(9, 5, 11, 11).Contains(local);
        var zoom = active && frame.HasZoomButton && new Rect(size.Width - 21, 5, 11, 11).Contains(local);
        var grow = active && frame.HasResizeGrip && new Rect(size.Width - 16, size.Height - 16, 14, 14).Contains(local);
        if (!title || (e.KeyModifiers & KeyModifiers.Meta) == 0) Activate(frame);
        if (close || zoom) return;
        if (!title && !grow)
        {
            if (!active) e.Handled = true;
            return;
        }
        e.Handled = true;
        e.PreventGestureRecognition();
        tracking = frame;
        pointer = e.Pointer;
        start = Round(e.GetPosition(this));
        original = frame.Bounds;
        proposed = original;
        resizing = grow;
        valid = true;
        outline.IsResize = grow;
        outline.Kind = frame.Kind;
        outline.CornerDiameter = frame.CornerDiameter;
        ShowOutline(original);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (tracking == null || e.Pointer != pointer) return;
        Update(Round(e.GetPosition(this)));
        e.Handled = true;
    }

    private void Update(Point position)
    {
        if (tracking == null) return;
        var delta = position - start;
        if (resizing)
        {
            var minimum = GetResizeMinimum(tracking);
            var maximum = GetResizeMaximum(tracking);
            var minWidth = Math.Max(5, Math.Max(tracking.MinWidth, minimum.Width + 3));
            var minHeight = Math.Max(21, Math.Max(tracking.MinHeight, minimum.Height + 21));
            // GrowWindow uses an inclusive minimum and an exclusive maximum for the client rectangle.
            var maxWidth = Math.Max(minWidth, Math.Min(tracking.MaxWidth, maximum.Width + 2));
            var maxHeight = Math.Max(minHeight, Math.Min(tracking.MaxHeight, maximum.Height + 20));
            proposed = new Rect(original.Position, new Size(Math.Clamp(original.Width + delta.X, minWidth, maxWidth),
                Math.Clamp(original.Height + delta.Y, minHeight, maxHeight)));
            valid = true;
        }
        else
        {
            var area = AvailableArea;
            valid = position.X >= area.Left && position.X < area.Right && position.Y >= area.Top + 4 && position.Y < area.Bottom;
            if (valid) proposed = original.Translate(delta);
        }
        ShowOutline(valid ? proposed : default);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (tracking == null || e.Pointer != pointer || e.InitialPressMouseButton != MouseButton.Left) return;
        Update(Round(e.GetPosition(this)));
        Finish(valid);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (tracking != null && e.Pointer == pointer) Finish(false);
    }

    private void Finish(bool commit)
    {
        var frame = tracking;
        var captured = pointer;
        tracking = null;
        pointer = null;
        ShowOutline(default);
        if (commit && frame is { IsEffectivelyEnabled: true, IsVisible: true }) SetBounds(frame, proposed);
        if (captured?.Captured == this) captured.Capture(null);
    }

    private void ShowOutline(Rect bounds)
    {
        // WDEF 0 draws no outline smaller than its title bar.
        outline.IsVisible = bounds is { Width: >= 3, Height: >= 21 };
        outlineBounds = bounds;
        InvalidateArrange();
    }

    private void Zoom(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not System7WindowFrame frame || !Children.Contains(frame)) return;
        var state = zoomStates.GetOrCreateValue(frame);
        if (frame.Bounds == state.Standard && state.Restore.Width > 0) SetBounds(frame, state.Restore);
        else
        {
            state.Restore = frame.Bounds;
            var area = AvailableArea;
            state.Standard = frame.Kind == System7WindowKind.MovableDialog
                ? new Rect(area.X + 2, area.Y + 6, Math.Max(17, area.Width - 4), Math.Max(32, area.Height - 8))
                : new Rect(area.X + 2, area.Y + 3, Math.Max(5, area.Width - 3), Math.Max(21, area.Height - 4));
            SetBounds(frame, state.Standard);
        }
        Activate(frame);
        e.Handled = true;
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not System7WindowFrame frame || !Children.Contains(frame)) return;
        frame.SetCurrentValue(IsVisibleProperty, false);
        frame.SetCurrentValue(System7WindowFrame.IsActiveProperty, false);
        var next = Children.OfType<System7WindowFrame>().Where(f => f.IsVisible).MaxBy(f => f.ZIndex);
        if (next != null) Activate(next);
        e.Handled = true;
    }

    private static void SetBounds(System7WindowFrame frame, Rect bounds)
    {
        frame.SetCurrentValue(LeftProperty, bounds.X);
        frame.SetCurrentValue(TopProperty, bounds.Y);
        frame.SetCurrentValue(WidthProperty, bounds.Width);
        frame.SetCurrentValue(HeightProperty, bounds.Height);
    }

    private static Point Round(Point point) => new(Math.Floor(point.X), Math.Floor(point.Y));
}
