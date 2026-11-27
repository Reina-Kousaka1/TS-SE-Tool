using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using TSSE.Core.IO;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;

namespace TSSE.FixtureInspector;

/// <summary>Developer-only inspection of explicitly authorized test copies; never scans or writes files.</summary>
public static class FixtureCopyInspection
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public static int Run(string[] args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        if ((args.Length == 3 || args.Length == 4 && args[3] == "--include-field-names") &&
            args[0] == "--compare-approved-test-copies" &&
            !string.IsNullOrWhiteSpace(args[1]) && !string.IsNullOrWhiteSpace(args[2]))
            return CompareCopies(args[1], args[2], args.Length == 4, output);
        if (args.Length != 2 || args[0] != "--approved-test-copy" || string.IsNullOrWhiteSpace(args[1]))
        {
            output.WriteLine("Use --approved-test-copy <file> or --compare-approved-test-copies <before> <after> [--include-field-names] with explicitly approved copies.");
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

    private static int CompareCopies(string beforePath, string afterPath, bool includeNames, TextWriter output)
    {
        try
        {
            StableSyntax before = ReadStableSyntax(beforePath);
            StableSyntax after = ReadStableSyntax(afterPath);
            IReadOnlyList<SiiSyntaxDifference> differences = SiiSyntaxComparison.Compare(before.Syntax, after.Syntax);
            var counts = differences.GroupBy(item => item.Kind)
                .ToDictionary(group => group.Key.ToString(), group => group.Count(), StringComparer.Ordinal);
            string[] names = includeNames ? differences.Where(item => item.FieldName is not null)
                .Select(item => item.FieldName!).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)
                .Take(1001).ToArray() : [];
            output.WriteLine(JsonSerializer.Serialize(new {
                DifferenceCounts = counts,
                ByteIdentical = before.Bytes.AsSpan().SequenceEqual(after.Bytes),
                SyntaxComparisonIgnoresFormattingAndComments = true,
                FieldNames = includeNames ? names.Take(1000).ToArray() : null,
                FieldNamesTruncated = includeNames && names.Length > 1000,
                GameCompatibilityEstablished = false,
                SaveWritingEnabled = false
            }, JsonOptions));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or InvalidOperationException or FormatException)
        {
            output.WriteLine("Test copies could not be safely compared.");
            return 1;
        }
    }

    private static StableSyntax ReadStableSyntax(string suppliedPath)
    {
        string path = Path.GetFullPath(suppliedPath);
        if (FileSystemPathSafety.ContainsReparsePointInPath(path) ||
            (File.GetAttributes(path) & FileAttributes.Directory) != 0)
            throw new IOException("The supplied path is unsupported.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        FixtureObservation observation = ObserveTwice(stream);
        if (observation.ObservedSameBytesAcrossReads != true || observation.Report.Status != SiiFixtureStatus.ParsedSyntaxOnly)
            throw new InvalidOperationException("The supplied input is not comparable plain syntax.");
        stream.Position = 0;
        byte[] bytes = new byte[observation.Report.ByteLength];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), observation.Report.Sha256, StringComparison.Ordinal) ||
            FileSystemPathSafety.ContainsReparsePointInPath(path))
            throw new IOException("The supplied input changed during inspection.");
        return new(SiiEncodedText.Parse(bytes).Syntax, bytes);
    }

    private sealed record StableSyntax(SiiSyntaxDocument Syntax, byte[] Bytes);

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
