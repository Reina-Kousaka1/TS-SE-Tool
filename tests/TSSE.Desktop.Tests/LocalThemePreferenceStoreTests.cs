using TSSE.Desktop.Services;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class LocalThemePreferenceStoreTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Saves_and_reloads_theme_preference(bool isDark)
    {
        string directory = CreateTestDirectory();
        try
        {
            var store = new LocalThemePreferenceStore(directory);

            store.SaveIsDark(!isDark);
            store.SaveIsDark(isDark);

            Assert.Equal(isDark, store.LoadIsDark());
            Assert.Equal(new[] { "theme.preference" }, Directory.GetFiles(directory).Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Missing_preference_defaults_to_dark_without_creating_a_file()
    {
        string directory = CreateTestDirectory();
        try
        {
            var store = new LocalThemePreferenceStore(directory);

            Assert.True(store.LoadIsDark());
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Malformed_preference_is_reported_instead_of_silently_accepted()
    {
        string directory = CreateTestDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "theme.preference"), "maybe");
            var store = new LocalThemePreferenceStore(directory);

            Assert.Throws<InvalidDataException>(() => store.LoadIsDark());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTestDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "theme-preference-tests", Guid.NewGuid().ToString("N"));
}
