using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TSSE.Desktop;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using TSSE.GameAdapters.Compatibility;
using Xunit;

namespace TSSE.Desktop.Tests;

[Collection("Avalonia UI")]
public sealed class AvaloniaWindowTests
{
    [AvaloniaFact]
    public void Main_window_loads_navigation_theme_and_preview_content()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en-US"));
        var catalog = new PageCatalog(new FoundationStatusService(new GameAdapterRegistry()), text);
        var viewModel = new MainWindowViewModel(new NavigationService(catalog), new TestThemeService(), text);
        var window = new MainWindow(viewModel);

        try
        {
            window.Show();

            Assert.NotNull(window.FindControl<TextBox>("SectionSearch"));
            Assert.NotNull(window.FindControl<ToggleSwitch>("ThemeSwitch"));
            Assert.NotNull(window.FindControl<ContentControl>("PageHost"));
            Assert.IsType<SectionViewModel>(window.FindControl<ContentControl>("PageHost")?.Content);
            Assert.Equal("Command Center", viewModel.CurrentPage.Title);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == viewModel.ThemePreferenceStatus);

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

    [AvaloniaFact]
    public void Main_window_localizes_accessible_controls_for_the_system_language()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("de-DE"));
        var catalog = new PageCatalog(new FoundationStatusService(new GameAdapterRegistry()), text);
        var viewModel = new MainWindowViewModel(new NavigationService(catalog), new TestThemeService(), text);
        NavigationItemViewModel vehicle = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "vehicle-studio");
        vehicle.Page.SelectVehicleCategoryCommand.Execute("Coaches");
        viewModel.NavigateCommand.Execute(vehicle);
        var window = new MainWindow(viewModel);

        try
        {
            window.Show();

            TextBox search = Assert.IsType<TextBox>(window.FindControl<TextBox>("SectionSearch"));
            ToggleSwitch theme = Assert.IsType<ToggleSwitch>(window.FindControl<ToggleSwitch>("ThemeSwitch"));
            Assert.Equal("Bereiche suchen", search.PlaceholderText);
            Assert.Equal("Dunkles Theme", theme.Content?.ToString());
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "ARBEITSBEREICH");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Die Ansicht „Reisebusse“ ist eine Vorschau. Es ist kein kompatibler Spielstand geladen.");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestThemeService : IThemeService
    {
        public bool IsDark { get; private set; } = true;
        public string PreferenceStatus => "Theme preference is stored on this device.";
        public void SetDark(bool isDark) => IsDark = isDark;
    }
}
