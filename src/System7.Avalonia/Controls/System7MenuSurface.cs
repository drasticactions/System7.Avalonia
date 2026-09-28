using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace System7.Avalonia.Controls;

/// <summary>
/// MDEF 0's menu: its theme draws the frame, one-pixel shadow, and scroll arrows around its content, and scrolls the content up by
/// <see cref="ScrollOffset"/> inside a viewport <see cref="ViewportHeight"/> tall.
/// </summary>
public sealed partial class System7MenuSurface : ContentControl
{
    public static readonly DirectProperty<System7MenuSurface, double> ContentTopProperty =
        AvaloniaProperty.RegisterDirect<System7MenuSurface, double>(nameof(ContentTop), x => x.ContentTop);

    /// <summary>The height of the visible items; NaN shows them all.</summary>
    [GeneratedStyledProperty(DefaultValue = double.NaN)]
    public partial double ViewportHeight { get; set; }

    [GeneratedStyledProperty]
    public partial double ScrollOffset { get; set; }

    public static readonly StyledProperty<IBrush> SurfaceBackgroundProperty = AvaloniaProperty.Register<System7MenuSurface, IBrush>(nameof(SurfaceBackground), Brushes.White);
    public IBrush SurfaceBackground { get => GetValue(SurfaceBackgroundProperty); set => SetValue(SurfaceBackgroundProperty, value); }
    public static readonly StyledProperty<IBrush> ScrollIndicatorBrushProperty = AvaloniaProperty.Register<System7MenuSurface, IBrush>(nameof(ScrollIndicatorBrush), Brushes.Black);
    public IBrush ScrollIndicatorBrush { get => GetValue(ScrollIndicatorBrushProperty); set => SetValue(ScrollIndicatorBrushProperty, value); }
    /// <summary>Where the content's top sits in the viewport.</summary>
    public double ContentTop => -ScrollOffset;

    [GeneratedDirectProperty]
    public partial bool CanScrollUp { get; private set; }

    [GeneratedDirectProperty]
    public partial bool CanScrollDown { get; private set; }

    /// <summary>The full height of the content, before scrolling and clipping.</summary>
    public double ContentHeight => Presenter?.DesiredSize.Height ?? 0;
    internal bool OpensToLeft { get; set; }

    private Size measuredContent;

    static System7MenuSurface()
    {
        AffectsMeasure<System7MenuSurface>(ViewportHeightProperty);
        AffectsArrange<System7MenuSurface>(ScrollOffsetProperty);
    }

    public System7MenuSurface()
    {
        UseLayoutRounding = true;
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        // The content sits in a Canvas, which does not pass its size on, so the menu measures again when its items change size.
        LayoutUpdated += (_, _) =>
        {
            if (Presenter is { } presenter && presenter.DesiredSize != measuredContent) InvalidateMeasure();
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollOffsetProperty)
            RaisePropertyChanged(ContentTopProperty, -change.GetOldValue<double>(), ContentTop);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The frame takes a pixel on the left and top; the frame and shadow take two on the right and bottom.
        base.MeasureOverride(new Size(availableSize.Width, double.PositiveInfinity));
        var content = measuredContent = Presenter?.DesiredSize ?? default;
        return new Size(content.Width + 3, (double.IsNaN(ViewportHeight) ? content.Height : ViewportHeight) + 3);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        CanScrollUp = ScrollOffset > 0;
        CanScrollDown = ScrollOffset + finalSize.Height - 3 < ContentHeight;
        return size;
    }
}
