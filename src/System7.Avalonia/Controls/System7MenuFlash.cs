using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace System7.Avalonia.Controls;

/// <summary>
/// Blinks a chosen menu item before its command runs, as MDEF 0 does, while the menu otherwise keeps Avalonia's own behavior.
/// </summary>
internal sealed class System7MenuFlash : IDisposable
{
    private static readonly System7Attachments<Menu, System7MenuFlash> Instances = new(owner => new System7MenuFlash(owner));
    private readonly Menu menu;
    private readonly System7FlashTimer timer;
    private MenuItem? flashing;

    static System7MenuFlash()
    {
        MenuItem.PointerEnteredItemEvent.AddClassHandler<MenuItem>(SuppressHover);
        MenuItem.PointerExitedItemEvent.AddClassHandler<MenuItem>(SuppressHover);
    }

    public static void SetEnabled(Menu menu, bool enabled) => Instances.Set(menu, enabled);

    public void Dispose()
    {
        Cancel();
        menu.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        menu.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        menu.Closed -= Closed;
    }

    private System7MenuFlash(Menu menu)
    {
        this.menu = menu;
        timer = new(() => flashing!.IsSelected = !flashing.IsSelected, Complete);
        menu.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        menu.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        menu.Closed += Closed;
    }

    private static void SuppressHover(MenuItem item, RoutedEventArgs e)
    {
        for (var parent = item.Parent; parent != null; parent = parent.Parent)
            if (parent is Menu menu && Instances.TryGet(menu, out var flash) && flash.flashing != null)
            {
                e.Handled = true;
                return;
            }
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (flashing != null) e.Handled = true;
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (flashing != null)
        {
            e.Handled = true;
            return;
        }
        if (e.InitialPressMouseButton != MouseButton.Left || System7Theme.GetUseNativeMenuTracking(menu)) return;
        var item = e.Source as MenuItem ?? (e.Source as Control)?.FindLogicalAncestorOfType<MenuItem>();
        if (item is not { IsTopLevel: false, HasSubMenu: false, IsEffectivelyEnabled: true } || Equals(item.Header, "-")) return;
        e.Handled = true;
        flashing = item;
        if (timer.Start(menu)) item.IsSelected = false;
        else Complete();
    }

    private void Complete()
    {
        var item = flashing;
        Cancel();
        if (item != null) Choose(menu, item);
    }

    /// <summary>Closes the menu unless the item stays open, then toggles the item and raises its click as MenuItem does.</summary>
    internal static void Choose(Menu menu, MenuItem item)
    {
        var enabled = item is { IsEffectivelyEnabled: true, IsVisible: true };
        if (!item.StaysOpenOnClick) menu.Close();
        if (!enabled) return;
        if (item.ToggleType == MenuItemToggleType.CheckBox)
            item.SetCurrentValue(MenuItem.IsCheckedProperty, !item.IsChecked);
        else if (item.ToggleType == MenuItemToggleType.Radio)
            item.SetCurrentValue(MenuItem.IsCheckedProperty, true);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private void Closed(object? sender, RoutedEventArgs e) => Cancel();

    private void Cancel()
    {
        timer.Stop();
        flashing = null;
    }
}
