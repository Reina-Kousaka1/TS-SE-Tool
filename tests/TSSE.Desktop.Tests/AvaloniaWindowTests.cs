using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
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
    public void Compatibility_lab_shows_synthetic_format_diagnostics()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            var page = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "compatibility-lab");
            viewModel.NavigateCommand.Execute(page);
            window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Synthetic format diagnostics" && block.IsEffectivelyVisible);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Exact byte roundtrip: yes" && block.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void Synthetic_profile_skill_editor_is_visible_in_savegame_explorer()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            var page = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "savegame-explorer");
            viewModel.NavigateCommand.Execute(page);
            window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Synthetic profile skills" && block.IsEffectivelyVisible);
            Assert.Contains(window.GetVisualDescendants().OfType<CheckBox>(), box =>
                Equals(box.Content, "Bit 1") && box.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void Job_demo_is_accessible_from_navigation_and_shows_synthetic_data()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            NavigationItemViewModel job = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "jobs");
            viewModel.NavigateCommand.Execute(job);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Active job — synthetic demo" && block.IsEffectivelyVisible);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "cargo.synthetic" && block.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vehicle_demo_is_visible_for_trucks_and_hidden_for_coaches()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            NavigationItemViewModel vehicle = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "vehicle-studio");
            viewModel.NavigateCommand.Execute(vehicle);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Vehicle editor — synthetic demo" && block.IsEffectivelyVisible);
            window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<ComboBox>(), box =>
                box.IsEffectivelyVisible && Equals(box.SelectedItem, "demo.truck"));
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text != null && block.Text.Contains("demo.missing — missing target", StringComparison.Ordinal) && block.IsEffectivelyVisible);
            vehicle.Page.SelectVehicleCategoryCommand.Execute("Trailers");
            window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<ComboBox>(), box =>
                box.IsEffectivelyVisible && Equals(box.SelectedItem, "demo.trailer"));
            vehicle.Page.SelectVehicleCategoryCommand.Execute("Coaches");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Text == "Vehicle editor — synthetic demo" && block.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

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

            string[] screenshotPageKeys =
            [
                "command-center", "savegame-explorer", "vehicle-studio", "jobs", "economy-intelligence",
                "time-machine", "compatibility-lab", "diagnostics-center", "settings"
            ];
            foreach (string pageKey in screenshotPageKeys)
            {
                NavigationItemViewModel page = viewModel.FilteredNavigationItems.Single(item => item.PageKey == pageKey);
                viewModel.NavigateCommand.Execute(page);
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                    block.Text == viewModel.CurrentPage.Title);
                CaptureVisualSnapshot(window, $"{pageKey}-dark.png");
            }

            NavigationItemViewModel vehicle = viewModel.FilteredNavigationItems.Single(item => item.PageKey == "vehicle-studio");
            viewModel.NavigateCommand.Execute(vehicle);
            viewModel.CurrentPage.SelectVehicleCategoryCommand.Execute("Coaches");
            viewModel.IsDarkTheme = false;
            CaptureVisualSnapshot(window, "coaches-preview-light.png");

            Button[] navigationButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("nav-item"))
                .ToArray();
            Assert.Equal(9, navigationButtons.Length);
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

    [AvaloniaFact]
    public void Mouse_navigation_updates_each_page_and_its_selected_item()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            string[] pageTitles =
            [
                "Command Center", "Savegame Explorer", "Vehicle Studio", "Jobs", "Economy Intelligence",
                "Savegame Time Machine", "Compatibility Lab", "Diagnostics Center", "Settings"
            ];

            foreach (string title in pageTitles)
            {
                Button navigationItem = FindNavigationButton(window, title);
                Click(window, navigationItem);

                Assert.Equal(title, viewModel.CurrentPage.Title);
                Assert.Contains("selected", navigationItem.Classes);
                TextBlock navigationTitle = Assert.Single(navigationItem.GetVisualDescendants().OfType<TextBlock>(),
                    block => block.Text == title);
                Assert.Equal(TextWrapping.Wrap, navigationTitle.TextWrapping);
                Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                    button => button.Classes.Contains("nav-item") && button.Classes.Contains("selected"));
                Assert.IsType<SectionViewModel>(window.FindControl<ContentControl>("PageHost")?.Content);
                Assert.Equal(2, window.GetVisualDescendants().OfType<TextBlock>().Count(block => block.Text == title));
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_space_activates_navigation_and_coach_preview_buttons()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            Button vehicleStudio = FindNavigationButton(window, "Vehicle Studio");
            Assert.True(vehicleStudio.Focus());
            Assert.True(vehicleStudio.IsFocused);

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

            Assert.Equal("Vehicle Studio", viewModel.CurrentPage.Title);
            Assert.Contains("selected", vehicleStudio.Classes);
            Button coaches = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == "Coaches");
            Assert.True(coaches.Focus());
            Assert.True(coaches.IsFocused);

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

            Assert.Equal("Coaches", viewModel.CurrentPage.SelectedVehicleCategory);
            Assert.Contains("selected", coaches.Classes);
            Assert.Contains("preview", viewModel.CurrentPage.SelectedVehicleMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No compatible savegame is loaded", viewModel.CurrentPage.SelectedVehicleMessage, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_in_search_filters_the_actual_navigation_controls()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            TextBox search = Assert.IsType<TextBox>(window.FindControl<TextBox>("SectionSearch"));
            Assert.True(search.Focus());

            window.KeyTextInput("Vehicle");

            Assert.Equal("Vehicle", search.Text);
            Assert.Equal("Vehicle", viewModel.SearchQuery);
            Assert.Equal("vehicle-studio", Assert.Single(viewModel.FilteredNavigationItems).PageKey);
            Button[] visibleNavigation = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("nav-item"))
                .ToArray();
            Assert.Equal("Vehicle Studio", Assert.Single(visibleNavigation)
                .GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == "Vehicle Studio").Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_selection_of_coaches_updates_the_preview_without_creating_game_data()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            Click(window, FindNavigationButton(window, "Vehicle Studio"));
            Button coaches = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == "Coaches");

            Click(window, coaches);

            Assert.Equal("Coaches", viewModel.CurrentPage.SelectedVehicleCategory);
            Assert.Contains("selected", coaches.Classes);
            Assert.DoesNotContain("selected", Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == "Trucks").Classes);
            Assert.Contains("preview", viewModel.CurrentPage.SelectedVehicleMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No compatible savegame is loaded", viewModel.CurrentPage.SelectedVehicleMessage, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_selection_of_thirty_day_analytics_updates_only_preview_state()
    {
        var (viewModel, window, _) = CreateMainWindow();
        try
        {
            window.Show();
            Click(window, FindNavigationButton(window, "Economy Intelligence"));
            Button thirtyDays = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content?.ToString() == "30 days");

            Click(window, thirtyDays);

            Assert.Equal(30, viewModel.CurrentPage.SelectedWindowDays);
            Assert.Contains("selected", thirtyDays.Classes);
            Assert.Contains("30-day view", viewModel.CurrentPage.AnalyticsNotice, StringComparison.Ordinal);
            Assert.Contains("No transaction history or account values", viewModel.CurrentPage.AnalyticsNotice, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_theme_switch_track_updates_the_view_model_and_theme_service()
    {
        (MainWindowViewModel viewModel, MainWindow window, TestThemeService themeService) = CreateMainWindow();
        try
        {
            window.Show();
            ToggleSwitch themeSwitch = Assert.IsType<ToggleSwitch>(window.FindControl<ToggleSwitch>("ThemeSwitch"));
            Assert.True(themeSwitch.IsChecked);
            Control switchTrack = Assert.Single(themeSwitch.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "SwitchAreaGrid");

            Click(window, switchTrack);

            Assert.False(themeSwitch.IsChecked);
            Assert.False(viewModel.IsDarkTheme);
            Assert.False(themeService.IsDark);
        }
        finally
        {
            window.Close();
        }
    }

    private static (MainWindowViewModel ViewModel, MainWindow Window, TestThemeService ThemeService) CreateMainWindow()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en-US"));
        var catalog = new PageCatalog(new FoundationStatusService(new GameAdapterRegistry()), text);
        var themeService = new TestThemeService();
        var viewModel = new MainWindowViewModel(new NavigationService(catalog), themeService, text);
        return (viewModel, new MainWindow(viewModel), themeService);
    }

    private static Button FindNavigationButton(MainWindow window, string title) =>
        Assert.Single(window.GetVisualDescendants().OfType<Button>(),
            button => button.Classes.Contains("nav-item")
                && button.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == title));

    private static void Click(Window window, Control control)
    {
        Point? center = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window);
        Assert.True(center.HasValue, "The target control must share the window's visual tree.");
        window.MouseDown(center.Value, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center.Value, MouseButton.Left, RawInputModifiers.None);
    }

    private static void CaptureVisualSnapshot(Window window, string fileName)
    {
        string? directory = Environment.GetEnvironmentVariable("TSSE_UI_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);
        using WriteableBitmap frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless rendering did not produce a frame.");
        using FileStream output = File.Create(Path.Combine(directory, fileName));
        frame.Save(output, new PngBitmapEncoderOptions());
    }

    private sealed class TestThemeService : IThemeService
    {
        public bool IsDark { get; private set; } = true;
        public string PreferenceStatus => "Theme preference is stored on this device.";
        public void SetDark(bool isDark)
        {
            IsDark = isDark;
            if (Application.Current is { } application)
                application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
