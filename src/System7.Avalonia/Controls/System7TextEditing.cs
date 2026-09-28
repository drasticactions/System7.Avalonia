using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace System7.Avalonia.Controls;

internal static class System7TextEditing
{
    public static void Initialize()
    {
        InputElement.KeyDownEvent.AddClassHandler<TextBox>(KeyDown, RoutingStrategies.Tunnel);
        InputElement.GotFocusEvent.AddClassHandler<TextBox>((box, e) =>
        {
            if (System7Theme.GetUseNativeTextEditing(box) && e.NavigationMethod == NavigationMethod.Tab) box.SelectAll();
        });
    }

    private static void KeyDown(TextBox box, KeyEventArgs e)
    {
        if (!System7Theme.GetUseNativeTextEditing(box) || e.Handled || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter == null || !string.IsNullOrEmpty(presenter.PreeditText)) return;
        var forward = e.Key is Key.Right or Key.Down;
        var position = forward ? Math.Max(box.SelectionStart, box.SelectionEnd) : Math.Min(box.SelectionStart, box.SelectionEnd);
        presenter.MoveCaretToTextPosition(position);
        if (e.Key is Key.Left or Key.Right)
            presenter.MoveCaretHorizontal(forward ? LogicalDirection.Forward : LogicalDirection.Backward);
        else
        {
            presenter.MoveCaretVertical(forward ? LogicalDirection.Forward : LogicalDirection.Backward);
            if (presenter.CaretIndex == position) presenter.MoveCaretToTextPosition(forward ? box.Text?.Length ?? 0 : 0);
        }
        box.SetCurrentValue(TextBox.CaretIndexProperty, presenter.CaretIndex);
        box.ClearSelection();
        e.Handled = true;
    }
}
