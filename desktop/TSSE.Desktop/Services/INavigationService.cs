using TSSE.Desktop.ViewModels;

namespace TSSE.Desktop.Services;

public interface INavigationService
{
    IReadOnlyList<NavigationItemViewModel> Items { get; }
    SectionViewModel CurrentPage { get; }
    bool Navigate(string pageKey);
}
