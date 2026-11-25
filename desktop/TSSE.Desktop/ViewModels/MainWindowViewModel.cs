using System.Collections.ObjectModel;
using TSSE.Desktop.Services;

namespace TSSE.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly IReadOnlyList<NavigationItemViewModel> _allNavigationItems;
    private string _searchQuery = string.Empty;
    private SectionViewModel _currentPage;

    public MainWindowViewModel(INavigationService navigationService, IThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _currentPage = navigationService.CurrentPage;
        _allNavigationItems = navigationService.Items;
        foreach (NavigationItemViewModel item in _allNavigationItems)
            item.SetSelected(item.PageKey == _currentPage.PageKey);

        FilteredNavigationItems = new ObservableCollection<NavigationItemViewModel>(_allNavigationItems);
        NavigateCommand = new RelayCommand<NavigationItemViewModel>(item =>
        {
            if (navigationService.Navigate(item.PageKey))
            {
                CurrentPage = navigationService.CurrentPage;
                foreach (NavigationItemViewModel navigationItem in _allNavigationItems)
                    navigationItem.SetSelected(navigationItem.PageKey == CurrentPage.PageKey);
            }
        });
    }

    public ObservableCollection<NavigationItemViewModel> FilteredNavigationItems { get; }
    public RelayCommand<NavigationItemViewModel> NavigateCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value ?? string.Empty))
                RefreshNavigation();
        }
    }

    public SectionViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public bool IsDarkTheme
    {
        get => _themeService.IsDark;
        set
        {
            if (value == _themeService.IsDark)
                return;

            _themeService.SetDark(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemePreferenceStatus));
        }
    }

    public string ThemePreferenceStatus => _themeService.PreferenceStatus;

    private void RefreshNavigation()
    {
        FilteredNavigationItems.Clear();
        foreach (NavigationItemViewModel item in _allNavigationItems.Where(item =>
                     item.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                     || item.Page.Subtitle.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                     || item.Page.EmptyStateMessage.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredNavigationItems.Add(item);
        }
    }
}
