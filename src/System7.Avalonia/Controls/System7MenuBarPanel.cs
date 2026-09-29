using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace System7.Avalonia.Controls;

/// <summary>Lays menu titles out from the left, and titles whose <see cref="System7Theme.GetMenuBarDock"/> is right from the right edge, as the Application menu and the clock sit.</summary>
public sealed class System7MenuBarPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        var height = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Max(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var left = 0d;
        foreach (var child in Children)
        {
            if (Docked(child)) continue;
            child.Arrange(new Rect(left, 0, child.DesiredSize.Width, finalSize.Height));
            left += child.DesiredSize.Width;
        }
        var right = finalSize.Width;
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (!Docked(child)) continue;
            right -= child.DesiredSize.Width;
            child.Arrange(new Rect(Math.Max(left, right), 0, child.DesiredSize.Width, finalSize.Height));
        }
        return finalSize;
    }

    private static bool Docked(Control child) => child is MenuItem item && System7Theme.GetMenuBarDock(item) == HorizontalAlignment.Right;
}
