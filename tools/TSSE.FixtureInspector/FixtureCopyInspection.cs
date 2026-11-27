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
            FixtureObservation observation = ObserveTwice(stream);
            if (FileSystemPathSafety.ContainsReparsePointInPath(path))
                throw new IOException("The path changed during inspection.");
            output.WriteLine(JsonSerializer.Serialize(new { observation.Report, observation.ObservedSameBytesAcrossReads, GameCompatibilityEstablished = false, SaveWritingEnabled = false }, JsonOptions));
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

    /// <summary>Compares two bounded reads of the same seekable stream; this is not an atomic filesystem snapshot.</summary>
    public static FixtureObservation ObserveTwice(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek) throw new ArgumentException("A seekable readable stream is required.", nameof(stream));
        long start = stream.Position;
        SiiFixtureReport first = SiiFixtureInspector.Inspect(stream);
        if (first.Sha256 is null) return new(first, null);
        stream.Position = start;
        SiiFixtureReport second = SiiFixtureInspector.Inspect(stream);
        if (second.Sha256 is null || first.ByteLength != second.ByteLength ||
            !string.Equals(first.Sha256, second.Sha256, StringComparison.Ordinal))
            throw new IOException("The supplied stream changed between reads.");
        return new(first, true);
    }
}

/// <summary>Evidence from bounded reads only; never establishes game compatibility or write authorization.</summary>
public sealed record FixtureObservation(SiiFixtureReport Report, bool? ObservedSameBytesAcrossReads);
