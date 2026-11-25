using Avalonia;
using Avalonia.Styling;

namespace TSSE.Desktop.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    private readonly App _application;
    private readonly IThemePreferenceStore _preferenceStore;

    public AvaloniaThemeService(App application, IThemePreferenceStore preferenceStore)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));

        try
        {
            IsDark = _preferenceStore.LoadIsDark();
            PreferenceStatus = "Theme preference is stored on this device.";
        }
        catch (Exception exception) when (IsPreferenceError(exception))
        {
            IsDark = true;
            PreferenceStatus = "Could not read the local theme preference; using dark theme for this session.";
        }

        ApplyTheme();
    }

    public bool IsDark { get; private set; }
    public string PreferenceStatus { get; private set; }

    public void SetDark(bool isDark)
    {
        IsDark = isDark;
        ApplyTheme();

        try
        {
            _preferenceStore.SaveIsDark(isDark);
            PreferenceStatus = "Theme preference saved locally.";
        }
        catch (Exception exception) when (IsPreferenceError(exception))
        {
            PreferenceStatus = "Theme changed for this session; the local preference could not be saved.";
        }
    }

    private void ApplyTheme() =>
        _application.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    private static bool IsPreferenceError(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException;
}
