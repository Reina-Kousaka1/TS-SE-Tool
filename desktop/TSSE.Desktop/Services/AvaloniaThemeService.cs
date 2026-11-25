using Avalonia;
using Avalonia.Styling;

namespace TSSE.Desktop.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    private readonly App _application;
    private readonly IThemePreferenceStore _preferenceStore;
    private readonly IUiTextCatalog _text;

    public AvaloniaThemeService(App application, IThemePreferenceStore preferenceStore, IUiTextCatalog text)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
        _text = text ?? throw new ArgumentNullException(nameof(text));

        try
        {
            IsDark = _preferenceStore.LoadIsDark();
            PreferenceStatus = _text.Get("theme.preference.stored");
        }
        catch (Exception exception) when (IsPreferenceError(exception))
        {
            IsDark = true;
            PreferenceStatus = _text.Get("theme.preference.readFailed");
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
            PreferenceStatus = _text.Get("theme.preference.saved");
        }
        catch (Exception exception) when (IsPreferenceError(exception))
        {
            PreferenceStatus = _text.Get("theme.preference.saveFailed");
        }
    }

    private void ApplyTheme() =>
        _application.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    private static bool IsPreferenceError(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException;
}
