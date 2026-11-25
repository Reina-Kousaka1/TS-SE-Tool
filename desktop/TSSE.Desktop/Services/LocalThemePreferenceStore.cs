using System.Text;

namespace TSSE.Desktop.Services;

public sealed class LocalThemePreferenceStore : IThemePreferenceStore
{
    private readonly string? _directory;
    private readonly string? _preferencePath;

    public LocalThemePreferenceStore()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            return;

        _directory = Path.Combine(localData, "TS-SE Tool", "Next Generation");
        _preferencePath = Path.Combine(_directory, "theme.preference");
    }

    public LocalThemePreferenceStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _preferencePath = Path.Combine(directory, "theme.preference");
    }

    public bool LoadIsDark()
    {
        string preferencePath = _preferencePath
            ?? throw new InvalidOperationException("The operating system did not provide a local application data directory.");
        if (!File.Exists(preferencePath))
            return true;

        string value = File.ReadAllText(preferencePath, Encoding.UTF8).Trim();
        if (bool.TryParse(value, out bool isDark))
            return isDark;

        throw new InvalidDataException("The local theme preference is malformed.");
    }

    public void SaveIsDark(bool isDark)
    {
        string directory = _directory
            ?? throw new InvalidOperationException("The operating system did not provide a local application data directory.");
        string preferencePath = _preferencePath
            ?? throw new InvalidOperationException("The operating system did not provide a local application data directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $"theme.preference.{Guid.NewGuid():N}.tmp");

        try
        {
            byte[] contents = Encoding.UTF8.GetBytes(isDark ? "true\n" : "false\n");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, preferencePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
