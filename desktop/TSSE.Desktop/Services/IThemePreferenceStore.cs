namespace TSSE.Desktop.Services;

public interface IThemePreferenceStore
{
    bool LoadIsDark();
    void SaveIsDark(bool isDark);
}
