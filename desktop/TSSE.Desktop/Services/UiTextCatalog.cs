using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace TSSE.Desktop.Services;

public sealed class UiTextCatalog : IUiTextCatalog
{
    private readonly IReadOnlyDictionary<string, string> _strings;

    public UiTextCatalog(CultureInfo? culture = null)
    {
        Culture = culture ?? CultureInfo.CurrentUICulture;
        var strings = ReadResource("en");
        if (Culture.TwoLetterISOLanguageName == "de")
        {
            foreach ((string key, string value) in ReadResource("de"))
                strings[key] = value;
        }

        _strings = strings;
        CurrentLanguageName = Get(Culture.TwoLetterISOLanguageName == "de" ? "language.german" : "language.english");
    }

    public CultureInfo Culture { get; }
    public string CurrentLanguageName { get; }

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _strings.TryGetValue(key, out string? value)
            ? value
            : throw new KeyNotFoundException($"UI text resource '{key}' is missing.");
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    private static Dictionary<string, string> ReadResource(string language)
    {
        Assembly assembly = typeof(UiTextCatalog).Assembly;
        string resourceName = $"{assembly.GetName().Name}.Resources.{language}.json";
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded UI text resource '{resourceName}' was not found.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Embedded UI text resource '{resourceName}' is invalid.");
    }
}
