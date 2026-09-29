using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

public sealed partial class System7WindowFrame : ContentControl
{
    public static readonly StyledProperty<IBrush> FrameBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(FrameBrush), Brushes.Black);
    public IBrush FrameBrush { get => GetValue(FrameBrushProperty); set => SetValue(FrameBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TitleBarLightBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TitleBarLightBrush), Brushes.White);
    public IBrush TitleBarLightBrush { get => GetValue(TitleBarLightBrushProperty); set => SetValue(TitleBarLightBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TitleBarDarkBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TitleBarDarkBrush), Brushes.Black);
    public IBrush TitleBarDarkBrush { get => GetValue(TitleBarDarkBrushProperty); set => SetValue(TitleBarDarkBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TitleStripeLightBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TitleStripeLightBrush), Brushes.White);
    public IBrush TitleStripeLightBrush { get => GetValue(TitleStripeLightBrushProperty); set => SetValue(TitleStripeLightBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TitleStripeDarkBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TitleStripeDarkBrush), Brushes.Black);
    public IBrush TitleStripeDarkBrush { get => GetValue(TitleStripeDarkBrushProperty); set => SetValue(TitleStripeDarkBrushProperty, value); }
    public static readonly StyledProperty<IBrush> DialogLightBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(DialogLightBrush), new SolidColorBrush(Color.Parse("#CCCCFF")));
    public IBrush DialogLightBrush { get => GetValue(DialogLightBrushProperty); set => SetValue(DialogLightBrushProperty, value); }
    public static readonly StyledProperty<IBrush> DialogDarkBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(DialogDarkBrush), Brushes.Black);
    public IBrush DialogDarkBrush { get => GetValue(DialogDarkBrushProperty); set => SetValue(DialogDarkBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TintLightBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TintLightBrush), new SolidColorBrush(Color.Parse("#CCCCFF")));
    public IBrush TintLightBrush { get => GetValue(TintLightBrushProperty); set => SetValue(TintLightBrushProperty, value); }
    public static readonly StyledProperty<IBrush> TintDarkBrushProperty = AvaloniaProperty.Register<System7WindowFrame, IBrush>(nameof(TintDarkBrush), new SolidColorBrush(Color.Parse("#333366")));
    public IBrush TintDarkBrush { get => GetValue(TintDarkBrushProperty); set => SetValue(TintDarkBrushProperty, value); }
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent = RoutedEvent.Register<System7WindowFrame, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> ZoomRequestedEvent = RoutedEvent.Register<System7WindowFrame, RoutedEventArgs>(nameof(ZoomRequested), RoutingStrategies.Bubble);

    [GeneratedStyledProperty]
    public partial System7WindowKind Kind { get; set; }

    [GeneratedStyledProperty(DefaultValue = 16, ValidateMethodName = nameof(IsValidCornerDiameter))]
    public partial int CornerDiameter { get; set; }

    [GeneratedStyledProperty(DefaultValue = "")]
    public partial string Title { get; set; }

    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool IsActive { get; set; }

    [GeneratedStyledProperty]
    public partial bool CanClose { get; set; }

    [GeneratedStyledProperty]
    public partial bool CanZoom { get; set; }

    [GeneratedStyledProperty]
    public partial bool CanResize { get; set; }

    /// <summary>Whether the frame draws only a Document window's title bar: its top border, the ends of the side borders, and the line under the title. The strip is 19 pixels high and has no drop shadow.</summary>
    [GeneratedStyledProperty]
    public partial bool IsTitleStrip { get; set; }

    [GeneratedDirectProperty]
    public partial bool HasTitleBar { get; private set; } = true;

    [GeneratedDirectProperty]
    public partial bool HasCloseButton { get; private set; }

    [GeneratedDirectProperty]
    public partial bool HasZoomButton { get; private set; }

    [GeneratedDirectProperty]
    public partial bool HasResizeGrip { get; private set; }

    /// <summary>Whether the close box is held down with the pointer inside it, so it draws highlighted.</summary>
    [GeneratedDirectProperty]
    public partial bool IsCloseBoxPressed { get; private set; }

    [GeneratedDirectProperty]
    public partial bool IsZoomBoxPressed { get; private set; }

    /// <summary>WDEF 0's colors on this screen.</summary>
    [GeneratedDirectProperty]
    public partial System7WindowScheme? Scheme { get; private set; }

    /// <summary>Whether WDEF 0 draws in its color roles; otherwise in one bit, on a black-and-white screen or one that cannot show its shades.</summary>
    [GeneratedDirectProperty]
    public partial bool IsColorFrame { get; private set; }

    /// <summary>The title's clip: WDEF 0 stops the title before the zoom box or, without one, as far from the right as it starts from the left.</summary>
    [GeneratedDirectProperty]
    public partial Thickness TitleClipMargin { get; private set; }

    [GeneratedDirectProperty]
    public partial Thickness TitleTextMargin { get; private set; }

    /// <summary>The stripes stop six pixels either side of the title.</summary>
    [GeneratedDirectProperty]
    public partial Thickness TitleGapMargin { get; private set; }

    [GeneratedDirectProperty]
    public partial bool HasTitleGap { get; private set; }

    [GeneratedDirectProperty]
    public partial Thickness RoundedTitleMargin { get; private set; }

    [GeneratedDirectProperty]
    public partial bool ShowGrowRow { get; private set; }

    [GeneratedDirectProperty]
    public partial bool ShowGrowColumn { get; private set; }

    public event EventHandler<RoutedEventArgs>? CloseRequested { add => AddHandler(CloseRequestedEvent, value); remove => RemoveHandler(CloseRequestedEvent, value); }
    public event EventHandler<RoutedEventArgs>? ZoomRequested { add => AddHandler(ZoomRequestedEvent, value); remove => RemoveHandler(ZoomRequestedEvent, value); }

    private Button? closeButton;
    private Button? zoomButton;

    private static bool IsValidCornerDiameter(int value) => value is 4 or 6 or 8 or 10 or 12 or 16 or 20 or 24;

    public System7WindowFrame() => ResourcesChanged += (_, _) => UpdateColors();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == CanCloseProperty || change.Property == CanZoomProperty || change.Property == CanResizeProperty
            || change.Property == IsTitleStripProperty)
        {
            HasTitleBar = Kind is System7WindowKind.Document or System7WindowKind.MovableDialog or System7WindowKind.RoundedDocument;
            HasCloseButton = CanClose && Kind is System7WindowKind.Document or System7WindowKind.RoundedDocument;
            HasZoomButton = CanZoom && Kind is System7WindowKind.Document or System7WindowKind.MovableDialog;
            HasResizeGrip = CanResize && Kind == System7WindowKind.Document && !IsTitleStrip;
            ClipToBounds = IsTitleStrip;
            InvalidateMeasure();
        }
        if (change.Property == FrameBrushProperty || change.Property == TitleBarLightBrushProperty || change.Property == TitleBarDarkBrushProperty
            || change.Property == TitleStripeLightBrushProperty || change.Property == TitleStripeDarkBrushProperty
            || change.Property == DialogLightBrushProperty || change.Property == DialogDarkBrushProperty || change.Property == TintLightBrushProperty
            || change.Property == TintDarkBrushProperty || change.Property == ForegroundProperty || change.Property == BackgroundProperty
            || change.Property == KindProperty || change.Property == System7Theme.ColorDepthProperty)
            UpdateColors();
        if (change.Property == KindProperty || change.Property == TitleProperty || change.Property == CanCloseProperty
            || change.Property == CanZoomProperty || change.Property == BoundsProperty || change.Property == IsTitleStripProperty)
            UpdateTitle();
    }

    private void UpdateColors()
    {
        var colors = new Color[13];
        void Take(int index, IBrush? brush) { if (brush is ISolidColorBrush solid) colors[index] = solid.Color; }
        Take(0, Background);
        Take(1, FrameBrush);
        Take(2, Foreground);
        Take(5, TitleStripeLightBrush);
        Take(6, TitleStripeDarkBrush);
        Take(7, TitleBarLightBrush);
        Take(8, TitleBarDarkBrush);
        Take(9, DialogLightBrush);
        Take(10, DialogDarkBrush);
        Take(11, TintLightBrush);
        Take(12, TintDarkBrush);
        var blends = this.TryFindResource("System7WindowBlendRecipes", out var recipes) ? recipes as string ?? "" : "";
        var next = new System7WindowScheme(colors, blends, System7Theme.GetColorDepth(this), Kind);
        if (!next.Equals(Scheme)) Scheme = next;
        IsColorFrame = Kind != System7WindowKind.RoundedDocument && Scheme!.Palette != null;
    }

    private void UpdateTitle()
    {
        var width = (int)Math.Round(Bounds.Width) + (IsTitleStrip ? 1 : 0);
        var height = (int)Math.Round(Bounds.Height) + (IsTitleStrip ? 1 : 0);
        var movable = Kind == System7WindowKind.MovableDialog;
        var canClose = HasCloseButton && !movable;
        var canZoom = HasZoomButton;
        var titleWidth = System7Font.Measure(Title);
        var left = Math.Max(canClose ? 32 : 2, (width - (movable ? 2 : 3) - titleWidth) / 2) + 1;
        var right = canZoom ? width - (movable ? 33 : 34) : width - (movable ? 1 : 2) - (left - 1);
        var gapLeft = Math.Clamp(titleWidth == 0 ? left : left - 6, 2, Math.Max(2, width - 3));
        var gapRight = Math.Clamp(titleWidth == 0 ? left : Math.Min(right, left + titleWidth) + 6, 2, Math.Max(2, width - 3));
        if (canZoom) gapRight = Math.Max(2, Math.Min(gapRight, width - 29));
        TitleClipMargin = new Thickness(1, 1, Math.Max(0, width - Math.Min(width - 2, right)), 0);
        TitleTextMargin = new Thickness(left - 1, 1, 0, 0);
        TitleGapMargin = new Thickness(gapLeft, 4, Math.Max(0, width - gapRight), 0);
        HasTitleGap = gapRight > gapLeft;
        var roundedLeft = (width - 1 - titleWidth) / 2;
        if (HasCloseButton) roundedLeft = Math.Max(28, roundedLeft);
        RoundedTitleMargin = new Thickness(roundedLeft, 2, 0, 0);
        ShowGrowRow = height >= 36;
        ShowGrowColumn = width >= 18;
    }

    // A title strip lays its template out as a Document window one pixel wider and one pixel taller, whose drop shadow the clip then removes.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsTitleStrip) return base.MeasureOverride(availableSize);
        foreach (var child in VisualChildren.OfType<Layoutable>()) child.Measure(new Size(availableSize.Width + 1, StripHeight + 1));
        return new Size(0, StripHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!IsTitleStrip) return base.ArrangeOverride(finalSize);
        foreach (var child in VisualChildren.OfType<Layoutable>()) child.Arrange(new Rect(0, 0, finalSize.Width + 1, StripHeight + 1));
        return finalSize;
    }

    /// <summary>The height of a title strip: the top border, the title bar, and the line under it.</summary>
    public const int StripHeight = 19;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (closeButton != null)
        {
            closeButton.Click -= CloseClick;
            closeButton.PropertyChanged -= BoxChanged;
        }
        if (zoomButton != null)
        {
            zoomButton.Click -= ZoomClick;
            zoomButton.PropertyChanged -= BoxChanged;
        }
        base.OnApplyTemplate(e);
        closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        zoomButton = e.NameScope.Find<Button>("PART_ZoomButton");
        if (closeButton != null)
        {
            closeButton.Click += CloseClick;
            closeButton.PropertyChanged += BoxChanged;
        }
        if (zoomButton != null)
        {
            zoomButton.Click += ZoomClick;
            zoomButton.PropertyChanged += BoxChanged;
        }
    }

    private void BoxChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Button.IsPressedProperty && e.Property != IsPointerOverProperty) return;
        IsCloseBoxPressed = closeButton is { IsPressed: true, IsPointerOver: true };
        IsZoomBoxPressed = zoomButton is { IsPressed: true, IsPointerOver: true };
    }

    private void CloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }

    private void ZoomClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ZoomRequestedEvent));
    }
}
