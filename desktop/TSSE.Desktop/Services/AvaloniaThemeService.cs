using Avalonia;
using Avalonia.Styling;

namespace TSSE.Desktop.Services;

public sealed class AvaloniaThemeService(App application) : IThemeService
{
    public bool IsDark { get; private set; } = true;

    public void SetDark(bool isDark)
    {
        IsDark = isDark;
        application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
