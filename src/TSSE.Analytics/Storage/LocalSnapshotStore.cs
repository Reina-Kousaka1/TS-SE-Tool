using System.Text.Json;
using TSSE.Analytics.Models;
using TSSE.Core.IO;

namespace TSSE.Analytics.Storage;

/// <summary>Stores user-provided snapshot facts in one local JSON file; it has no network behavior.</summary>
public sealed class LocalSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly AtomicFileTransaction _transaction;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalSnapshotStore(string path, AtomicFileTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _transaction = transaction ?? new AtomicFileTransaction();
    }

    public async Task<IReadOnlyList<LocalBalanceSnapshot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSafeSnapshotPath();
            if (Directory.Exists(_path))
                throw new InvalidDataException("The local snapshot path points to a directory.");

            if (!File.Exists(_path))
                return Array.Empty<LocalBalanceSnapshot>();

            byte[] bytes;
            await using (var stream = OpenSnapshotFile())
            {
                bytes = await ReadBoundedBytesAsync(
                    stream,
                    AtomicFileTransaction.MaximumPayloadBytes,
                    cancellationToken).ConfigureAwait(false);
            }

            return Deserialize(bytes);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<FileCommitResult> AppendAsync(
        LocalBalanceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSafeSnapshotPath();
            if (Directory.Exists(_path))
                throw new InvalidDataException("The local snapshot path points to a directory.");

            string directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            EnsureSafeSnapshotPath();

            byte[]? original = null;
            if (File.Exists(_path))
            {
                try
                {
                    await using var stream = OpenSnapshotFile();
                    original = await ReadBoundedBytesAsync(
                        stream,
                        AtomicFileTransaction.MaximumPayloadBytes,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException)
                {
                    return new FileCommitResult(
                        FileCommitStatus.ValidationRejected,
                        ErrorCode: "snapshot_file_too_large");
                }
            }

            List<LocalBalanceSnapshot> all = original is null ? [] : Deserialize(original).ToList();

            if (all.Any(item => item.SnapshotId == snapshot.SnapshotId))
                return new FileCommitResult(FileCommitStatus.Conflict, ErrorCode: "snapshot_id_exists");

            all.Add(snapshot);
            byte[] updated = JsonSerializer.SerializeToUtf8Bytes(all, JsonOptions);
            string? expectedHash = original is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));

            return await _transaction.CommitAsync(
                _path,
                updated,
                expectedHash,
                IsValidJsonArray,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IReadOnlyList<LocalBalanceSnapshot> Deserialize(byte[] bytes)
    {
        List<LocalBalanceSnapshot?> snapshots;
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The local snapshot file must contain a JSON array.");

            snapshots = JsonSerializer.Deserialize<List<LocalBalanceSnapshot?>>(bytes, JsonOptions)
                ?? throw new InvalidDataException("The local snapshot file is invalid.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The local snapshot file contains malformed JSON.", error);
        }

        var snapshotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LocalBalanceSnapshot? snapshot in snapshots)
        {
            if (snapshot is null)
                throw new InvalidDataException("The local snapshot file contains an empty entry.");

            try
            {
                ValidateSnapshot(snapshot, requireStableProfileIdentity: false);
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException("A local snapshot entry is missing required identity data.", error);
            }

            if (!snapshotIds.Add(snapshot.SnapshotId))
                throw new InvalidDataException("The local snapshot file contains a duplicate snapshot ID.");
        }

        return snapshots.Select(snapshot => snapshot!).ToArray();
    }

    internal static async Task<byte[]> ReadBoundedBytesAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumBytes < 0 || maximumBytes > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (!source.CanRead)
            throw new ArgumentException("The source stream must be readable.", nameof(source));

        if (source.CanSeek)
        {
            long availableBytes = Math.Max(0, source.Length - source.Position);
            if (availableBytes > maximumBytes)
                throw SnapshotSizeLimitExceeded();

            byte[] bytes = new byte[checked((int)availableBytes)];
            int bytesRead = 0;
            while (bytesRead < bytes.Length)
            {
                int read = await source.ReadAsync(bytes.AsMemory(bytesRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                bytesRead += read;
            }

            if (bytesRead < bytes.Length)
                throw new IOException("The local snapshot file changed while it was being read.");

            byte[] overflowProbe = new byte[1];
            if (await source.ReadAsync(overflowProbe.AsMemory(), cancellationToken).ConfigureAwait(false) > 0)
                throw SnapshotSizeLimitExceeded();

            return bytes;
        }

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[64 * 1024];
        while (true)
        {
            long remainingBytes = maximumBytes - buffer.Length;
            int readLimit = (int)Math.Min(chunk.Length, remainingBytes + 1);
            int read = await source.ReadAsync(chunk.AsMemory(0, readLimit), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            if (read > remainingBytes)
                throw SnapshotSizeLimitExceeded();

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private FileStream OpenSnapshotFile() => new(
        _path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 64 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static InvalidDataException SnapshotSizeLimitExceeded() =>
        new("The local snapshot file exceeds the supported size limit.");

    private void EnsureSafeSnapshotPath()
    {
        if (FileSystemPathSafety.ContainsReparsePointInPath(_path))
            throw new IOException("The local snapshot path cannot contain symbolic links or reparse points.");

        string? directory = Path.GetDirectoryName(_path);
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(directory))
                throw new InvalidDataException("A parent component of the local snapshot path is a file.");
            if (Directory.Exists(directory))
                return;

            string? parent = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, directory, StringComparison.Ordinal))
                return;

            directory = parent;
        }
    }

    private static bool IsValidJsonArray(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateSnapshot(LocalBalanceSnapshot snapshot, bool requireStableProfileIdentity = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.SnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.GameId);
        if (requireStableProfileIdentity)
            ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ProfileLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.SaveVersion);
    }
}
