using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Catalog_exposes_the_requested_preview_sections_without_fake_save_data()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        string[] expected = ["Command Center", "Savegame Explorer", "Vehicle Studio", "Economy Intelligence", "Savegame Time Machine", "Compatibility Lab", "Diagnostics Center", "Settings"];
        Assert.Equal(expected, viewModel.FilteredNavigationItems.Select(item => item.Title));
        Assert.All(viewModel.FilteredNavigationItems.Skip(1), item =>
            Assert.Contains(item.Page.StatusLabel, new[] { "PREVIEW", "UNVERIFIED", "FOUNDATION ONLY", "AVAILABLE" }));
        Assert.All(viewModel.FilteredNavigationItems.SelectMany(item => item.Page.StatusCards), card =>
            Assert.DoesNotContain("€", card.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Search_filters_navigation_and_can_be_cleared()
    {
        MainWindowViewModel viewModel = CreateViewModel();

        viewModel.SearchQuery = "coach";

        Assert.Contains(viewModel.FilteredNavigationItems, item => item.Title == "Vehicle Studio");
        Assert.Contains(viewModel.FilteredNavigationItems, item => item.Title == "Compatibility Lab");

        viewModel.SearchQuery = "";
        Assert.Equal(8, viewModel.FilteredNavigationItems.Count);
    }

    [Fact]
    public void Navigation_command_changes_the_current_page()
    {
        MainWindowViewModel viewModel = CreateViewModel();
        NavigationItemViewModel target = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "compatibility-lab");

        viewModel.NavigateCommand.Execute(target);

        Assert.Equal("Compatibility Lab", viewModel.CurrentPage.Title);
    }

    [Fact]
    public void Vehicle_categories_and_analytics_windows_only_change_preview_state()
    {
        MainWindowViewModel viewModel = CreateViewModel();
        SectionViewModel vehicle = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "vehicle-studio").Page;
        SectionViewModel economy = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "economy-intelligence").Page;

        vehicle.SelectVehicleCategoryCommand.Execute("Coaches");
        economy.SelectAnalyticsWindowCommand.Execute("30");

        Assert.Equal("Coaches", vehicle.SelectedVehicleCategory);
        Assert.Contains("preview", vehicle.SelectedVehicleMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30, economy.SelectedWindowDays);
        Assert.Contains("local snapshots only", economy.AnalyticsNotice, StringComparison.Ordinal);
        Assert.Contains("No transaction history or account values", economy.AnalyticsNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_toggle_is_delegated_to_the_theme_service()
    {
        var theme = new TestThemeService();
        MainWindowViewModel viewModel = CreateViewModel(theme);

        viewModel.IsDarkTheme = false;

        Assert.False(viewModel.IsDarkTheme);
        Assert.False(theme.IsDark);
        Assert.Equal(1, theme.ChangeCount);
    }

    private static MainWindowViewModel CreateViewModel(TestThemeService? theme = null)
    {
        var registry = new TSSE.GameAdapters.Compatibility.GameAdapterRegistry();
        var catalog = new PageCatalog(new FoundationStatusService(registry));
        var navigation = new NavigationService(catalog);
        return new MainWindowViewModel(navigation, theme ?? new TestThemeService());
    }

    private sealed class TestThemeService : IThemeService
    {
        public bool IsDark { get; private set; } = true;
        public int ChangeCount { get; private set; }

        public void SetDark(bool isDark)
        {
            IsDark = isDark;
            ChangeCount++;
        }
    }
}
