using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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
        registrations.AddSingleton<GameAdapterRegistry>();
        registrations.AddSingleton<IFoundationStatusService, FoundationStatusService>();
        registrations.AddSingleton<IThemeService>(_ => new AvaloniaThemeService(this));
        registrations.AddSingleton<PageCatalog>();
        registrations.AddSingleton<INavigationService, NavigationService>();
        registrations.AddSingleton<MainWindowViewModel>();
        ServiceProvider provider = registrations.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(provider.GetRequiredService<MainWindowViewModel>());
            desktop.Exit += (_, _) => provider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
