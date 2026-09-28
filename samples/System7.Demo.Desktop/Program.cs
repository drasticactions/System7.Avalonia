using System;
using Avalonia;
using System7.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => AppBuilder.Configure<App>()
        .UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
