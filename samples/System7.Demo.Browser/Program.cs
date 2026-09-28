using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using System7.Demo;

internal static partial class Program
{
    private static Task Main(string[] args) => AppBuilder.Configure<App>().StartBrowserAppAsync("out");

    [JSExport]
    public static bool InsertText(string text)
    {
        if (Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime { MainView: { } view } ||
            TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement() is not TextBox textBox)
            return false;
        textBox.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });
        return true;
    }
}
