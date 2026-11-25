using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TSSE.Desktop;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using TSSE.GameAdapters.Compatibility;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class AvaloniaWindowTests
{
    [AvaloniaFact]
    public void Main_window_loads_navigation_theme_and_preview_content()
    {
        var catalog = new PageCatalog(new FoundationStatusService(new GameAdapterRegistry()));
        var viewModel = new MainWindowViewModel(new NavigationService(catalog), new TestThemeService());
        var window = new MainWindow(viewModel);

        try
        {
            window.Show();

            Assert.NotNull(window.FindControl<TextBox>("SectionSearch"));
            Assert.NotNull(window.FindControl<ToggleSwitch>("ThemeSwitch"));
            Assert.NotNull(window.FindControl<ContentControl>("PageHost"));
            Assert.IsType<SectionViewModel>(window.FindControl<ContentControl>("PageHost")?.Content);
            Assert.Equal("Command Center", viewModel.CurrentPage.Title);

            Button[] navigationButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("nav-item"))
                .ToArray();
            Assert.Equal(8, navigationButtons.Length);
            Assert.Single(navigationButtons, button => button.Classes.Contains("selected"));
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestThemeService : IThemeService
    {
        public bool IsDark { get; private set; } = true;
        public void SetDark(bool isDark) => IsDark = isDark;
    }
}
