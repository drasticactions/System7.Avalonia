using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System7.Avalonia.Rendering;

namespace System7.Avalonia.Controls;

internal sealed class System7PopupTracking : IDisposable
{
    private static readonly System7Attachments<ComboBox, System7PopupTracking> Instances = new(owner => new System7PopupTracking(owner));
    private readonly ComboBox owner;
    private TopLevel? root;
    private Popup? popup;
    private System7MenuSurface? menu;
    private IPointer? pointer;
    private Point position;
    private Point pressPosition;
    private bool openedByTouch;
    private int highlighted = -1;
    private int flashing = -1;
    private int pendingScroll;
    private readonly System7FlashTimer flash;
    private readonly DispatcherTimer scroll = new() { Interval = TimeSpan.FromMilliseconds(16) };

    public static void SetEnabled(ComboBox owner, bool enabled) => Instances.Set(owner, enabled);

    private System7PopupTracking(ComboBox owner)
    {
        this.owner = owner;
        owner.TemplateApplied += TemplateApplied;
        owner.AttachedToVisualTree += Attached;
        owner.DetachedFromVisualTree += Detached;
        owner.PropertyChanged += PropertyChanged;
        owner.DropDownOpened += Opened;
        owner.DropDownClosed += Closed;
        owner.PointerCaptureLost += CaptureLost;
        flash = new(() => Highlight(highlighted < 0 ? flashing : -1), Commit);
        scroll.Tick += (_, _) => UpdateHover(true);
        popup = owner.GetTemplateDescendants().OfType<Popup>().FirstOrDefault(p => p.Name == "PART_Popup");
        menu = popup?.Child as System7MenuSurface;
        AttachRoot();
    }

    private void TemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        popup = e.NameScope.Find<Popup>("PART_Popup");
        menu = e.NameScope.Find<System7MenuSurface>("PART_PopupMenu");
        Prepare();
    }

    private void Attached(object? sender, VisualTreeAttachmentEventArgs e) => AttachRoot();
    private void Detached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Close();
        DetachRoot();
    }

    private void AttachRoot()
    {
        var next = TopLevel.GetTopLevel(owner);
        if (next == root) return;
        DetachRoot();
        root = next;
        if (root == null) return;
        root.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, true);
        root.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, true);
        root.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, true);
        root.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel, true);
    }

    private void DetachRoot()
    {
        Stop();
        if (root == null) return;
        root.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        root.RemoveHandler(InputElement.PointerMovedEvent, Moved);
        root.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        root.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
        root = null;
    }

    private void PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ComboBox.IsDropDownOpenProperty && owner.IsDropDownOpen)
        {
            if (owner.ItemCount == 0) Close();
            else Prepare();
        }
        if (e.Property == InputElement.IsEffectivelyEnabledProperty && !owner.IsEffectivelyEnabled) Close();
        if (e.Property == ItemsControl.ItemCountProperty && owner.IsDropDownOpen)
        {
            if (owner.ItemCount == 0) Close();
            else Prepare();
        }
    }

    private void Prepare()
    {
        if (popup == null || menu == null || root == null || owner.Bounds.Width <= 0) return;
        var titleWidth = Math.Clamp(System7Theme.GetPopupTitleWidth(owner), 0, owner.Bounds.Width);
        var origin = owner.TranslatePoint(default, root) ?? default;
        var unit = owner.TranslatePoint(new Point(1, 1), root) ?? new Point(1, 1);
        var scaleX = Math.Max(0.001, unit.X - origin.X);
        var scaleY = Math.Max(0.001, unit.Y - origin.Y);
        var left = origin.X / scaleX;
        var top = origin.Y / scaleY;
        var itemWidth = (double)System7TextConverters.MenuWidth(owner.Items);
        foreach (var item in owner.GetRealizedContainers().OfType<ComboBoxItem>())
            if (item.ContentTemplate != null || item.Content is not null and not string)
            {
                item.Measure(Size.Infinity);
                itemWidth = Math.Max(itemWidth, item.DesiredSize.Width);
            }
        var screenWidth = root.Bounds.Width / scaleX;
        var screenHeight = root.Bounds.Height / scaleY;
        var width = Math.Min(Math.Max(owner.Bounds.Width - titleWidth, Math.Min(itemWidth, screenWidth - 16) + 3), Math.Max(3, screenWidth - 5));
        var height = Math.Min(owner.ItemCount * 16, Math.Min(owner.MaxDropDownHeight - 3, root.Bounds.Height / scaleY - 27));
        height = Math.Max(16, height);
        var desiredTop = top + Math.Floor((owner.Bounds.Height - 19) / 2) - Math.Max(0, owner.SelectedIndex) * 16;
        var actualTop = desiredTop;
        if (actualTop < 21) actualTop += Math.Ceiling((21 - actualTop) / 16) * 16;
        else if (actualTop + height + 1 > screenHeight - 5)
            actualTop -= Math.Ceiling((actualTop + height + 1 - (screenHeight - 5)) / 16) * 16;
        var actualLeft = Math.Clamp(left + titleWidth, 3, Math.Max(3, screenWidth - width - 2));
        System7MenuPlacement.Place(popup, (actualLeft - left) * scaleX, (actualTop - top) * scaleY);
        menu.Width = width;
        menu.ViewportHeight = height;
        menu.ScrollOffset = actualTop - desiredTop;
    }

    private void Opened(object? sender, EventArgs e)
    {
        owner.UpdateLayout();
        Prepare();
        owner.UpdateLayout();
        Highlight(owner.SelectedIndex);
        if (pointer == null || openedByTouch) ShowHighlightedItem();
    }

    private void Closed(object? sender, EventArgs e) => Stop();

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (pointer == e.Pointer && flashing < 0) Close();
    }

    private bool ContainsOwner(PointerEventArgs e)
    {
        if (e.Source is not Visual source || source != owner && !source.GetVisualAncestors().Contains(owner)) return false;
        var point = e.GetPosition(owner);
        var top = Math.Floor((owner.Bounds.Height - 19) / 2);
        return owner is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } &&
               point.X >= 0 && point.X < owner.Bounds.Width && point.Y >= top && point.Y < top + 19;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (!owner.IsDropDownOpen && !ContainsOwner(e)) return;
        if (flashing >= 0) { e.Handled = true; return; }
        if (pointer != null && pointer != e.Pointer) return;
        if (!e.GetCurrentPoint(owner).Properties.IsLeftButtonPressed) return;
        if (owner is { IsDropDownOpen: false, IsEditable: true } && e.Source is Visual v &&
            (v is TextBox || v.FindAncestorOfType<TextBox>() != null)) return;
        if (owner.ItemCount == 0)
        {
            pointer = e.Pointer;
            e.Pointer.Capture(owner);
            e.Handled = true;
            return;
        }
        var opening = !owner.IsDropDownOpen;
        pointer = e.Pointer;
        openedByTouch = opening && e.Pointer.Type == PointerType.Touch;
        if (opening)
        {
            owner.Focus(NavigationMethod.Pointer);
            owner.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
            owner.UpdateLayout();
        }
        pointer = e.Pointer;
        pressPosition = position = e.GetPosition(root);
        e.PreventGestureRecognition();
        e.Pointer.Capture(owner);
        if (opening) Highlight(IsSelectable(owner.SelectedIndex) ? owner.SelectedIndex : -1);
        else UpdateHover();
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (!owner.IsDropDownOpen || flashing >= 0) return;
        position = e.GetPosition(root);
        UpdateHover();
        if (pointer == e.Pointer) e.Handled = true;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (pointer != e.Pointer || flashing >= 0) return;
        position = e.GetPosition(root);
        UpdateHover();
        e.Handled = true;
        if (openedByTouch && new Vector(position.X - pressPosition.X, position.Y - pressPosition.Y).Length < 4)
        {
            openedByTouch = false;
            pointer = null;
            e.Pointer.Capture(null);
            return;
        }
        openedByTouch = false;
        if (highlighted < 0) Close();
        else BeginFlash(highlighted);
    }

    private void UpdateHover(bool scrollElapsed = false)
    {
        if (root == null || menu == null || !owner.IsDropDownOpen || flashing >= 0) return;
        var local = root.TranslatePoint(position, menu) ?? new Point(-1, -1);
        var inColumns = local.X >= 1 && local.X < menu.Bounds.Width - 2;
        if (!inColumns)
        {
            Highlight(-1);
            scroll.Stop();
            pendingScroll = 0;
            return;
        }
        var direction = menu.CanScrollUp && local.Y < 17 ? -1 : menu.CanScrollDown && local.Y >= menu.Bounds.Height - 18 ? 1 : 0;
        if (direction != 0)
        {
            Highlight(-1);
            var slow = direction < 0 ? local.Y >= 9 : local.Y <= menu.Bounds.Height - 10;
            if (slow && !scrollElapsed)
            {
                if (pendingScroll != direction)
                {
                    scroll.Stop();
                    pendingScroll = direction;
                    scroll.Interval = TimeSpan.FromSeconds(10d / 60);
                    scroll.Start();
                }
                return;
            }
            pendingScroll = 0;
            menu.ScrollOffset = direction < 0 ? Math.Max(0, menu.ScrollOffset - 16)
                : Math.Min(Math.Max(0, owner.ItemCount * 16 - menu.ViewportHeight), menu.ScrollOffset + 16);
            menu.UpdateLayout();
            scroll.Stop();
            if (direction < 0 ? !menu.CanScrollUp : !menu.CanScrollDown)
            {
                UpdateHover();
                return;
            }
            scroll.Interval = TimeSpan.FromSeconds((slow ? 10d : 1d) / 60);
            if (slow) pendingScroll = direction;
            scroll.Start();
            return;
        }
        scroll.Stop();
        pendingScroll = 0;
        var index = (int)Math.Floor((local.Y - 1 + menu.ScrollOffset) / 16);
        Highlight(local.Y >= 1 && local.Y < menu.Bounds.Height - 2 && IsSelectable(index) ? index : -1);
    }

    private bool IsSelectable(int index) => index >= 0 && index < owner.ItemCount &&
        owner.ContainerFromIndex(index) is not Control { IsEffectivelyEnabled: false } &&
        owner.Items[index] is not Control { IsEffectivelyEnabled: false } && System7TextConverters.ItemText(owner.Items[index]) != "-";

    private void Highlight(int index)
    {
        highlighted = index;
        foreach (var item in owner.GetRealizedContainers().OfType<ComboBoxItem>())
            System7Theme.SetIsPopupItemHighlighted(item, owner.IndexFromContainer(item) == index);
    }

    private void BeginFlash(int index)
    {
        scroll.Stop();
        pendingScroll = 0;
        flashing = index;
        if (flash.Start(owner)) Highlight(-1);
        else Commit();
    }

    private void Commit()
    {
        var selected = flashing;
        Close();
        if (IsSelectable(selected)) owner.SetCurrentValue(ComboBox.SelectedIndexProperty, selected);
        owner.Focus();
    }

    private void Close()
    {
        owner.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
        Stop();
    }

    private void Stop()
    {
        flash.Stop();
        scroll.Stop();
        pendingScroll = 0;
        flashing = -1;
        openedByTouch = false;
        Highlight(-1);
        var captured = pointer;
        pointer = null;
        if (captured?.Captured == owner) captured.Capture(null);
    }

    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if (!owner.IsDropDownOpen)
        {
            if (owner is { IsFocused: true, IsEditable: false } && e is { KeyModifiers: KeyModifiers.None, Key: Key.Up or Key.Down })
            {
                var step = e.Key == Key.Down ? 1 : -1;
                for (var index = owner.SelectedIndex + step; index >= 0 && index < owner.ItemCount; index += step)
                    if (IsSelectable(index)) { owner.SetCurrentValue(ComboBox.SelectedIndexProperty, index); break; }
                e.Handled = true;
            }
            return;
        }
        if (pointer != null || flashing >= 0) { e.Handled = true; return; }
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key is Key.Enter or Key.Space && highlighted >= 0) { BeginFlash(highlighted); e.Handled = true; }
        else if (e.Key is Key.Up or Key.Down)
        {
            var step = e.Key == Key.Down ? 1 : -1;
            for (var index = highlighted + step; index >= 0 && index < owner.ItemCount; index += step)
                if (IsSelectable(index))
                {
                    Highlight(index);
                    ShowHighlightedItem();
                    break;
                }
            e.Handled = true;
        }
    }

    private void ShowHighlightedItem()
    {
        if (menu == null || highlighted < 0) return;
        var maximumOffset = Math.Max(0, owner.ItemCount * 16 - menu.ViewportHeight);
        menu.ScrollOffset = Math.Clamp(menu.ScrollOffset, 0, maximumOffset);
        if (highlighted * 16 - menu.ScrollOffset < (menu.CanScrollUp ? 16 : 0))
            menu.ScrollOffset = Math.Max(0, (highlighted - 1) * 16);
        else if ((highlighted + 1) * 16 - menu.ScrollOffset > menu.ViewportHeight - (menu.CanScrollDown ? 16 : 0))
            menu.ScrollOffset = Math.Clamp((highlighted + 2) * 16 - menu.ViewportHeight, 0, maximumOffset);
    }

    public void Dispose()
    {
        Close();
        DetachRoot();
        owner.TemplateApplied -= TemplateApplied;
        owner.AttachedToVisualTree -= Attached;
        owner.DetachedFromVisualTree -= Detached;
        owner.PropertyChanged -= PropertyChanged;
        owner.DropDownOpened -= Opened;
        owner.DropDownClosed -= Closed;
        owner.PointerCaptureLost -= CaptureLost;
        foreach (var item in owner.GetRealizedContainers()) item.ClearValue(System7Theme.IsPopupItemHighlightedProperty);
        System7MenuPlacement.Reset(owner.GetTemplateDescendants().OfType<Popup>().FirstOrDefault());
    }
}
