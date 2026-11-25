namespace TSSE.Desktop.Services;

public interface IThemeService
{
    bool IsDark { get; }
    string PreferenceStatus { get; }
    void SetDark(bool isDark);
}
