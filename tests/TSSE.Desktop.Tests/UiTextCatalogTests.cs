using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TSSE.Desktop.Services;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class UiTextCatalogTests
{
    [Fact]
    public void English_and_German_resources_have_the_same_keys()
    {
        using JsonDocument english = ReadResource("en");
        using JsonDocument german = ReadResource("de");

        string[] englishKeys = english.RootElement.EnumerateObject().Select(item => item.Name).Order().ToArray();
        string[] germanKeys = german.RootElement.EnumerateObject().Select(item => item.Name).Order().ToArray();

        Assert.Equal(englishKeys, germanKeys);
    }

    [Fact]
    public void English_and_German_resources_have_matching_format_arguments()
    {
        using JsonDocument english = ReadResource("en");
        using JsonDocument german = ReadResource("de");

        foreach (JsonProperty englishEntry in english.RootElement.EnumerateObject())
        {
            string germanText = german.RootElement.GetProperty(englishEntry.Name).GetString()!;
            Assert.Equal(FormatArguments(englishEntry.Value.GetString()!), FormatArguments(germanText));
        }
    }

    [Theory]
    [InlineData("en-US", "English", "Search sections")]
    [InlineData("de-DE", "Deutsch", "Bereiche suchen")]
    [InlineData("fr-FR", "English", "Search sections")]
    public void Catalog_uses_system_language_and_falls_back_to_English(
        string cultureName,
        string expectedLanguage,
        string expectedSearchText)
    {
        var catalog = new UiTextCatalog(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expectedLanguage, catalog.CurrentLanguageName);
        Assert.Equal(expectedSearchText, catalog.Get("shell.search"));
        Assert.Equal(expectedLanguage == "Deutsch" ? "Zeitraum: 30 Tage" : "30-day window selected",
            catalog.Format("analytics.window.selected", 30));
    }

    private static JsonDocument ReadResource(string language)
    {
        string assemblyName = typeof(UiTextCatalog).Assembly.GetName().Name!;
        string name = $"{assemblyName}.Resources.{language}.json";
        using Stream stream = typeof(UiTextCatalog).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Resource '{name}' was not embedded.");
        return JsonDocument.Parse(stream);
    }

    private static string[] FormatArguments(string value) =>
        Regex.Matches(value, @"\{(?<index>\d+)(?:,[^}]*)?(?::[^}]*)?\}")
            .Select(match => match.Groups["index"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
