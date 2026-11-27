using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace TSSE.Core.IO;

public enum RecoveryPayloadState
{
    Valid,
    Invalid,
    Unavailable
}

public enum RecoveryPreviewAction
{
    None,
    CopyExactBytesToSeparateFile
}

public sealed record RecoveryPayloadPreview(
    string Path,
    AtomicRecoveryArtifactKind? ArtifactKind,
    RecoveryPayloadState State,
    long? LengthBytes,
    string? Sha256,
    string? ErrorCode,
    RecoveryPreviewAction ProposedAction);

public sealed record AtomicFileRecoveryPreviewResult(
    bool IsComplete,
    bool? TargetExists,
    RecoveryPayloadPreview? Target,
    IReadOnlyList<RecoveryPayloadPreview> Candidates,
    string? ErrorCode);

/// <summary>
/// Reads and validates atomic transaction artifacts and describes a possible exact-copy recovery.
/// It never writes, replaces, renames, or deletes a file.
/// </summary>
public sealed class AtomicFileRecoveryPreviewer
{
    private readonly AtomicFileRecoveryInspector _inspector;

    public AtomicFileRecoveryPreviewer(AtomicFileRecoveryInspector? inspector = null)
    {
        _inspector = inspector ?? new AtomicFileRecoveryInspector();
    }

    /// <summary>
    /// Validates the current target and each recognized transaction artifact. A validator returns null
    /// for valid bytes or a stable error code for rejected bytes. Validation exceptions are reported.
    /// </summary>
    public async Task<AtomicFileRecoveryPreviewResult> AnalyzeAsync(
        string targetPath,
        Func<ReadOnlyMemory<byte>, string?> validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(validator);
        cancellationToken.ThrowIfCancellationRequested();

        AtomicRecoveryScanResult inventory = _inspector.Inspect(targetPath);
        if (!inventory.IsComplete)
        {
            return new AtomicFileRecoveryPreviewResult(
                false,
                null,
                null,
                Array.Empty<RecoveryPayloadPreview>(),
                inventory.ErrorCode ?? "inventory_incomplete");
        }

        string fullTargetPath;
        try
        {
            fullTargetPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new AtomicFileRecoveryPreviewResult(
                false,
                null,
                null,
                Array.Empty<RecoveryPayloadPreview>(),
                "target_path_invalid");
        }

        bool targetExists;
        try
        {
            // File.Exists hides access and I/O errors by returning false, which
            // could incorrectly describe an inaccessible original as absent.
            _ = File.GetAttributes(fullTargetPath);
            targetExists = true;
        }
        catch (FileNotFoundException)
        {
            // If the parent disappeared after inventory, absence is not known.
            if (!Directory.Exists(Path.GetDirectoryName(fullTargetPath)))
                return new AtomicFileRecoveryPreviewResult(false, null, null,
                    Array.Empty<RecoveryPayloadPreview>(), "target_attributes_unavailable");
            targetExists = false;
        }
        catch (DirectoryNotFoundException)
        {
            return new AtomicFileRecoveryPreviewResult(false, null, null,
                Array.Empty<RecoveryPayloadPreview>(), "target_attributes_unavailable");
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new AtomicFileRecoveryPreviewResult(false, null, null,
                Array.Empty<RecoveryPayloadPreview>(), "target_attributes_unavailable");
        }
        RecoveryPayloadPreview? target = targetExists
            ? await InspectPayloadAsync(fullTargetPath, null, validator, cancellationToken).ConfigureAwait(false)
            : null;

        var candidates = new List<RecoveryPayloadPreview>(inventory.Artifacts.Count);
        foreach (AtomicRecoveryArtifact artifact in inventory.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (artifact.ErrorCode is not null)
            {
                candidates.Add(Unavailable(artifact.Path, artifact.Kind, artifact.ErrorCode, artifact.LengthBytes));
                continue;
            }

            candidates.Add(await InspectPayloadAsync(
                artifact.Path,
                artifact.Kind,
                validator,
                cancellationToken).ConfigureAwait(false));
        }

        bool complete = (target is null || target.State != RecoveryPayloadState.Unavailable)
            && candidates.All(candidate => candidate.State != RecoveryPayloadState.Unavailable);
        return new AtomicFileRecoveryPreviewResult(
            complete,
            targetExists,
            target,
            new ReadOnlyCollection<RecoveryPayloadPreview>(candidates),
            complete ? null : "payload_analysis_incomplete");
    }

    private static async Task<RecoveryPayloadPreview> InspectPayloadAsync(
        string path,
        AtomicRecoveryArtifactKind? kind,
        Func<ReadOnlyMemory<byte>, string?> validator,
        CancellationToken cancellationToken)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (FileSystemPathSafety.ContainsReparsePointInPath(fullPath))
                return Unavailable(fullPath, kind, "payload_path_contains_link");

            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return Unavailable(fullPath, kind, "payload_is_link");
            if ((attributes & FileAttributes.Directory) != 0)
                return Unavailable(fullPath, kind, "payload_is_directory");

            var info = new FileInfo(fullPath);
            info.Refresh();
            if (!info.Exists)
                return Unavailable(fullPath, kind, "payload_missing");
            if (info.Length > AtomicFileTransaction.MaximumPayloadBytes)
                return Unavailable(fullPath, kind, "payload_size_exceeds_limit", info.Length);

            ReadOnlyMemory<byte> bytes;
            await using (var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                bytes = await ReadBoundedBytesAsync(
                    stream,
                    AtomicFileTransaction.MaximumPayloadBytes,
                    cancellationToken).ConfigureAwait(false);
            }

            if (bytes.IsEmpty)
                return Invalid(fullPath, kind, bytes, "payload_empty");

            if (FileSystemPathSafety.ContainsReparsePointInPath(fullPath))
                return Unavailable(fullPath, kind, "payload_path_changed_during_read", bytes.Length);

            string originalHash = Hash(bytes.Span);

            string? validationErrorCode;
            try
            {
                validationErrorCode = validator(bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Invalid(fullPath, kind, bytes, "payload_validation_failed");
            }

            if (!string.Equals(originalHash, Hash(bytes.Span), StringComparison.Ordinal))
                return Invalid(fullPath, kind, bytes, "payload_mutated_during_validation", originalHash);

            if (!string.IsNullOrWhiteSpace(validationErrorCode))
                return Invalid(fullPath, kind, bytes, validationErrorCode);

            return new RecoveryPayloadPreview(
                fullPath,
                kind,
                RecoveryPayloadState.Valid,
                bytes.Length,
                originalHash,
                null,
                kind.HasValue ? RecoveryPreviewAction.CopyExactBytesToSeparateFile : RecoveryPreviewAction.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return Unavailable(path, kind, "payload_size_exceeds_limit");
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return Unavailable(path, kind, "payload_read_failed");
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadBoundedBytesAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            long remaining = maximumBytes - output.Length;
            int readLimit = (int)Math.Min(buffer.Length, remaining + 1);
            int read = await source.ReadAsync(buffer.AsMemory(0, readLimit), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (!output.TryGetBuffer(out ArraySegment<byte> segment) || segment.Array is null)
                    throw new InvalidOperationException("The recovery preview buffer is unavailable.");

                return new ReadOnlyMemory<byte>(segment.Array, segment.Offset, checked((int)output.Length));
            }
            if (read > remaining)
                throw new InvalidDataException("Payload exceeds the configured recovery preview limit.");

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static RecoveryPayloadPreview Invalid(
        string path,
        AtomicRecoveryArtifactKind? kind,
        ReadOnlyMemory<byte> bytes,
        string errorCode,
        string? sha256 = null) =>
        new(path, kind, RecoveryPayloadState.Invalid, bytes.Length, sha256 ?? Hash(bytes.Span), errorCode, RecoveryPreviewAction.None);

    private static RecoveryPayloadPreview Unavailable(
        string path,
        AtomicRecoveryArtifactKind? kind,
        string errorCode,
        long? length = null) =>
        new(path, kind, RecoveryPayloadState.Unavailable, length, null, errorCode, RecoveryPreviewAction.None);

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool IsFileOperationFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            PlatformNotSupportedException or System.Security.SecurityException;
}
