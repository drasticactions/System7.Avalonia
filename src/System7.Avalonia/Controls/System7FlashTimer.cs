using Avalonia.Controls;
using Avalonia.Threading;

namespace System7.Avalonia.Controls;

/// <summary>Blinks a chosen menu item with the owner's flash count and interval, then completes the choice.</summary>
internal sealed class System7FlashTimer
{
    private readonly DispatcherTimer timer = new();
    private int phasesRemaining;

    public System7FlashTimer(Action toggle, Action complete) => timer.Tick += (_, _) =>
    {
        if (--phasesRemaining > 0) toggle();
        else
        {
            timer.Stop();
            complete();
        }
    };

    /// <summary>Starts blinking, or returns false when the owner's flash count is zero.</summary>
    public bool Start(Control owner)
    {
        phasesRemaining = Math.Clamp(System7Theme.GetMenuFlashCount(owner), 0, short.MaxValue) * 2;
        if (phasesRemaining == 0) return false;
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, System7Theme.GetMenuFlashInterval(owner)));
        timer.Start();
        return true;
    }

    public void Stop() => timer.Stop();
}
