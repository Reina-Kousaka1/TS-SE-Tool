using Avalonia;
using Avalonia.Headless;
using TSSE.Desktop;

[assembly: Avalonia.Headless.XUnit.AvaloniaTestApplication(typeof(TSSE.Desktop.Tests.TestAppBuilder))]

namespace TSSE.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
