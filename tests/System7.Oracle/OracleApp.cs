using Avalonia;
using Avalonia.Styling;
using System7.Avalonia;

internal sealed class OracleApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new System7Theme());
        // Scroll arrows repeat on a timer; one step per press keeps captures independent of rendering speed.
        Resources["System7ScrollRepeatInterval"] = 3600000;
        Resources["System7CaretBlinkInterval"] = TimeSpan.FromHours(1);
    }
}
