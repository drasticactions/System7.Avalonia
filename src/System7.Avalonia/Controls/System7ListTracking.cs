using System.Runtime.CompilerServices;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace System7.Avalonia.Controls;

internal sealed class System7ListTracking
{
    private readonly ListBox list;
    private static readonly ConditionalWeakTable<ListBox, System7ListTracking> Instances = new();
    private IPointer? pointer;
    private int anchor;
    private int previous = -1;
    private bool range;
    private bool toggle;
    private bool selectSense;
    private int first;
    private int[] original = [];
    private int columns = 1;
    private readonly DispatcherTimer autoScroll = new();
    private readonly Stopwatch scrollElapsed = new();
    private ScrollViewer? scroll;
    private ScrollContentPresenter? viewport;
    private Point position;

    private System7ListTracking(ListBox list)
    {
        this.list = list;
        autoScroll.Tick += (_, _) =>
        {
            autoScroll.Stop();
            SelectAtPosition();
            ScrollOutside();
        };
        list.DetachedFromVisualTree += (_, _) => Stop();
    }

    public static void Initialize()
    {
        InputElement.PointerPressedEvent.AddClassHandler<ListBox>((list, e) =>
        {
            if (System7Theme.GetUseNativeListTracking(list)) Instances.GetValue(list, box => new(box)).Pressed(e);
        }, RoutingStrategies.Tunnel);
        InputElement.PointerMovedEvent.AddClassHandler<ListBox>((list, e) =>
        {
            if (Instances.TryGetValue(list, out var tracker)) tracker.Moved(e);
        }, RoutingStrategies.Tunnel);
        InputElement.PointerReleasedEvent.AddClassHandler<ListBox>((list, e) =>
        {
            if (Instances.TryGetValue(list, out var tracker)) tracker.Released(e);
        }, RoutingStrategies.Tunnel);
        InputElement.PointerCaptureLostEvent.AddClassHandler<ListBox>((list, _) =>
        {
            if (Instances.TryGetValue(list, out var tracker)) tracker.Stop();
        });
        foreach (var property in new AvaloniaProperty<bool>[] { System7Theme.IsActiveProperty, System7Theme.UseNativeListTrackingProperty, InputElement.IsEffectivelyEnabledProperty })
            property.Changed.AddClassHandler<ListBox>((list, change) =>
            {
                if (!change.GetNewValue<bool>() && Instances.TryGetValue(list, out var tracker)) tracker.Stop();
            });
    }

    private ListBoxItem? ItemAt(Point point, bool clamp)
    {
        if (viewport == null || list.TranslatePoint(point, viewport) is not { } local) return null;
        if (clamp)
            local = new Point(Math.Clamp(local.X, 0, Math.Max(0, viewport.Bounds.Width - 1)),
                Math.Clamp(local.Y, 0, Math.Max(0, viewport.Bounds.Height - 1)));
        else if (!new Rect(viewport.Bounds.Size).Contains(local)) return null;
        return list.GetRealizedContainers().OfType<ListBoxItem>()
            .FirstOrDefault(item => item.IsEffectivelyEnabled && viewport.TranslatePoint(local, item) is { } p &&
                new Rect(item.Bounds.Size).Contains(p));
    }

    private void Pressed(PointerPressedEventArgs e)
    {
        if (e.Handled || pointer != null || !System7Theme.GetIsActive(list) || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().TakeWhile(x => x is not ListBoxItem).OfType<Button>().Any()) return;
        scroll = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(x => x.Name == "PART_ScrollViewer");
        viewport = scroll?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
        position = e.GetPosition(list);
        if (ItemAt(position, false) is not { } item) return;
        var index = list.IndexFromContainer(item);
        if (index < 0) return;
        var multiple = list.SelectionMode.HasFlag(SelectionMode.Multiple);
        range = multiple && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        toggle = multiple && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control) || list.SelectionMode.HasFlag(SelectionMode.Toggle));
        original = list.Selection.SelectedIndexes.ToArray();
        columns = list.ItemsPanelRoot is WrapPanel
            ? list.GetRealizedContainers().GroupBy(container => container.Bounds.Y).Select(row => row.Count()).DefaultIfEmpty(1).Max() : 1;
        anchor = range && original.Length > 0 ? index < original.Min() ? original.Max() : original.Min() : index;
        selectSense = !original.Contains(index);
        first = index;
        previous = -1;
        pointer = e.Pointer;
        e.PreventGestureRecognition();
        e.Pointer.Capture(list);
        item.Focus(NavigationMethod.Pointer);
        Select(index);
        e.Handled = true;
    }

    private void Moved(PointerEventArgs e)
    {
        if (pointer != e.Pointer) return;
        position = e.GetPosition(list);
        if (!autoScroll.IsEnabled && !ScrollOutside()) SelectAtPosition();
        e.Handled = true;
    }

    private void Released(PointerReleasedEventArgs e)
    {
        if (pointer != e.Pointer) return;
        position = e.GetPosition(list);
        if (autoScroll.IsEnabled) SelectAtPosition();
        Stop();
        e.Handled = true;
    }

    private void SelectAtPosition()
    {
        list.UpdateLayout();
        if (ItemAt(position, true) is { } item) Select(list.IndexFromContainer(item));
    }

    private bool ScrollOutside()
    {
        if (pointer == null || scroll == null || viewport == null || list.TranslatePoint(position, viewport) is not { } local) return false;
        var before = scroll.Offset;
        if (local.Y <= 0) scroll.LineUp();
        else if (local.Y >= viewport.Bounds.Height - 1) scroll.LineDown();
        else
        {
            scrollElapsed.Reset();
            return false;
        }
        if (scroll.Offset == before) return false;
        if (!scrollElapsed.IsRunning) scrollElapsed.Start();
        // System 7's $A84C selector $500 delays scrolling by 3 + 108 / (12 + elapsed ticks).
        var ticks = (int)(scrollElapsed.Elapsed.TotalSeconds * 60);
        autoScroll.Interval = TimeSpan.FromSeconds((3 + 108 / (12 + ticks)) / 60d);
        autoScroll.Start();
        return true;
    }

    private void Stop()
    {
        autoScroll.Stop();
        scrollElapsed.Reset();
        var captured = pointer;
        pointer = null;
        captured?.Capture(null);
    }

    private void Select(int index)
    {
        if (index < 0 || index == previous) return;
        var from = previous < 0 ? index : previous;
        previous = index;
        if (!range) anchor = index;
        var selection = list.Selection;
        selection.BeginBatchUpdate();
        try
        {
            if (toggle)
            {
                foreach (var cell in CellsBetween(from, index))
                    if (selectSense) selection.Select(cell);
                    else selection.Deselect(cell);
            }
            else
            {
                selection.Clear();
                if (range)
                    foreach (var cell in CellsBetween(anchor, index)) selection.Select(cell);
                else
                {
                    if (!selectSense)
                        foreach (var old in original)
                            if (old != first) selection.Select(old);
                    selection.Select(index);
                }
            }
            selection.AnchorIndex = anchor;
        }
        finally { selection.EndBatchUpdate(); }
    }

    private IEnumerable<int> CellsBetween(int firstCell, int lastCell)
    {
        var left = Math.Min(firstCell % columns, lastCell % columns);
        var right = Math.Max(firstCell % columns, lastCell % columns);
        for (var row = Math.Min(firstCell / columns, lastCell / columns); row <= Math.Max(firstCell / columns, lastCell / columns); row++)
            for (var column = left; column <= right; column++)
                if (row * columns + column < list.ItemCount) yield return row * columns + column;
    }
}
