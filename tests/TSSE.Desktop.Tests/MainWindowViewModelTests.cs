using System.Globalization;
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
    public void Catalog_communicates_parser_compatibility_and_recovery_limits()
    {
        MainWindowViewModel viewModel = CreateViewModel();
        SectionViewModel save = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "savegame-explorer").Page;
        SectionViewModel compatibility = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "compatibility-lab").Page;
        SectionViewModel recovery = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "time-machine").Page;
        SectionViewModel diagnostics = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "diagnostics-center").Page;

        Assert.Contains(save.StatusCards, card => card.Value == "Synthetic tests only");
        Assert.Contains(compatibility.StatusCards, card => card.Value == "Not verified");
        Assert.Contains(compatibility.StatusCards, card => card.Value == "Not tested");
        Assert.Contains(recovery.StatusCards, card => card.Value == "Core library only");
        Assert.Contains(recovery.StatusCards, card => card.Value == "Unavailable");
        Assert.Contains(diagnostics.StatusCards, card => card.Value == "Syntax only");
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
    public void Navigation_selection_tracks_the_current_page()
    {
        MainWindowViewModel viewModel = CreateViewModel();
        NavigationItemViewModel commandCenter = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "command-center");
        NavigationItemViewModel target = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "compatibility-lab");

        Assert.True(commandCenter.IsSelected);
        Assert.False(target.IsSelected);

        viewModel.NavigateCommand.Execute(target);

        Assert.False(commandCenter.IsSelected);
        Assert.True(target.IsSelected);
        Assert.Single(viewModel.FilteredNavigationItems, item => item.IsSelected);
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
        Assert.False(vehicle.IsTrucksSelected);
        Assert.False(vehicle.IsTrailersSelected);
        Assert.True(vehicle.IsCoachesSelected);
        Assert.False(vehicle.IsVehicleDemoVisible);
        Assert.Contains("preview", vehicle.SelectedVehicleMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30, economy.SelectedWindowDays);
        Assert.False(economy.IsSevenDaysSelected);
        Assert.True(economy.IsThirtyDaysSelected);
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
        Assert.Equal(theme.PreferenceStatus, viewModel.ThemePreferenceStatus);
    }

    [Theory]
    [InlineData("en-US", "Command Center", "English", "Search sections", "Dark theme")]
    [InlineData("de-DE", "Steuerzentrale", "Deutsch", "Bereiche suchen", "Dunkles Theme")]
    [InlineData("fr-FR", "Command Center", "English", "Search sections", "Dark theme")]
    public void System_culture_localizes_the_shell_and_falls_back_to_English(
        string cultureName,
        string expectedFirstPage,
        string expectedLanguage,
        string expectedSearch,
        string expectedTheme)
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo(cultureName));
        var catalog = new PageCatalog(
            new FoundationStatusService(new TSSE.GameAdapters.Compatibility.GameAdapterRegistry()), text);
        var theme = new TestThemeService();
        var viewModel = new MainWindowViewModel(new NavigationService(catalog), theme, text);

        Assert.Equal(expectedFirstPage, viewModel.CurrentPage.Title);
        Assert.Equal(expectedLanguage, text.CurrentLanguageName);
        Assert.Equal(expectedSearch, viewModel.SearchPlaceholder);
        Assert.Equal(expectedTheme, viewModel.DarkThemeLabel);

        SectionViewModel settings = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "settings").Page;
        Assert.Contains(settings.StatusCards, card => card.Value == expectedLanguage);

        SectionViewModel vehicle = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "vehicle-studio").Page;
        vehicle.SelectVehicleCategoryCommand.Execute("Coaches");
        Assert.Contains(expectedLanguage == "Deutsch" ? "Reisebusse" : "Coaches", vehicle.SelectedVehicleMessage, StringComparison.Ordinal);

        SectionViewModel economy = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "economy-intelligence").Page;
        economy.SelectAnalyticsWindowCommand.Execute("30");
        Assert.Contains(expectedLanguage == "Deutsch" ? "30-Tage-Ansicht" : "30-day view", economy.AnalyticsNotice, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(TestThemeService? theme = null)
    {
        var registry = new TSSE.GameAdapters.Compatibility.GameAdapterRegistry();
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en-US"));
        var catalog = new PageCatalog(new FoundationStatusService(registry), text);
        var navigation = new NavigationService(catalog);
        return new MainWindowViewModel(navigation, theme ?? new TestThemeService(), text);
    }

    private sealed class TestThemeService : IThemeService
    {
        public bool IsDark { get; private set; } = true;
        public int ChangeCount { get; private set; }
        public string PreferenceStatus { get; private set; } = "Theme preference has not changed.";

        public void SetDark(bool isDark)
        {
            IsDark = isDark;
            ChangeCount++;
            PreferenceStatus = "Theme preference saved locally.";
        }
    }
}
