using System.Collections.ObjectModel;
using TSSE.Desktop.Services;

namespace TSSE.Desktop.ViewModels;

public sealed class SectionViewModel : ObservableObject
{
    private readonly IUiTextCatalog _text;
    private string _selectedVehicleCategory = "Trucks";
    private int _selectedWindowDays = 7;

    public SectionViewModel(
        string pageKey,
        string title,
        string eyebrow,
        string subtitle,
        string statusLabel,
        string emptyStateTitle,
        string emptyStateMessage,
        IEnumerable<StatusCardViewModel> statusCards,
        IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        PageKey = pageKey;
        Title = title;
        Eyebrow = eyebrow;
        Subtitle = subtitle;
        StatusLabel = statusLabel;
        EmptyStateTitle = emptyStateTitle;
        EmptyStateMessage = emptyStateMessage;
        StatusCards = new ObservableCollection<StatusCardViewModel>(statusCards);
        SettingsStorageHeading = _text.Get("settings.storage.heading");
        SettingsStorageMessage = _text.Get("settings.storage.message");
        VehicleCategoryHeading = _text.Get("vehicle.category.heading");
        TrucksLabel = _text.Get("vehicle.category.trucks");
        TrailersLabel = _text.Get("vehicle.category.trailers");
        CoachesLabel = _text.Get("vehicle.category.coaches");
        AnalyticsWindowHeading = _text.Get("analytics.window.heading");
        SevenDaysLabel = _text.Get("analytics.window.sevenDays");
        ThirtyDaysLabel = _text.Get("analytics.window.thirtyDays");
        SelectVehicleCategoryCommand = new RelayCommand<string>(SelectVehicleCategory);
        SelectAnalyticsWindowCommand = new RelayCommand<string>(SelectAnalyticsWindow);
        ProfileDemo = new ProfileDemoViewModel(_text);
        VehicleDemo = new VehicleDemoViewModel(_text);
    }

    public string PageKey { get; }
    public string Title { get; }
    public string Eyebrow { get; }
    public string Subtitle { get; }
    public string StatusLabel { get; }
    public string EmptyStateTitle { get; }
    public string EmptyStateMessage { get; }
    public ObservableCollection<StatusCardViewModel> StatusCards { get; }
    public bool IsVehicleStudio => PageKey == "vehicle-studio";
    public bool IsEconomyIntelligence => PageKey == "economy-intelligence";
    public bool IsSettings => PageKey == "settings";
    public bool IsSavegameExplorer => PageKey == "savegame-explorer";
    public ProfileDemoViewModel ProfileDemo { get; }
    public VehicleDemoViewModel VehicleDemo { get; }
    public bool IsVehicleDemoVisible => IsVehicleStudio && !IsCoachesSelected;
    public string SettingsStorageHeading { get; }
    public string SettingsStorageMessage { get; }
    public string VehicleCategoryHeading { get; }
    public string TrucksLabel { get; }
    public string TrailersLabel { get; }
    public string CoachesLabel { get; }
    public string AnalyticsWindowHeading { get; }
    public string SevenDaysLabel { get; }
    public string ThirtyDaysLabel { get; }
    public string SelectedVehicleCategory => _selectedVehicleCategory;
    public bool IsTrucksSelected => _selectedVehicleCategory == "Trucks";
    public bool IsTrailersSelected => _selectedVehicleCategory == "Trailers";
    public bool IsCoachesSelected => _selectedVehicleCategory == "Coaches";
    public int SelectedWindowDays => _selectedWindowDays;
    public bool IsSevenDaysSelected => _selectedWindowDays == 7;
    public bool IsThirtyDaysSelected => _selectedWindowDays == 30;
    public string SelectedVehicleCategoryLabel => _text.Get($"vehicle.category.{_selectedVehicleCategory.ToLowerInvariant()}");
    public string SelectedWindowLabel => _text.Format("analytics.window.selected", _selectedWindowDays);
    public string SelectedVehicleMessage => _text.Format("vehicle.selectedPreview", SelectedVehicleCategoryLabel);
    public string AnalyticsNotice => _text.Format("analytics.notice", _selectedWindowDays);
    public RelayCommand<string> SelectVehicleCategoryCommand { get; }
    public RelayCommand<string> SelectAnalyticsWindowCommand { get; }

    private void SelectVehicleCategory(string category)
    {
        if (category is not ("Trucks" or "Trailers" or "Coaches"))
            return;

        if (SetProperty(ref _selectedVehicleCategory, category, nameof(SelectedVehicleCategory)))
        {
            OnPropertyChanged(nameof(IsTrucksSelected));
            OnPropertyChanged(nameof(IsTrailersSelected));
            OnPropertyChanged(nameof(IsCoachesSelected));
            OnPropertyChanged(nameof(IsVehicleDemoVisible));
            VehicleDemo.SelectCategory(category);
            OnPropertyChanged(nameof(SelectedVehicleCategoryLabel));
            OnPropertyChanged(nameof(SelectedVehicleMessage));
        }
    }

    private void SelectAnalyticsWindow(string value)
    {
        if (!int.TryParse(value, out int days) || days is not (7 or 30))
            return;

        if (SetProperty(ref _selectedWindowDays, days, nameof(SelectedWindowDays)))
        {
            OnPropertyChanged(nameof(IsSevenDaysSelected));
            OnPropertyChanged(nameof(IsThirtyDaysSelected));
            OnPropertyChanged(nameof(SelectedWindowLabel));
            OnPropertyChanged(nameof(AnalyticsNotice));
        }
    }
}
