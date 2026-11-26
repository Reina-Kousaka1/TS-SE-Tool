using System.Collections.ObjectModel;
using System.Globalization;

namespace TSSE.Core.IO;

public enum AtomicRecoveryArtifactKind
{
    StagedPayloadCandidate,
    PreviousTargetBackupCandidate
}

public sealed record AtomicRecoveryArtifact(
    string Path,
    AtomicRecoveryArtifactKind Kind,
    long? LengthBytes,
    DateTimeOffset? LastWriteTimeUtc,
    string? ErrorCode);

public sealed record AtomicRecoveryScanResult(
    bool IsComplete,
    IReadOnlyList<AtomicRecoveryArtifact> Artifacts,
    string? ErrorCode = null);

/// <summary>
/// Read-only inventory of the temporary and backup files created by <see cref="AtomicFileTransaction"/>.
/// It never reads artifact contents, restores files, or deletes anything.
/// </summary>
public sealed class AtomicFileRecoveryInspector
{
    public const int MaximumDirectoryEntries = 10_000;
    public const int MaximumArtifacts = 256;

    public AtomicRecoveryScanResult Inspect(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        string fullTargetPath;
        try
        {
            fullTargetPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return Result(isComplete: false, [], "target_path_invalid");
        }

        string? directory = Path.GetDirectoryName(fullTargetPath);
        string fileName = Path.GetFileName(fullTargetPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return Result(isComplete: false, [], "target_path_invalid");

        if (!Directory.Exists(directory))
            return Result(isComplete: false, [], "target_directory_missing");

        try
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                return Result(isComplete: false, [], "target_directory_is_link");

            if (Directory.Exists(fullTargetPath))
                return Result(isComplete: false, [], "target_is_directory");

            if (File.Exists(fullTargetPath) &&
                (File.GetAttributes(fullTargetPath) & FileAttributes.ReparsePoint) != 0)
                return Result(isComplete: false, [], "target_is_link");
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return Result(isComplete: false, [], "target_attributes_unavailable");
        }

        var artifacts = new List<AtomicRecoveryArtifact>();
        int entriesScanned = 0;

        try
        {
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(directory))
            {
                entriesScanned++;
                if (entriesScanned > MaximumDirectoryEntries)
                    return Result(isComplete: false, artifacts, "directory_entry_limit_exceeded");

                if (!TryClassify(entryPath, fileName, out AtomicRecoveryArtifactKind kind))
                    continue;

                if (artifacts.Count >= MaximumArtifacts)
                    return Result(isComplete: false, artifacts, "artifact_limit_exceeded");

                artifacts.Add(InspectArtifact(entryPath, kind));
            }
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return Result(isComplete: false, artifacts, "artifact_enumeration_failed");
        }

        return Result(isComplete: true, artifacts);
    }

    private static AtomicRecoveryArtifact InspectArtifact(string path, AtomicRecoveryArtifactKind kind)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return new AtomicRecoveryArtifact(path, kind, null, null, "artifact_is_link");

            if ((attributes & FileAttributes.Directory) != 0)
                return new AtomicRecoveryArtifact(path, kind, null, null, "artifact_is_directory");

            var info = new FileInfo(path);
            info.Refresh();
            if (!info.Exists)
                return new AtomicRecoveryArtifact(path, kind, null, null, "artifact_metadata_unavailable");

            if (info.Length > AtomicFileTransaction.MaximumPayloadBytes)
                return new AtomicRecoveryArtifact(
                    path,
                    kind,
                    info.Length,
                    new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                    "artifact_size_exceeds_limit");

            return new AtomicRecoveryArtifact(
                path,
                kind,
                info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                ErrorCode: null);
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new AtomicRecoveryArtifact(path, kind, null, null, "artifact_metadata_unavailable");
        }
    }

    private static bool TryClassify(string path, string targetFileName, out AtomicRecoveryArtifactKind kind)
    {
        kind = default;
        string name = System.IO.Path.GetFileName(path);
        string prefix = $".{targetFileName}.tsse-";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string artifactId = name[prefix.Length..];
        if (artifactId.EndsWith(".tmp", StringComparison.Ordinal))
        {
            string guid = artifactId[..^".tmp".Length];
            if (!Guid.TryParseExact(guid, "N", out _))
                return false;

            kind = AtomicRecoveryArtifactKind.StagedPayloadCandidate;
            return true;
        }

        if (!artifactId.EndsWith(".bak", StringComparison.Ordinal))
            return false;

        string backupId = artifactId[..^".bak".Length];
        const int timestampLength = 17;
        if (backupId.Length != timestampLength + 1 + 32 || backupId[timestampLength] != '-')
            return false;

        string timestamp = backupId[..timestampLength];
        string backupGuid = backupId[(timestampLength + 1)..];
        if (!DateTimeOffset.TryParseExact(
                timestamp,
                "yyyyMMddHHmmssfff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _) ||
            !Guid.TryParseExact(backupGuid, "N", out _))
            return false;

        kind = AtomicRecoveryArtifactKind.PreviousTargetBackupCandidate;
        return true;
    }

    private static AtomicRecoveryScanResult Result(
        bool isComplete,
        IEnumerable<AtomicRecoveryArtifact> artifacts,
        string? errorCode = null)
    {
        AtomicRecoveryArtifact[] ordered = artifacts
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        return new AtomicRecoveryScanResult(isComplete, new ReadOnlyCollection<AtomicRecoveryArtifact>(ordered), errorCode);
    }

    private static bool IsFileOperationFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            PlatformNotSupportedException or System.Security.SecurityException;
}
