using Avalonia;
using Avalonia.Controls;

namespace System7.Demo;

/// <summary>Stacks its children into the fewest columns that fit the viewport height, tallest first, each to the shortest column.</summary>
public sealed class DemoColumns : Panel
{
    private readonly List<Rect> slots = [];

    public double Spacing { get; init; } = 10;
    public double MinColumnWidth { get; init; } = 240;

    public double ViewportHeight
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    } = double.PositiveInfinity;

    public int MaxColumns
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateMeasure();
        }
    } = int.MaxValue;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var most = double.IsInfinity(width) ? 1 : Math.Clamp((int)((width + Spacing) / (MinColumnWidth + Spacing)), 1, MaxColumns);
        var height = 0.0;
        for (var columns = 1; columns <= most; columns++)
        {
            height = Place(width, columns);
            if (height <= ViewportHeight) break;
        }
        return new Size(double.IsInfinity(width) ? slots.Select(slot => slot.Right).DefaultIfEmpty().Max() : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++) Children[i].Arrange(slots[i]);
        return finalSize;
    }

    private double Place(double width, int columns)
    {
        var columnWidth = double.IsInfinity(width) ? double.PositiveInfinity : Math.Floor((width - Spacing * (columns - 1)) / columns);
        var bottoms = new double[columns];
        foreach (var child in Children) child.Measure(new Size(columnWidth, double.PositiveInfinity));
        var order = columns == 1 ? Children.ToList() : Children.OrderByDescending(child => child.DesiredSize.Height).ToList();
        slots.Clear();
        slots.AddRange(Enumerable.Repeat(default(Rect), Children.Count));
        foreach (var child in order)
        {
            var column = Array.IndexOf(bottoms, bottoms.Min());
            var top = bottoms[column] == 0 ? 0 : bottoms[column] + Spacing;
            var slotWidth = double.IsInfinity(columnWidth) ? child.DesiredSize.Width : columnWidth;
            slots[Children.IndexOf(child)] = new Rect(column == 0 ? 0 : column * (columnWidth + Spacing), top, slotWidth, child.DesiredSize.Height);
            bottoms[column] = top + child.DesiredSize.Height;
        }
        return bottoms.Max();
    }
}
