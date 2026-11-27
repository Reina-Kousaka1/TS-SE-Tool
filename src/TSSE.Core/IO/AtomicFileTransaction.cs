using System.Globalization;
using System.Security.Cryptography;

namespace TSSE.Core.IO;

/// <summary>
/// Snapshots, stages, and validates bytes before replacing a file. This is not connected to live game saves.
/// Callers must supply a validator and an expected hash for existing targets.
/// </summary>
public sealed class AtomicFileTransaction
{
    public const long MaximumPayloadBytes = 128L * 1024 * 1024;

    private readonly IAtomicFileReplacer _replacer;

    public AtomicFileTransaction(IAtomicFileReplacer? replacer = null)
    {
        _replacer = replacer ?? new PlatformAtomicFileReplacer();
    }

    public async Task<FileCommitResult> CommitAsync(
        string targetPath,
        ReadOnlyMemory<byte> newContents,
        string? expectedOriginalSha256,
        Func<ReadOnlyMemory<byte>, bool> validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(validator);

        if (newContents.IsEmpty || newContents.Length > MaximumPayloadBytes)
            return new FileCommitResult(FileCommitStatus.ValidationRejected, ErrorCode: "payload_size_invalid");

        byte[] payload = newContents.ToArray();
        ReadOnlyMemory<byte> stableContents = payload;
        // A ReadOnlyMemory view can still expose its backing array to a validator.
        // Never give the validator access to the snapshot that will be written.
        byte[] validationPayload = payload.ToArray();

        try
        {
            if (!validator(validationPayload))
                return new FileCommitResult(FileCommitStatus.ValidationRejected, ErrorCode: "validation_rejected");
            if (!validationPayload.AsSpan().SequenceEqual(payload))
                return new FileCommitResult(FileCommitStatus.ValidationRejected, ErrorCode: "validation_mutated_payload");
        }
        catch
        {
            return new FileCommitResult(FileCommitStatus.ValidationRejected, ErrorCode: "validation_failed");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_path_invalid");
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_directory_missing");

        try
        {
            if (FileSystemPathSafety.IsReparsePointIfPresent(directory))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_directory_is_link");

            if (FileSystemPathSafety.IsReparsePointIfPresent(fullPath))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_is_link");

            if (FileSystemPathSafety.ContainsReparsePointInPath(fullPath))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_path_contains_link");
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_attributes_unavailable");
        }

        if (!Directory.Exists(directory))
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_directory_missing");

        bool targetExists;
        try
        {
            FileAttributes targetAttributes = File.GetAttributes(fullPath);
            if ((targetAttributes & FileAttributes.Directory) != 0)
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_is_directory");
            targetExists = true;
        }
        catch (FileNotFoundException)
        {
            if (!Directory.Exists(directory))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_attributes_unavailable");
            targetExists = false;
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_attributes_unavailable");
        }

        if (targetExists != (expectedOriginalSha256 is not null))
            return new FileCommitResult(FileCommitStatus.Conflict, ErrorCode: "target_existence_changed");

        string? originalHash = null;
        try
        {
            if (targetExists)
            {
                var info = new FileInfo(fullPath);
                if (info.Length > MaximumPayloadBytes)
                    return new FileCommitResult(FileCommitStatus.ValidationRejected, ErrorCode: "original_size_invalid");

                originalHash = await HashFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
                if (!HashesEqual(originalHash, expectedOriginalSha256))
                    return new FileCommitResult(FileCommitStatus.Conflict, ErrorCode: "target_hash_changed");
            }
            else if (expectedOriginalSha256 is not null)
            {
                return new FileCommitResult(FileCommitStatus.Conflict, ErrorCode: "target_missing");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "target_read_failed");
        }

        string fileName = Path.GetFileName(fullPath);
        string stagedPath = Path.Combine(directory, $".{fileName}.tsse-{Guid.NewGuid():N}.tmp");
        string backupTimestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        string backupPath = Path.Combine(directory, $".{fileName}.tsse-{backupTimestamp}-{Guid.NewGuid():N}.bak");
        bool preserveStagedFile = false;

        try
        {
            await using (var stream = new FileStream(
                stagedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(stableContents, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            string stagedHash = await HashFileAsync(stagedPath, cancellationToken).ConfigureAwait(false);
            if (!HashesEqual(stagedHash, HashBytes(payload)))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: "staged_hash_mismatch");

            if (targetExists)
            {
                string currentHash = await HashFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
                if (!HashesEqual(currentHash, originalHash))
                    return new FileCommitResult(FileCommitStatus.Conflict, ErrorCode: "target_changed_before_replace");

                _replacer.Replace(stagedPath, fullPath, backupPath);

                if (!File.Exists(backupPath))
                    return new FileCommitResult(
                        FileCommitStatus.RecoveryRequired,
                        ErrorCode: "replacement_backup_missing");

                string replacedOriginalHash = await HashFileAsync(backupPath, cancellationToken).ConfigureAwait(false);
                if (!HashesEqual(replacedOriginalHash, originalHash))
                    return new FileCommitResult(
                        FileCommitStatus.RecoveryRequired,
                        BackupPath: backupPath,
                        ErrorCode: "target_changed_during_replace");
            }
            else
            {
                File.Move(stagedPath, fullPath, overwrite: false);
            }

            string committedHash = await HashFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!HashesEqual(committedHash, HashBytes(payload)))
                return new FileCommitResult(
                    FileCommitStatus.RecoveryRequired,
                    BackupPath: File.Exists(backupPath) ? backupPath : null,
                    ErrorCode: "post_commit_hash_mismatch");

            return new FileCommitResult(
                FileCommitStatus.Succeeded,
                BackupPath: targetExists && File.Exists(backupPath) ? backupPath : null);
        }
        catch (OperationCanceledException)
        {
            FileCommitResult result = GetFailureState(fullPath, stagedPath, backupPath, originalHash, targetExists, "cancelled");
            preserveStagedFile = result.Status == FileCommitStatus.RecoveryRequired && File.Exists(stagedPath);
            return result;
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            FileCommitResult result = GetFailureState(fullPath, stagedPath, backupPath, originalHash, targetExists, "replace_failed");
            preserveStagedFile = result.Status == FileCommitStatus.RecoveryRequired && File.Exists(stagedPath);
            return result;
        }
        finally
        {
            if (!preserveStagedFile)
                TryDeleteStagedFile(stagedPath);
        }
    }

    private static FileCommitResult GetFailureState(
        string targetPath,
        string stagedPath,
        string backupPath,
        string? originalHash,
        bool targetExisted,
        string errorCode)
    {
        try
        {
            if (!targetExisted && !File.Exists(targetPath))
                return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: errorCode);

            if (File.Exists(targetPath))
            {
                string currentHash = HashFileAsync(targetPath, CancellationToken.None).GetAwaiter().GetResult();
                if (targetExisted && HashesEqual(currentHash, originalHash))
                    return new FileCommitResult(FileCommitStatus.FailedUnchanged, ErrorCode: errorCode);
            }
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            // A failed verification is treated as an uncertain state below.
        }

        return new FileCommitResult(
            FileCommitStatus.RecoveryRequired,
            BackupPath: File.Exists(backupPath) ? backupPath : null,
            RecoveryArtifactPath: File.Exists(stagedPath) ? stagedPath : null,
            ErrorCode: errorCode);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await HashStreamAsync(stream, MaximumPayloadBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string> HashStreamAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (!source.CanRead)
            throw new ArgumentException("The source stream must be readable.", nameof(source));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long bytesHashed = 0;

        while (true)
        {
            long remainingBytes = maximumBytes - bytesHashed;
            int readLimit = (int)Math.Min(buffer.Length, remainingBytes >= buffer.Length ? buffer.Length : remainingBytes + 1);
            int bytesRead = await source.ReadAsync(buffer.AsMemory(0, readLimit), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break;

            if (bytesRead > remainingBytes)
                throw new InvalidDataException("The file exceeds the supported size limit.");

            hash.AppendData(buffer.AsSpan(0, bytesRead));
            bytesHashed += bytesRead;
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool HashesEqual(string? left, string? right) =>
        left is not null && right is not null &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsFileOperationFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            PlatformNotSupportedException or System.Security.SecurityException;

    private static void TryDeleteStagedFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (IsFileOperationFailure(ex))
        {
            // The uniquely named staged file is left in place for manual recovery.
        }
    }
}
