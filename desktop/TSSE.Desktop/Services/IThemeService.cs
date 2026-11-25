namespace TSSE.Desktop.Services;

public interface IThemeService
{
    bool IsDark { get; }
    void SetDark(bool isDark);
}
