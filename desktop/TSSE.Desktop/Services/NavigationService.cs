using TSSE.Desktop.ViewModels;

namespace TSSE.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IReadOnlyList<NavigationItemViewModel> _items;

    public NavigationService(PageCatalog catalog)
    {
        string[] glyphs = ["⌂", "▤", "◈", "◷", "↶", "✓", "⌁", "⚙"];
        _items = catalog.Sections.Select((page, index) =>
            new NavigationItemViewModel(page.PageKey, page.Title, glyphs[index], page)).ToArray();
        CurrentPage = catalog.Sections[0];
    }

    public IReadOnlyList<NavigationItemViewModel> Items => _items;
    public SectionViewModel CurrentPage { get; private set; }

    public bool Navigate(string pageKey)
    {
        NavigationItemViewModel? match = _items.FirstOrDefault(item => item.PageKey == pageKey);
        if (match is null)
            return false;

        CurrentPage = match.Page;
        return true;
    }
}
