using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using TSSE.GameAdapters.Compatibility;

namespace TSSE.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IUiTextCatalog>(_ => new UiTextCatalog(CultureInfo.CurrentUICulture));
        registrations.AddSingleton<GameAdapterRegistry>();
        registrations.AddSingleton<IFoundationStatusService, FoundationStatusService>();
        registrations.AddSingleton<IThemePreferenceStore, LocalThemePreferenceStore>();
        registrations.AddSingleton<IThemeService>(provider =>
            new AvaloniaThemeService(
                this,
                provider.GetRequiredService<IThemePreferenceStore>(),
                provider.GetRequiredService<IUiTextCatalog>()));
        registrations.AddSingleton<PageCatalog>();
        registrations.AddSingleton<INavigationService, NavigationService>();
        registrations.AddSingleton<MainWindowViewModel>();
        ServiceProvider provider = registrations.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow(provider.GetRequiredService<MainWindowViewModel>());
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => provider.Dispose();

            if (Environment.GetEnvironmentVariable("TSSE_DESKTOP_CI_SMOKE_TEST") == "1")
            {
                bool captureNativeWindow = Environment.GetEnvironmentVariable("TSSE_DESKTOP_CI_SCREENSHOT") == "1";
                var smokeTimer = new DispatcherTimer
                {
                    Interval = captureNativeWindow ? TimeSpan.FromSeconds(20) : TimeSpan.FromMilliseconds(1500)
                };
                smokeTimer.Tick += (_, _) =>
                {
                    smokeTimer.Stop();
                    desktop.MainWindow?.Close();
                };
                mainWindow.Opened += (_, _) => smokeTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
