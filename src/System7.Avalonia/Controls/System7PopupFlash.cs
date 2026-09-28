using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace System7.Avalonia.Controls;

/// <summary>
/// Blinks the chosen item of a pop-up before it is selected, as MDEF 0 does, while the pop-up otherwise keeps Avalonia's own behavior.
/// </summary>
internal sealed class System7PopupFlash : IDisposable
{
    private static readonly System7Attachments<ComboBox, System7PopupFlash> Instances = new(owner => new System7PopupFlash(owner));
    private readonly ComboBox owner;
    private readonly System7FlashTimer timer;
    private ComboBoxItem? flashing;

    public static void SetEnabled(ComboBox owner, bool enabled) => Instances.Set(owner, enabled);

    public void Dispose()
    {
        Cancel();
        owner.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
        owner.RemoveHandler(InputElement.PointerReleasedEvent, Released);
        owner.RemoveHandler(InputElement.PointerMovedEvent, Moved);
        owner.DropDownClosed -= Closed;
    }

    private System7PopupFlash(ComboBox owner)
    {
        this.owner = owner;
        timer = new(() => System7Theme.SetIsPopupItemHighlighted(flashing!, !System7Theme.GetIsPopupItemHighlighted(flashing!)), Commit);
        owner.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
        owner.DropDownClosed += Closed;
    }

    private bool Active => !System7Theme.GetUseNativePopupTracking(owner);

    private ComboBoxItem? ItemAt(PointerEventArgs e) => owner.IsDropDownOpen
        ? e.Source as ComboBoxItem ?? (e.Source as Control)?.FindLogicalAncestorOfType<ComboBoxItem>() : null;

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (flashing != null || Active && ItemAt(e) != null) e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
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
        if (!Active || e.InitialPressMouseButton != MouseButton.Left || ItemAt(e) is not { } item) return;
        e.Handled = true;
        if (!item.IsEffectivelyEnabled || System7Theme.GetIsMenuSeparator(item)) return;
        flashing = item;
        if (timer.Start(owner)) System7Theme.SetIsPopupItemHighlighted(item, false);
        else Commit();
    }

    private void Commit()
    {
        var item = flashing;
        Cancel();
        if (item == null) return;
        owner.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
        var index = owner.IndexFromContainer(item);
        if (index >= 0) owner.SetCurrentValue(ComboBox.SelectedIndexProperty, index);
        owner.Focus();
    }

    private void Closed(object? sender, EventArgs e) => Cancel();

    private void Cancel()
    {
        timer.Stop();
        // The item goes back to following the pointer.
        flashing?.ClearValue(System7Theme.IsPopupItemHighlightedProperty);
        flashing = null;
    }
}
