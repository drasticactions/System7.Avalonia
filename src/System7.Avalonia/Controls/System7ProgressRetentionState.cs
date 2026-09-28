using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace System7.Avalonia.Controls;

/// <summary>
/// CDEF 62 leaves its previous pixels in place while the control is disabled or its window is inactive. This keeps the last
/// value such a control drew, and forgets it where the Window Manager would have erased the pixels.
/// </summary>
internal sealed class System7ProgressRetentionState
{
    private static readonly ConditionalWeakTable<ProgressBar, System7ProgressRetentionState> Instances = new();
    private readonly ProgressBar bar;
    private readonly System7ProgressRetention kind;
    private double progress;
    private double total = 100;
    private bool hasValue;
    private bool drawn = true;
    private bool laidOut;
    private int frame = -1;

    public static void SetKind(ProgressBar bar, System7ProgressRetention kind)
    {
        if (Instances.TryGetValue(bar, out var state))
        {
            bar.PropertyChanged -= state.Changed;
            Instances.Remove(bar);
        }
        if (kind != System7ProgressRetention.None) Instances.Add(bar, new System7ProgressRetentionState(bar, kind));
    }

    private System7ProgressRetentionState(ProgressBar bar, System7ProgressRetention kind)
    {
        this.bar = bar;
        this.kind = kind;
        bar.PropertyChanged += Changed;
        Update();
    }

    private bool Live => bar.IsEffectivelyEnabled && System7Theme.GetIsActive(bar);

    private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == System7Theme.ProgressFractionProperty || e.Property == System7Theme.IsProgressDrawnProperty
            || e.Property == System7Theme.SpeakerFrameProperty) return;
        if (kind == System7ProgressRetention.Speaker) SpeakerChanged(e);
        else BarChanged(e);
    }

    private void BarChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && !bar.IsVisible) drawn = false;
        if (e.Property == Visual.BoundsProperty)
        {
            if (laidOut && e.GetOldValue<Rect>().Size != bar.Bounds.Size && !Live) drawn = false;
            laidOut |= bar.Bounds.Size != default;
        }
        if (Live && bar.IsVisible) Remember();
        Update();
    }

    private void Remember()
    {
        progress = bar.Value - bar.Minimum;
        total = bar.Maximum - bar.Minimum;
        hasValue = true;
        drawn = true;
    }

    private void SpeakerChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == System7Theme.WindowBackgroundProperty && laidOut)
        {
            // Erasing the window removes pixels retained by a missing or disabled ICON.
            frame = -1;
            RememberFrame();
            Update();
            return;
        }
        if (e.Property == Visual.IsVisibleProperty && !bar.IsVisible) frame = -1;
        if (e.Property == Visual.BoundsProperty)
        {
            if (laidOut && e.GetOldValue<Rect>().Size != bar.Bounds.Size) frame = -1;
            // CDEF 62 first draws once the control is laid out, after its settings are in place.
            laidOut |= bar.Bounds.Size != default;
        }
        if (laidOut && bar.IsVisible) RememberFrame();
        Update();
    }

    private void RememberFrame()
    {
        if (!Live) return;
        var range = (int)bar.Maximum - (int)bar.Minimum;
        if (range == 0) return;
        // CDEF 62 uses the absolute value; its seventh ICON resource is absent.
        var index = 6 * (int)bar.Value / range;
        if ((uint)index < 6) frame = index;
    }

    private void Update()
    {
        var sized = bar.Bounds is { Width: > 0, Height: > 0 };
        if (kind == System7ProgressRetention.Speaker)
        {
            bar.SetValue(System7Theme.SpeakerFrameProperty, sized ? frame : -1);
            return;
        }
        if (!hasValue && sized) Remember();
        bar.SetValue(System7Theme.IsProgressDrawnProperty, drawn && sized);
        bar.SetValue(System7Theme.ProgressFractionProperty, total > 0 ? Math.Clamp(progress, 0, total) / total : 1);
    }
}
