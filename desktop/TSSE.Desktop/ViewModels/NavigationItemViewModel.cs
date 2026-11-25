namespace TSSE.Desktop.ViewModels;

public sealed class NavigationItemViewModel(
    string pageKey,
    string title,
    string glyph,
    SectionViewModel page) : ObservableObject
{
    private bool _isSelected;

    public string PageKey { get; } = pageKey;
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public SectionViewModel Page { get; } = page;
    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    internal void SetSelected(bool isSelected) => IsSelected = isSelected;
}
