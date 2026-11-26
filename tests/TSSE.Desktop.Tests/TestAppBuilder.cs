using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using TSSE.Desktop;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(TSSE.Desktop.Tests.TestAppBuilder))]

namespace TSSE.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
