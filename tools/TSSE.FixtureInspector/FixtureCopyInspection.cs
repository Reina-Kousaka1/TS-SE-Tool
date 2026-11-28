using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using TSSE.Core.IO;
using TSSE.GameAdapters.Editing;
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
        if (args.Length == 2 && args[0] == "--exercise-binary-profile-preview" && !string.IsNullOrWhiteSpace(args[1]))
            return ExerciseBinaryProfilePreview(args[1], output);
        if ((args.Length == 3 || args.Length == 4 && args[3] == "--include-field-names") &&
            args[0] == "--compare-approved-test-copies" &&
            !string.IsNullOrWhiteSpace(args[1]) && !string.IsNullOrWhiteSpace(args[2]))
            return CompareCopies(args[1], args[2], args.Length == 4, output);
        if (args.Length != 2 || args[0] != "--approved-test-copy" || string.IsNullOrWhiteSpace(args[1]))
        {
            output.WriteLine("Use --approved-test-copy <file>, --exercise-binary-profile-preview <file>, or --compare-approved-test-copies <before> <after> [--include-field-names] with explicitly approved copies.");
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

    private static int ExerciseBinaryProfilePreview(string suppliedPath, TextWriter output)
    {
        byte[]? input = null;
        byte[]? preview = null;
        try
        {
            (byte[] Bytes, string Sha256) source = ReadStableCopy(suppliedPath);
            input = source.Bytes;
            string initialHash = source.Sha256;
            using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(input);
            if (decoded.Format != SiiPayloadFormat.Binary || decoded.BinaryBlocks is null)
                throw new InvalidOperationException("The supplied copy is not a decoded BSII save.");

            ExperimentalBinaryBlock[] economies = decoded.BinaryBlocks.Where(block => block.Type == "economy").ToArray();
            if (economies.Length != 1 || economies[0].Identifier is null)
                throw new InvalidOperationException("The binary economy selection is ambiguous or unavailable.");

            ReadOnlyMemory<byte> payloadSnapshot = decoded.Payload;
            try
            {
                var profile = new LegacyBinaryProfilePreview(payloadSnapshot.Span, economies[0].Identifier);
                ProfileValues originalValues = profile.Current;
                long changedBalance = originalValues.Balance == long.MaxValue
                    ? originalValues.Balance - 1 : originalValues.Balance + 1;
                uint changedExperience = originalValues.Experience >= uint.MaxValue - 1
                    ? originalValues.Experience - 1 : originalValues.Experience + 1;
                long? changedCoachExperience = NextCoachExperience(economies[0], originalValues.CoachExperience);
                profile.Apply(new ProfileValues(changedBalance, changedExperience, changedCoachExperience));
                preview = profile.PreviewBytes;
                var expectedChanges = new HashSet<string>(StringComparer.Ordinal) { "money_account", "experience_points" };
                if (changedCoachExperience != originalValues.CoachExperience)
                    expectedChanges.Add("bus_experience_points");
                bool valuesChanged = expectedChanges.SetEquals(profile.Changes.Select(change => change.FieldName));
                bool valueRedoMatches;
                // Apply creates the initial undo state; capture, undo, redo, and undo again to verify exact history bytes.
                valueRedoMatches = profile.Undo() && input.AsSpan().SequenceEqual(profile.PreviewBytes) &&
                    profile.Redo() && preview.AsSpan().SequenceEqual(profile.PreviewBytes) &&
                    profile.Undo() && input.AsSpan().SequenceEqual(profile.PreviewBytes);

                IReadOnlyDictionary<string, byte> skills = profile.CoachSkills;
                bool? skillRedoMatches = null;
                if (skills.Count > 0)
                {
                    KeyValuePair<string, byte> selected = skills.First();
                    byte nextRank = selected.Value == LegacyCoachSkillCatalog.MaximumRank
                        ? (byte)0 : checked((byte)(selected.Value + 1));
                    profile.SetCoachSkill(selected.Key, nextRank);
                    byte[] skillPreview = profile.PreviewBytes;
                    bool onlySelectedSkillChanged = profile.Changes.Count == 1 &&
                        profile.Changes[0].FieldName == selected.Key;
                    skillRedoMatches = onlySelectedSkillChanged && profile.Undo() &&
                        input.AsSpan().SequenceEqual(profile.PreviewBytes) && profile.Redo() &&
                        skillPreview.AsSpan().SequenceEqual(profile.PreviewBytes) && profile.Undo() &&
                        input.AsSpan().SequenceEqual(profile.PreviewBytes);
                    CryptographicOperations.ZeroMemory(skillPreview);
                }

                (byte[] afterPreview, string finalHash) = ReadStableCopy(suppliedPath);
                CryptographicOperations.ZeroMemory(afterPreview);
                bool sourceStable = string.Equals(initialHash, finalHash, StringComparison.Ordinal);
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    ProfileScalarsChangedOnlyInMemory = valuesChanged && valueRedoMatches,
                    MoneyXpUndoRedoRestoredExactPayload = valueRedoMatches,
                    ExistingCoachSkillCount = skills.Count,
                    ExistingCoachSkillUndoRedoRestoredExactPayload = skillRedoMatches,
                    SourceBytesStableAcrossPreview = sourceStable,
                    ContainerAuthenticationUnverified = decoded.ContainerAuthenticationUnverified,
                    GameCompatibilityEstablished = false,
                    SaveWritingEnabled = false
                }, JsonOptions));
                return sourceStable && valuesChanged && valueRedoMatches && (skills.Count == 0 || skillRedoMatches == true) ? 0 : 1;
            }
            finally
            {
                if (MemoryMarshal.TryGetArray(payloadSnapshot, out ArraySegment<byte> payloadSegment) && payloadSegment.Array is not null)
                    CryptographicOperations.ZeroMemory(payloadSegment.Array.AsSpan(payloadSegment.Offset, payloadSegment.Count));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or FormatException or InvalidOperationException or OverflowException)
        {
            output.WriteLine("Binary profile preview could not be safely completed.");
            return 1;
        }
        finally
        {
            if (input is not null) CryptographicOperations.ZeroMemory(input);
            if (preview is not null) CryptographicOperations.ZeroMemory(preview);
        }
    }

    private static long? NextCoachExperience(ExperimentalBinaryBlock economy, long? current)
    {
        if (current is null) return null;
        ExperimentalBinaryRawField field = economy.RawFields["bus_experience_points"];
        return field.TypeCode switch
        {
            0x25 => current == int.MaxValue ? current - 1 : current + 1,
            0x27 => current >= uint.MaxValue - 1 ? 0 : current + 1,
            _ => throw new InvalidOperationException("The existing Coaches experience field type is unsupported.")
        };
    }

    private static (byte[] Bytes, string Sha256) ReadStableCopy(string suppliedPath)
    {
        string path = Path.GetFullPath(suppliedPath);
        if (FileSystemPathSafety.ContainsReparsePointInPath(path) || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
            throw new IOException("Linked paths and directories are unsupported.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > SiiSaveContainerDecoder.MaximumFileBytes)
            throw new IOException("The supplied copy is empty or exceeds the inspection limit.");
        byte[] first = new byte[checked((int)stream.Length)];
        stream.ReadExactly(first);
        string firstHash = Convert.ToHexString(SHA256.HashData(first));
        stream.Position = 0;
        byte[] second = new byte[first.Length];
        stream.ReadExactly(second);
        string secondHash = Convert.ToHexString(SHA256.HashData(second));
        CryptographicOperations.ZeroMemory(second);
        if (stream.ReadByte() != -1 || !string.Equals(firstHash, secondHash, StringComparison.Ordinal) ||
            FileSystemPathSafety.ContainsReparsePointInPath(path))
        {
            CryptographicOperations.ZeroMemory(first);
            throw new IOException("The supplied copy changed during inspection.");
        }
        return (first, firstHash);
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
