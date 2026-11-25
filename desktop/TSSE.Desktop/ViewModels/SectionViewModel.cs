using System.Collections.ObjectModel;

namespace TSSE.Desktop.ViewModels;

public sealed class SectionViewModel : ObservableObject
{
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
        IEnumerable<StatusCardViewModel> statusCards)
    {
        PageKey = pageKey;
        Title = title;
        Eyebrow = eyebrow;
        Subtitle = subtitle;
        StatusLabel = statusLabel;
        EmptyStateTitle = emptyStateTitle;
        EmptyStateMessage = emptyStateMessage;
        StatusCards = new ObservableCollection<StatusCardViewModel>(statusCards);
        SelectVehicleCategoryCommand = new RelayCommand<string>(SelectVehicleCategory);
        SelectAnalyticsWindowCommand = new RelayCommand<string>(SelectAnalyticsWindow);
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
    public string SelectedVehicleCategory => _selectedVehicleCategory;
    public int SelectedWindowDays => _selectedWindowDays;
    public string SelectedWindowLabel => $"{_selectedWindowDays}-day window selected";
    public string SelectedVehicleMessage => $"{_selectedVehicleCategory} view is a preview. No compatible savegame is loaded.";
    public string AnalyticsNotice => $"The {_selectedWindowDays}-day view will show local snapshots only. No transaction history or account values are currently loaded.";
    public RelayCommand<string> SelectVehicleCategoryCommand { get; }
    public RelayCommand<string> SelectAnalyticsWindowCommand { get; }

    private void SelectVehicleCategory(string category)
    {
        if (category is not ("Trucks" or "Trailers" or "Coaches"))
            return;

        if (SetProperty(ref _selectedVehicleCategory, category, nameof(SelectedVehicleCategory)))
            OnPropertyChanged(nameof(SelectedVehicleMessage));
    }

    private void SelectAnalyticsWindow(string value)
    {
        if (!int.TryParse(value, out int days) || days is not (7 or 30))
            return;

        if (SetProperty(ref _selectedWindowDays, days, nameof(SelectedWindowDays)))
        {
            OnPropertyChanged(nameof(SelectedWindowLabel));
            OnPropertyChanged(nameof(AnalyticsNotice));
        }
    }
}
