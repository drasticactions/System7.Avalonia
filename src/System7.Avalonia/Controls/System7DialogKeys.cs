using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace System7.Avalonia.Controls;

internal sealed class System7DialogKeys
{
    private static readonly ConditionalWeakTable<TopLevel, System7DialogKeys> Instances = new();
    private readonly HashSet<(Key Key, PhysicalKey Physical)> down = new();
    private readonly Queue<(Control Scope, bool Cancel)> queued = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(8d / 60) };
    private Button? pressed;
    private Control? scope;

    private System7DialogKeys() => timer.Tick += (_, _) => Complete();

    public static void Initialize()
    {
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(KeyDown, RoutingStrategies.Tunnel);
        InputElement.KeyUpEvent.AddClassHandler<TopLevel>((root, e) =>
        {
            if (Instances.TryGetValue(root, out var state)) state.down.Remove((e.Key, e.PhysicalKey));
        }, RoutingStrategies.Tunnel, true);
        Button.IsPressedProperty.Changed.AddClassHandler<Button>((button, change) =>
        {
            if (!change.GetNewValue<bool>() && TopLevel.GetTopLevel(button) is { } root && Instances.TryGetValue(root, out var state)
                && state.queued.Count > 0)
                Dispatcher.UIThread.Post(state.DrainQueue, DispatcherPriority.Background);
        });
    }

    private static void KeyDown(TopLevel root, KeyEventArgs e)
    {
        var cancel = e.Key == Key.Escape || (e.KeyModifiers & KeyModifiers.Meta) != 0
            && (e.KeySymbol == "." || e.KeySymbol == null && e.Key == Key.OemPeriod);
        if (e.Handled || e.Key != Key.Enter && !cancel) return;
        var controls = root.GetVisualDescendants().OfType<Control>().ToArray();
        if (controls.Any(c => c is ComboBox { IsDropDownOpen: true } or MenuBase { IsOpen: true })) return;
        if (e.Source is Visual source && source.GetVisualAncestors().Prepend(source).OfType<TextBox>()
            .Any(box => box.GetVisualDescendants().OfType<TextPresenter>().Any(p => !string.IsNullOrEmpty(p.PreeditText)))) return;
        var scopes = controls.Prepend(root).Where(System7Theme.GetUseNativeDialogKeys).ToArray();
        if (scopes.Length == 0) return;
        var focused = e.Source as Visual;
        var owner = focused?.GetVisualAncestors().Prepend(focused).OfType<Control>()
            .FirstOrDefault(c => scopes.Contains(c) && Available(c));
        owner ??= scopes.LastOrDefault(c => Available(c) && Buttons(c).Any(b => b.IsDefault || b.IsCancel));
        if (owner == null)
        {
            if (scopes.Any(c => Buttons(c).Any(b => cancel ? b.IsCancel : b.IsDefault))) e.Handled = true;
            return;
        }
        var button = Buttons(owner).FirstOrDefault(b => cancel ? b.IsCancel : b.IsDefault);
        if (button == null) return;
        e.Handled = true;
        var state = Instances.GetValue(root, _ => new());
        // StdFilterProc handles keyDown; autoKey events do not activate default or cancel items.
        if (!state.down.Add((e.Key, e.PhysicalKey))) return;
        if (state.pressed != null || controls.OfType<Button>().Any(b => b.IsPressed))
        {
            state.queued.Enqueue((owner, cancel));
            return;
        }
        state.Begin(owner, button);
    }

    private static bool Available(Control owner) => owner is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && System7Theme.GetIsActive(owner);

    private static IEnumerable<Button> Buttons(Control owner) => owner.GetVisualDescendants().OfType<Button>()
        .Where(button => button.GetVisualAncestors().OfType<Control>().FirstOrDefault(System7Theme.GetUseNativeDialogKeys) == owner);

    private void Begin(Control owner, Button button)
    {
        // HideDItem moves the item offscreen without disabling its default-key alias.
        if (!Available(owner) || !button.IsEffectivelyEnabled || TopLevel.GetTopLevel(button) == null) return;
        scope = owner;
        pressed = button;
        button.DetachedFromVisualTree += Detached;
        button.SetValue(System7Theme.IsDialogButtonPressedProperty, true);
        timer.Start();
    }

    private void Detached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        queued.Clear();
        Stop();
    }

    private void Stop()
    {
        timer.Stop();
        if (pressed != null)
        {
            pressed.DetachedFromVisualTree -= Detached;
            pressed.ClearValue(System7Theme.IsDialogButtonPressedProperty);
        }
        pressed = null;
        scope = null;
    }

    private void Complete()
    {
        var button = pressed;
        var owner = scope;
        Stop();
        if (button != null && owner != null && Available(owner) && button.IsEffectivelyEnabled
            && TopLevel.GetTopLevel(button) != null && System7Theme.GetUseNativeDialogKeys(owner)
            && ControlAutomationPeer.CreatePeerForElement(button) is IInvokeProvider invoke)
            invoke.Invoke();
        DrainQueue();
    }

    private void DrainQueue()
    {
        while (pressed == null && queued.TryDequeue(out var next))
        {
            if (!System7Theme.GetUseNativeDialogKeys(next.Scope)) continue;
            var target = Buttons(next.Scope).FirstOrDefault(b => next.Cancel ? b.IsCancel : b.IsDefault);
            if (target != null) Begin(next.Scope, target);
        }
    }
}
