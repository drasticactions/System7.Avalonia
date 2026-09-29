using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using System7.Avalonia.Controls;

namespace System7.Avalonia.Rendering;

/// <summary>
/// Paints a <see cref="Shape"/>'s fill, a <see cref="TextBlock"/>'s foreground, or a <see cref="Border"/>'s background in <see cref="InkProperty"/>
/// through a QuickDraw pattern whose phase follows the element's position on the screen. An <see cref="Image"/> gets a white bitmap of the
/// pattern the element's size, which inverts the pixels beneath where the image blends by difference, as QuickDraw's XOR pen does.
/// </summary>
public static class System7Pattern
{
    public static readonly AttachedProperty<System7PatternKind> KindProperty =
        AvaloniaProperty.RegisterAttached<Control, System7PatternKind>("Kind", typeof(System7Pattern));
    public static readonly AttachedProperty<bool> InverseProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Inverse", typeof(System7Pattern));
    public static readonly AttachedProperty<bool> LocalProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Local", typeof(System7Pattern));
    public static readonly AttachedProperty<IBrush?> InkProperty =
        AvaloniaProperty.RegisterAttached<Control, IBrush?>("Ink", typeof(System7Pattern));
    public static readonly AttachedProperty<PixelPoint> ScreenOffsetProperty =
        AvaloniaProperty.RegisterAttached<Control, PixelPoint>("ScreenOffset", typeof(System7Pattern), inherits: true);

    public static System7PatternKind GetKind(Control control) => control.GetValue(KindProperty);
    public static void SetKind(Control control, System7PatternKind value) => control.SetValue(KindProperty, value);
    /// <summary>Paints the pixels the pattern leaves clear instead of the ones it sets.</summary>
    public static bool GetInverse(Control control) => control.GetValue(InverseProperty);
    public static void SetInverse(Control control, bool value) => control.SetValue(InverseProperty, value);
    /// <summary>Aligns the pattern to the element instead of the screen.</summary>
    public static bool GetLocal(Control control) => control.GetValue(LocalProperty);
    public static void SetLocal(Control control, bool value) => control.SetValue(LocalProperty, value);
    public static IBrush? GetInk(Control control) => control.GetValue(InkProperty);
    public static void SetInk(Control control, IBrush? value) => control.SetValue(InkProperty, value);
    /// <summary>Where the top-left corner of the element's top level sits on the screen, in unscaled pixels. A host that shows each top level as its own surface sets it on the surface's root, so patterns keep one phase across surfaces. Inherited.</summary>
    public static PixelPoint GetScreenOffset(Control control) => control.GetValue(ScreenOffsetProperty);
    public static void SetScreenOffset(Control control, PixelPoint value) => control.SetValue(ScreenOffsetProperty, value);

    private static readonly ConditionalWeakTable<Control, Tracker> Trackers = new();
    private static readonly Dictionary<(Color, System7PatternKind, bool, int, int), IBrush> Brushes = [];

    static System7Pattern()
    {
        KindProperty.Changed.AddClassHandler<Control>((control, _) => Track(control).Refresh());
        InverseProperty.Changed.AddClassHandler<Control>((control, _) => Track(control).Refresh());
        LocalProperty.Changed.AddClassHandler<Control>((control, _) => Track(control).Refresh());
        InkProperty.Changed.AddClassHandler<Control>((control, _) => Track(control).Refresh());
        ScreenOffsetProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Trackers.TryGetValue(control, out var tracker)) tracker.Refresh();
        });
    }

    private static Tracker Track(Control control) => Trackers.GetValue(control, key => new Tracker(key));

    /// <summary>Whether the pattern sets the pixel at (x, y) of a tile whose local (0, 0) sits at screen (<paramref name="originX"/>, <paramref name="originY"/>).</summary>
    public static bool IsSet(System7PatternKind kind, bool inverse, int originX, int originY, int x, int y)
    {
        x += originX;
        y += originY;
        var set = kind switch
        {
            System7PatternKind.Gray => ((x + y) & 1) == 0,
            System7PatternKind.LightGray => ((x + 2 * y) & 3) == 0,
            _ => true,
        };
        return set != inverse;
    }

    /// <summary>A tiled brush that paints <paramref name="ink"/> on the pattern's pixels, with local (0, 0) at screen (x, y).</summary>
    public static IBrush Brush(IBrush? ink, System7PatternKind kind, bool inverse, int x, int y)
    {
        var color = (ink as ISolidColorBrush)?.Color ?? Colors.Black;
        if (kind == System7PatternKind.Solid) return ink ?? global::Avalonia.Media.Brushes.Black;
        x = ((x % 4) + 4) % 4;
        y = ((y % 4) + 4) % 4;
        if (Brushes.TryGetValue((color, kind, inverse, x, y), out var brush)) return brush;
        var pixels = new StreamGeometry();
        using (var context = pixels.Open())
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                {
                    if (!IsSet(kind, inverse, x, y, column, row)) continue;
                    context.BeginFigure(new Point(column, row), true);
                    context.LineTo(new Point(column + 1, row));
                    context.LineTo(new Point(column + 1, row + 1));
                    context.LineTo(new Point(column, row + 1));
                    context.EndFigure(true);
                }
        brush = new DrawingBrush(new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = global::Avalonia.Media.Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 4, 4)) },
                new GeometryDrawing { Brush = new ImmutableSolidColorBrush(color), Geometry = pixels },
            },
        })
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            DestinationRect = new RelativeRect(0, 0, 4, 4, RelativeUnit.Absolute),
        };
        return Brushes[(color, kind, inverse, x, y)] = brush;
    }

    /// <summary>A white bitmap of the pattern's pixels over a <paramref name="width"/> by <paramref name="height"/> area.</summary>
    public static Bitmap Tile(System7PatternKind kind, bool inverse, int x, int y, int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(Math.Max(1, width), Math.Max(1, height)), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        var row = new int[Math.Max(1, width)];
        for (var top = 0; top < height; top++)
        {
            for (var left = 0; left < width; left++) row[left] = IsSet(kind, inverse, x, y, left, top) ? -1 : 0;
            System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + top * buffer.RowBytes, width);
        }
        return bitmap;
    }

    /// <summary>The screen position that fixes an element's pattern phase, in unscaled pixels.</summary>
    public static (int X, int Y) ScreenOrigin(Visual visual)
    {
        var root = TopLevel.GetTopLevel(visual);
        if (root == null) return default;
        var zero = visual.TranslatePoint(default, root) ?? default;
        var xUnit = visual.TranslatePoint(new Point(1, 0), root) ?? new Point(1, 0);
        var yUnit = visual.TranslatePoint(new Point(0, 1), root) ?? new Point(0, 1);
        var scaleX = Math.Max(0.0001, Math.Sqrt(Math.Pow(xUnit.X - zero.X, 2) + Math.Pow(xUnit.Y - zero.Y, 2)));
        var scaleY = Math.Max(0.0001, Math.Sqrt(Math.Pow(yUnit.X - zero.X, 2) + Math.Pow(yUnit.Y - zero.Y, 2)));
        // QuickDraw patterns keep their native phase when the display is magnified.
        var offset = visual is Control control ? GetScreenOffset(control) : default;
        return ((int)Math.Round(zero.X / scaleX) + offset.X, (int)Math.Round(zero.Y / scaleY) + offset.Y);
    }

    private sealed class Tracker
    {
        private readonly WeakReference<Control> owner;
        private bool listening;
        private (int X, int Y) origin;
        private Size size;
        private System7WindowFrame? window;
        private Point windowPosition;
        private Point? localPosition;

        public Tracker(Control control)
        {
            owner = new WeakReference<Control>(control);
            control.AttachedToVisualTree += (_, _) => Refresh();
            control.DetachedFromVisualTree += (_, _) => Listen(false);
        }

        public void Refresh()
        {
            if (!owner.TryGetTarget(out var control)) return;
            var kind = GetKind(control);
            Listen(kind != System7PatternKind.Solid && !GetLocal(control) && control.IsAttachedToVisualTree());
            if (kind != System7PatternKind.Solid && !GetLocal(control)) origin = ScreenOrigin(control);
            ObserveWindow(control);
            Apply(control);
        }

        private void Listen(bool listen)
        {
            if (listen == listening || !owner.TryGetTarget(out var control)) return;
            listening = listen;
            if (listen) control.LayoutUpdated += LayoutUpdated;
            else control.LayoutUpdated -= LayoutUpdated;
        }

        private void LayoutUpdated(object? sender, EventArgs e)
        {
            if (!owner.TryGetTarget(out var control)) return;
            var next = ScreenOrigin(control);
            if (control is Image && control.Bounds.Size != size)
            {
                origin = next;
                Apply(control);
                return;
            }
            if (next == origin) return;
            var frame = NativeWindow(control);
            var local = frame == null ? null : control.TranslatePoint(default, frame);
            var moved = frame != null && frame == window && local == localPosition && frame.Bounds.Position != windowPosition;
            origin = next;
            ObserveWindow(control);
            // MoveWindow copies existing pixels; drawing a control again uses the new screen phase.
            if (!moved) Apply(control);
        }

        private void ObserveWindow(Control control)
        {
            window = NativeWindow(control);
            windowPosition = window?.Bounds.Position ?? default;
            localPosition = window == null ? null : control.TranslatePoint(default, window);
        }

        private static System7WindowFrame? NativeWindow(Control control) => control.GetVisualAncestors()
            .OfType<System7WindowFrame>().FirstOrDefault(frame => frame.Parent is System7Desktop);

        private void Apply(Control control)
        {
            var kind = GetKind(control);
            var (x, y) = GetLocal(control) ? default : origin;
            var brush = kind == System7PatternKind.Solid ? GetInk(control) : Brush(GetInk(control), kind, GetInverse(control), x, y);
            switch (control)
            {
                case Shape shape: shape.SetCurrentValue(Shape.FillProperty, brush); break;
                case TextBlock text: text.SetCurrentValue(TextBlock.ForegroundProperty, brush); break;
                case Border border: border.SetCurrentValue(Border.BackgroundProperty, brush); break;
                case Panel panel: panel.SetCurrentValue(Panel.BackgroundProperty, brush); break;
                case Image image:
                    size = image.Bounds.Size;
                    if (kind != System7PatternKind.Solid && size is { Width: >= 1, Height: >= 1 })
                        image.SetCurrentValue(Image.SourceProperty, Tile(kind, GetInverse(control), x, y, (int)Math.Round(size.Width), (int)Math.Round(size.Height)));
                    break;
            }
        }
    }
}
