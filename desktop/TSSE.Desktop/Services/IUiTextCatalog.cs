using System.Globalization;

namespace TSSE.Desktop.Services;

public interface IUiTextCatalog
{
    CultureInfo Culture { get; }
    string CurrentLanguageName { get; }
    string Get(string key);
    string Format(string key, params object[] arguments);
}
