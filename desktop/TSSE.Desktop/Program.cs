using Avalonia;
using TSSE.Desktop.Services;

namespace TSSE.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        SmokeTrace.Mark("main-start");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        SmokeTrace.Mark("lifetime-returned");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
