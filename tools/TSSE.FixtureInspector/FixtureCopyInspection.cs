using System.Text.Json;
using System.Text.Json.Serialization;
using TSSE.Core.IO;
using TSSE.Savegames.Diagnostics;

namespace TSSE.FixtureInspector;

/// <summary>Developer-only inspection of one explicitly authorized test copy; never scans or writes files.</summary>
public static class FixtureCopyInspection
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public static int Run(string[] args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        if (args.Length != 2 || args[0] != "--approved-test-copy" || string.IsNullOrWhiteSpace(args[1]))
        {
            output.WriteLine("Specify exactly one explicitly approved test copy using --approved-test-copy <file>.");
            return 2;
        }

        try
        {
            string path = Path.GetFullPath(args[1]);
            if (FileSystemPathSafety.ContainsReparsePointInPath(path))
                throw new IOException("Linked paths are unsupported.");
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                throw new IOException("A file is required.");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            SiiFixtureReport report = SiiFixtureInspector.Inspect(stream);
            if (FileSystemPathSafety.ContainsReparsePointInPath(path))
                throw new IOException("The path changed during inspection.");
            output.WriteLine(JsonSerializer.Serialize(new { Report = report, GameCompatibilityEstablished = false, SaveWritingEnabled = false }, JsonOptions));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            // Do not disclose paths or file contents in error output.
            output.WriteLine("Test copy could not be safely inspected.");
            return 1;
        }
    }
}
