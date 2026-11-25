using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using TSSE.Desktop;
using TSSE.Desktop.Services;
using Xunit;

namespace TSSE.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class AvaloniaThemeServiceTests
{
    [AvaloniaFact]
    public void Loads_and_saves_theme_and_surfaces_local_storage_errors()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "theme-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        App application = Assert.IsType<App>(Application.Current);

        try
        {
            var store = new LocalThemePreferenceStore(root);
            store.SaveIsDark(false);

            var service = new AvaloniaThemeService(application, store);

            Assert.False(service.IsDark);
            Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
            Assert.Contains("stored on this device", service.PreferenceStatus, StringComparison.Ordinal);

            service.SetDark(true);

            Assert.True(store.LoadIsDark());
            Assert.Equal(ThemeVariant.Dark, application.RequestedThemeVariant);
            Assert.Equal("Theme preference saved locally.", service.PreferenceStatus);

            File.WriteAllText(Path.Combine(root, "theme.preference"), "malformed");
            var malformedStoreService = new AvaloniaThemeService(application, store);

            Assert.True(malformedStoreService.IsDark);
            Assert.Contains("Could not read", malformedStoreService.PreferenceStatus, StringComparison.Ordinal);

            string blockedPath = Path.Combine(root, "existing-file");
            File.WriteAllText(blockedPath, "file");
            var blockedService = new AvaloniaThemeService(application, new LocalThemePreferenceStore(blockedPath));
            blockedService.SetDark(false);

            Assert.False(blockedService.IsDark);
            Assert.Contains("could not be saved", blockedService.PreferenceStatus, StringComparison.Ordinal);
        }
        finally
        {
            application.RequestedThemeVariant = ThemeVariant.Dark;
            Directory.Delete(root, recursive: true);
        }
    }
}
