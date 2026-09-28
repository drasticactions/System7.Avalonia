using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

internal sealed class System7MenuTracking : IDisposable
{
    private static readonly System7Attachments<Menu, System7MenuTracking> Instances = new(owner => new System7MenuTracking(owner));
    private readonly Menu menu;
    private TopLevel? root;
    private IPointer? pointer;
    private readonly System7FlashTimer flashTimer;
    private MenuItem? flashing;
    private IPointer? flashPointer;
    private readonly DispatcherTimer submenuTimer = new();
    private readonly DispatcherTimer aimTimer = new();
    private readonly DispatcherTimer scrollTimer = new();
    private MenuItem? hovered;
    private MenuItem? opening;
    private MenuItem? aimOwner;
    private Point? aimAnchor;
    private Point position;
    private System7MenuSurface? scrolling;
    private int scrollDirection;
    private bool touchScroll;
    private MenuItem? touchOpening;

    static System7MenuTracking()
    {
        MenuItem.PointerEnteredItemEvent.AddClassHandler<MenuItem>(SuppressHover);
        MenuItem.PointerExitedItemEvent.AddClassHandler<MenuItem>(SuppressHover);
        MenuItem.SubmenuOpenedEvent.AddClassHandler<MenuItem>((item, e) =>
        {
            if (e.Source != item || Find(item) is not { root: { } root } tracking) return;
            System7MenuPlacement.Prepare(item, tracking.menu, root);
            if (System7MenuPlacement.Presenter(item) is { } presenter) presenter.ScrollOffset = 0;
            if (System7MenuPlacement.Popup(item) is { } popup)
            {
                popup.Opened -= tracking.PopupOpened;
                popup.Opened += tracking.PopupOpened;
            }
        });
    }

    private static System7MenuTracking? Find(MenuItem item)
    {
        for (StyledElement? parent = item.Parent; parent != null; parent = parent.Parent)
            if (parent is Menu menu && Instances.TryGet(menu, out var tracking)) return tracking;
        return null;
    }

    private static void SuppressHover(MenuItem item, RoutedEventArgs e)
    {
        if (Find(item) != null) e.Handled = true;
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        if (sender is not global::Avalonia.Controls.Primitives.Popup { PlacementTarget: MenuItem item } || root == null) return;
        item.UpdateLayout();
        System7MenuPlacement.Prepare(item, menu, root);
        item.UpdateLayout();
    }

    public static void SetEnabled(Menu menu, bool enabled) => Instances.Set(menu, enabled);

    public void Dispose()
    {
        DetachRoot();
        menu.AttachedToVisualTree -= Attached;
        menu.DetachedFromVisualTree -= Detached;
        menu.Closed -= Closed;
        menu.Close();
        foreach (var item in menu.GetLogicalDescendants().OfType<MenuItem>().ToList()) System7MenuPlacement.Reset(System7MenuPlacement.Popup(item));
    }

    private System7MenuTracking(Menu menu)
    {
        this.menu = menu;
        flashTimer = new(() => flashing!.IsSelected = !flashing.IsSelected, CompleteFlash);
        submenuTimer.Tick += (_, _) =>
        {
            submenuTimer.Stop();
            if (opening == null || opening != hovered || !opening.IsEffectivelyEnabled) return;
            var depth = 0;
            for (StyledElement? ancestor = opening; ancestor is MenuItem; ancestor = ancestor.Parent) depth++;
            if (depth > 5) return;
            opening.SetCurrentValue(MenuItem.IsSubMenuOpenProperty, true);
            aimOwner = opening;
            aimAnchor = null;
        };
        aimTimer.Tick += (_, _) =>
        {
            ClearAim();
            UpdateHover(true);
        };
        scrollTimer.Tick += (_, _) => UpdateHover(scrollElapsed: true);
        menu.AttachedToVisualTree += Attached;
        menu.DetachedFromVisualTree += Detached;
        menu.Closed += Closed;
        AttachRoot();
    }

    private void Attached(object? sender, VisualTreeAttachmentEventArgs e) => AttachRoot();
    private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => DetachRoot();
    private void Closed(object? sender, RoutedEventArgs e)
    {
        pointer = null;
        CancelFlash();
        StopTracking();
    }

    private void AttachRoot()
    {
        var next = TopLevel.GetTopLevel(menu);
        if (next == root) return;
        DetachRoot();
        root = next;
        if (root == null) return;
        root.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, true);
        root.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, true);
        root.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, true);
    }

    private void DetachRoot()
    {
        pointer = null;
        CancelFlash();
        StopTracking();
        if (root == null) return;
        root.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        root.RemoveHandler(InputElement.PointerMovedEvent, Moved);
        root.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        root = null;
    }

    private MenuItem? TitleAt(PointerEventArgs e)
    {
        var point = e.GetPosition(menu);
        if (!menu.IsEffectivelyVisible || !menu.IsEffectivelyEnabled || point.Y < 0 || point.Y >= menu.Bounds.Height) return null;
        foreach (var item in menu.GetRealizedContainers().OfType<MenuItem>())
        {
            var local = e.GetPosition(item);
            if (item.IsVisible && local.X >= 0 && local.X < item.Bounds.Width) return item;
        }
        return null;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (flashing != null)
        {
            e.Handled = true;
            return;
        }
        if (!e.GetCurrentPoint(menu).Properties.IsLeftButtonPressed) return;
        if (root != null) position = e.GetPosition(root);
        var touched = e.Pointer.Type == PointerType.Touch ? TitleAt(e) ?? (menu.IsOpen ? Hit()?.Item : null) : null;
        touchOpening = touched is { ItemCount: > 0, IsEffectivelyEnabled: true } ? touched : null;
        if (menu.IsOpen && e.Pointer.Type == PointerType.Touch && Hit() is { } hit && ScrollDirection(hit.Presenter, hit.Local) != 0)
        {
            touchOpening = null;
            touchScroll = true;
            pointer = e.Pointer;
            UpdateHover();
            e.PreventGestureRecognition();
            e.Pointer.Capture(menu);
            e.Handled = true;
            return;
        }
        if (e.Pointer.Type != PointerType.Mouse) return;
        if (TitleAt(e) is not { } item) return;
        Open(item);
        pointer = e.Pointer;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (!menu.IsOpen || flashing != null || root == null) return;
        position = e.GetPosition(root);
        if (TitleAt(e) is { } item)
        {
            StopTracking();
            Open(item);
            Select(item, null);
        }
        else UpdateHover();
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || flashing != null) return;
        if (root != null) position = e.GetPosition(root);
        if (e.Pointer.Type == PointerType.Touch && touchOpening != null)
        {
            Select(touchOpening, null);
            touchOpening = null;
            e.Handled = true;
            return;
        }
        if (touchScroll && pointer == e.Pointer)
        {
            touchScroll = false;
            pointer = null;
            StopScroll();
            if (Hit() is { } scrolled) Select(scrolled.Parent, null);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        var hit = Hit();
        var item = hit is { } target && ScrollDirection(target.Presenter, target.Local) == 0 ? target.Item : null;
        var owned = hit != null && BelongsToMenu(hit.Value.Parent);
        if (pointer != e.Pointer && !(e.Pointer.Type == PointerType.Touch && menu.IsOpen && owned)) return;
        pointer = null;
        StopTracking();
        if (owned && item is { IsTopLevel: false, ItemCount: 0, IsEffectivelyEnabled: true } && !Equals(item.Header, "-"))
        {
            e.Handled = true;
            BeginFlash(item, e.Pointer);
        }
        else if (e.Pointer.Type == PointerType.Mouse)
        {
            menu.Close();
            e.Handled = true;
        }
    }

    private bool BelongsToMenu(MenuItem item)
    {
        for (StyledElement? parent = item; parent != null; parent = parent.Parent)
            if (parent == menu) return true;
        return false;
    }

    private IEnumerable<MenuItem> OpenMenus(ItemsControl parent)
    {
        foreach (var item in parent.GetRealizedContainers().OfType<MenuItem>())
            if (item.IsSubMenuOpen)
            {
                yield return item;
                foreach (var child in OpenMenus(item)) yield return child;
            }
    }

    private (MenuItem Parent, MenuItem? Item, System7MenuSurface Presenter, Point Local)? Hit()
    {
        if (root == null) return null;
        foreach (var parent in OpenMenus(menu).Reverse())
        {
            if (System7MenuPlacement.Presenter(parent) is not { } presenter) continue;
            var local = root.TranslatePoint(position, presenter);
            if (local == null || local.Value.X < 1 || local.Value.X >= presenter.Bounds.Width - 2) continue;
            if (local.Value.Y < 1 || local.Value.Y >= presenter.Bounds.Height - 2)
            {
                if (ScrollDirection(presenter, local.Value) != 0) return (parent, null, presenter, local.Value);
                continue;
            }
            foreach (var child in parent.GetRealizedContainers().OfType<MenuItem>())
            {
                var point = root.TranslatePoint(position, child);
                if (point is { Y: >= 0 } && point.Value.Y < child.Bounds.Height)
                    return (parent, child, presenter, local.Value);
            }
            return (parent, null, presenter, local.Value);
        }
        return null;
    }

    private void UpdateHover(bool aimExpired = false, bool scrollElapsed = false)
    {
        if (root == null || !menu.IsOpen || flashing != null) return;
        var hit = Hit();
        if (!aimExpired && aimOwner is { IsSubMenuOpen: true } owner && hit?.Item != owner)
        {
            var inChild = false;
            for (StyledElement? parent = hit?.Parent; parent is MenuItem; parent = parent.Parent)
                if (parent == owner) inChild = true;
            if (inChild) ClearAim();
            else if (System7MenuPlacement.Presenter(owner) is { } child)
            {
                var origin = child.TranslatePoint(new Point(1, 1), root) ?? default;
                var unit = child.TranslatePoint(new Point(2, 2), root) ?? new Point(1, 1);
                var sx = Math.Max(.001, unit.X - origin.X);
                var sy = Math.Max(.001, unit.Y - origin.Y);
                if (aimAnchor == null)
                {
                    aimAnchor = position;
                    aimTimer.Interval = System7Theme.GetSubmenuAimDelay(menu);
                    aimTimer.Start();
                }
                var anchor = new Point(Math.Floor(aimAnchor.Value.X / sx), Math.Floor(aimAnchor.Value.Y / sy));
                var point = new Point(Math.Floor(position.X / sx), Math.Floor(position.Y / sy));
                var bounds = new Rect(origin.X / sx, origin.Y / sy, child.Bounds.Width - 3, child.Bounds.Height - 3);
                if (System7MenuAim.Contains(anchor, point, bounds, System7Theme.GetColorDepth(menu) != System7ColorDepth.Monochrome)) return;
                ClearAim();
            }
        }
        else if (hit?.Item == aimOwner) { aimAnchor = null; aimTimer.Stop(); }
        if (hit is { } current)
        {
            var presenter = current.Presenter;
            var direction = ScrollDirection(presenter, current.Local);
            if (direction != 0)
            {
                Select(current.Parent, null);
                SetHovered(null);
                var slow = direction < 0 ? current.Local.Y >= 9 : current.Local.Y <= presenter.Bounds.Height - 10;
                if (slow && !scrollElapsed && (scrolling != presenter || scrollDirection != direction))
                {
                    scrollTimer.Stop();
                    scrolling = presenter;
                    scrollDirection = direction;
                    scrollTimer.Interval = TimeSpan.FromSeconds(10d / 60);
                    scrollTimer.Start();
                    return;
                }
                if (slow && !scrollElapsed) return;
                presenter.ScrollOffset = Math.Clamp(presenter.ScrollOffset + direction * 16, 0, Math.Max(0, presenter.ContentHeight - presenter.ViewportHeight));
                presenter.UpdateLayout();
                scrolling = presenter;
                scrollDirection = direction;
                scrollTimer.Interval = TimeSpan.FromSeconds((slow ? 10d : 1d) / 60);
                scrollTimer.Start();
                return;
            }
            StopScroll();
            var selected = current.Item is { IsEffectivelyEnabled: true } candidate && !Equals(candidate.Header, "-") ? candidate : null;
            Select(current.Parent, selected);
            SetHovered(selected);
            if (selected is { IsSubMenuOpen: true })
            {
                Select(selected, null);
                aimOwner = selected;
                aimAnchor = null;
            }
        }
        else
        {
            StopScroll();
            foreach (var parent in menu.GetRealizedContainers().OfType<MenuItem>().Where(i => i.IsSubMenuOpen)) Select(parent, null);
            SetHovered(null);
        }
    }

    private static void Select(MenuItem parent, MenuItem? item)
    {
        foreach (var sibling in parent.GetRealizedContainers().OfType<MenuItem>())
            if (sibling != item && sibling.IsSubMenuOpen) sibling.SetCurrentValue(MenuItem.IsSubMenuOpenProperty, false);
        parent.SelectedItem = item;
    }

    private static int ScrollDirection(System7MenuSurface presenter, Point local) =>
        presenter.CanScrollUp && local.Y < 17 ? -1 : presenter.CanScrollDown && local.Y >= presenter.Bounds.Height - 18 ? 1 : 0;

    private void SetHovered(MenuItem? item)
    {
        if (hovered == item) return;
        hovered = item;
        submenuTimer.Stop();
        opening = item is { ItemCount: > 0, IsSubMenuOpen: false } ? item : null;
        if (opening == null) return;
        submenuTimer.Interval = System7Theme.GetSubmenuDelay(menu);
        submenuTimer.Start();
    }

    private void ClearAim()
    {
        aimTimer.Stop();
        aimOwner = null;
        aimAnchor = null;
    }

    private void StopScroll()
    {
        scrollTimer.Stop();
        scrolling = null;
        scrollDirection = 0;
    }

    private void StopTracking()
    {
        submenuTimer.Stop();
        hovered = opening = null;
        touchScroll = false;
        ClearAim();
        StopScroll();
    }

    private void BeginFlash(MenuItem item, IPointer inputPointer)
    {
        flashing = item;
        if (!flashTimer.Start(menu))
        {
            CompleteFlash();
            return;
        }
        item.IsSelected = false;
        if (inputPointer.Type == PointerType.Mouse)
        {
            flashPointer = inputPointer;
            Dispatcher.UIThread.Post(() =>
            {
                if (flashing == item) inputPointer.Capture(menu);
            }, DispatcherPriority.Send);
        }
    }

    private void CompleteFlash()
    {
        var item = flashing;
        CancelFlash();
        if (item != null) System7MenuFlash.Choose(menu, item);
    }

    private void CancelFlash()
    {
        flashTimer.Stop();
        flashing = null;
        var captured = flashPointer;
        flashPointer = null;
        if (captured?.Captured == menu) captured.Capture(null);
    }

    private void Open(MenuItem item)
    {
        menu.Open();
        menu.SelectedItem = item;
        foreach (var sibling in menu.GetRealizedContainers().OfType<MenuItem>())
            if (sibling != item && sibling.IsSubMenuOpen) sibling.SetCurrentValue(MenuItem.IsSubMenuOpenProperty, false);
        item.SetCurrentValue(MenuItem.IsSubMenuOpenProperty, item.ItemCount > 0);
    }
}
