using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Primitives = Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace System7.Avalonia.Controls;

internal static class System7MenuPlacement
{
    internal static Popup? Popup(MenuItem item) => item.GetTemplateDescendants().OfType<Popup>().FirstOrDefault();
    internal static System7MenuSurface? Presenter(MenuItem item) => Popup(item)?.Child as System7MenuSurface;

    internal static void Prepare(MenuItem item, Menu menu, TopLevel root)
    {
        item.ApplyTemplate();
        if (Popup(item) is not { Child: System7MenuSurface presenter } popup) return;
        var origin = item.TranslatePoint(default, root);
        var unit = item.TranslatePoint(new Point(1, 1), root);
        if (origin == null || unit == null) return;
        var scaleX = Math.Max(0.001, unit.Value.X - origin.Value.X);
        var scaleY = Math.Max(0.001, unit.Value.Y - origin.Value.Y);
        var screenWidth = root.Bounds.Width / scaleX;
        var screenHeight = root.Bounds.Height / scaleY;
        presenter.Measure(Size.Infinity);
        var width = presenter.DesiredSize.Width - 3;
        if (width <= 0) return;
        var maximumHeight = Math.Max(16, screenHeight - 38);
        var height = 0d;
        foreach (var child in item.GetRealizedContainers())
        {
            child.Measure(Size.Infinity);
            if (height > 0 && height + child.DesiredSize.Height > maximumHeight) break;
            height += child.DesiredSize.Height;
        }
        if (height <= 0) height = Math.Min(presenter.ContentHeight, maximumHeight);
        presenter.ViewportHeight = height;
        var left = origin.Value.X / scaleX;
        var top = origin.Value.Y / scaleY;
        var barBottom = ((menu.TranslatePoint(new Point(0, menu.Bounds.Height), root) ?? default).Y / scaleY);
        var opensToLeft = false;
        if (item.IsTopLevel)
        {
            top = barBottom;
            if (left + width + 8 > screenWidth)
            {
                left = Math.Max(4, screenWidth - 8 - width);
                opensToLeft = true;
            }
        }
        else if (item.Parent is MenuItem parent && Presenter(parent) is { } parentPresenter)
        {
            var parentOrigin = parentPresenter.TranslatePoint(new Point(1, 1), root) ?? default;
            var parentLeft = parentOrigin.X / scaleX;
            var parentRight = parentLeft + parentPresenter.Bounds.Width - 3;
            top = Math.Max(top, barBottom + 7);
            if (top + height + 12 > screenHeight) top = screenHeight - 12 - height;
            opensToLeft = parentPresenter.OpensToLeft;
            if (!opensToLeft)
            {
                left = parentRight - 4;
                if (left + width + 8 > screenWidth)
                {
                    left = parentLeft - width + 8;
                    opensToLeft = true;
                    if (left - 8 < 0) { left = 8; opensToLeft = false; }
                }
            }
            else
            {
                left = parentLeft + 8 - width;
                if (left - 8 <= 0)
                {
                    left = parentRight - 4;
                    opensToLeft = false;
                    if (left + width + 8 > screenWidth) { left = screenWidth - width - 8; opensToLeft = true; }
                }
            }
        }
        presenter.OpensToLeft = opensToLeft;
        var anchor = (popup.PlacementTarget ?? popup.Parent as Visual ?? item).TranslatePoint(default, root) ?? origin.Value;
        Place(popup, (left - 1) * scaleX - anchor.X, (top - 1) * scaleY - anchor.Y);
    }

    /// <summary>Places a popup at an exact offset from its target's top left corner, where the Menu Manager puts it.</summary>
    internal static void Place(Popup popup, double horizontalOffset, double verticalOffset)
    {
        popup.Placement = PlacementMode.AnchorAndGravity;
        popup.PlacementAnchor = PopupAnchor.TopLeft;
        popup.PlacementGravity = PopupGravity.BottomRight;
        popup.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
        popup.PlacementRect = default;
        popup.HorizontalOffset = horizontalOffset;
        popup.VerticalOffset = verticalOffset;
    }

    /// <summary>Drops the placement and sizes tracking wrote into a popup, so it returns to the theme's own values.</summary>
    internal static void Reset(Popup? popup)
    {
        if (popup == null) return;
        foreach (var property in new AvaloniaProperty[] { Primitives.Popup.PlacementProperty, Primitives.Popup.PlacementAnchorProperty, Primitives.Popup.PlacementGravityProperty,
                     Primitives.Popup.PlacementConstraintAdjustmentProperty, Primitives.Popup.PlacementRectProperty, Primitives.Popup.HorizontalOffsetProperty, Primitives.Popup.VerticalOffsetProperty })
            popup.ClearValue(property);
        if (popup.Child is System7MenuSurface surface)
            foreach (var property in new AvaloniaProperty[] { Layoutable.WidthProperty, System7MenuSurface.ViewportHeightProperty, System7MenuSurface.ScrollOffsetProperty })
                surface.ClearValue(property);
    }
}
