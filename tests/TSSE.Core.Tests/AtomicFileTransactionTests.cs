using System.Security.Cryptography;
using System.Runtime.InteropServices;
using TSSE.Core.IO;
using Xunit;

namespace TSSE.Core.Tests;

public sealed class AtomicFileTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidatorMutationCannotWriteOrCreateArtifacts(bool targetExists)
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "original"u8.ToArray();
        if (targetExists) await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);
        bool mutated = false;
        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path, "candidate"u8.ToArray(), targetExists ? Hash(original) : null,
            bytes =>
            {
                if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> buffer) && buffer.Array is not null)
                {
                    buffer.Array[buffer.Offset] = (byte)'X';
                    mutated = true;
                }
                return true;
            }, TestContext.Current.CancellationToken);
        Assert.True(mutated);
        Assert.Equal(FileCommitStatus.ValidationRejected, result.Status);
        Assert.Equal("validation_mutated_payload", result.ErrorCode);
        if (targetExists)
            Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        else Assert.False(File.Exists(path));
        Assert.Equal(targetExists ? 1 : 0, Directory.GetFiles(directory.Path).Length);
    }

    [Fact]
    public async Task HashStream_StopsAfterFirstByteBeyondConfiguredLimit()
    {
        using var source = new NonSeekableCountingStream("0123456789"u8.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => AtomicFileTransaction.HashStreamAsync(
                source,
                maximumBytes: 8,
                TestContext.Current.CancellationToken));

        Assert.Equal(9, source.BytesRead);
        Assert.False(source.IsDisposed);
    }

    [Fact]
    public async Task HashStream_AcceptsExactLimitAndReturnsSha256()
    {
        byte[] content = "12345678"u8.ToArray();
        using var source = new NonSeekableCountingStream(content);

        string hash = await AtomicFileTransaction.HashStreamAsync(
            source,
            maximumBytes: content.Length,
            TestContext.Current.CancellationToken);

        Assert.Equal(Hash(content), hash);
        Assert.Equal(content.Length, source.BytesRead);
        Assert.False(source.IsDisposed);
    }

    [Fact]
    public async Task Commit_ReplacesTargetAndKeepsOriginalBackup()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "old synthetic bytes"u8.ToArray();
        byte[] updated = "new synthetic bytes"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            updated,
            Hash(original),
            bytes => bytes.Span.SequenceEqual(updated),
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.Succeeded, result.Status);
        Assert.Equal(updated, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.NotNull(result.BackupPath);
        Assert.Equal(original, await File.ReadAllBytesAsync(result.BackupPath!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidationFailure_LeavesTargetByteIdentical()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "synthetic original"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            "invalid output"u8.ToArray(),
            Hash(original),
            _ => false,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.ValidationRejected, result.Status);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task ValidationException_LeavesTargetByteIdenticalAndCreatesNoArtifacts()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "synthetic original"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            "candidate bytes"u8.ToArray(),
            Hash(original),
            _ => throw new InvalidDataException("synthetic validation failure"),
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.ValidationRejected, result.Status);
        Assert.Equal("validation_failed", result.ErrorCode);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(path, Assert.Single(Directory.GetFiles(directory.Path)));
    }

    [Fact]
    public async Task Commit_ValidatesAndWritesTheSamePayloadSnapshot()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "new-snapshot.json");
        byte[] callerBuffer = "validated synthetic bytes"u8.ToArray();
        byte[] validatedPayload = callerBuffer.ToArray();

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            callerBuffer,
            expectedOriginalSha256: null,
            candidate =>
            {
                bool isExpectedPayload = candidate.Span.SequenceEqual(validatedPayload);
                callerBuffer[0] = (byte)'X';
                return isExpectedPayload;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.Succeeded, result.Status);
        Assert.Equal(validatedPayload, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StaleOriginalHash_RejectsConcurrentChange()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] expected = "before"u8.ToArray();
        byte[] concurrent = "concurrent change"u8.ToArray();
        await File.WriteAllBytesAsync(path, concurrent, TestContext.Current.CancellationToken);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            "new"u8.ToArray(),
            Hash(expected),
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.Conflict, result.Status);
        Assert.Equal(concurrent, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentChangeDuringReplace_RequiresRecoveryAndPreservesActualPreimage()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "expected original"u8.ToArray();
        byte[] concurrent = "concurrent update"u8.ToArray();
        byte[] updated = "new synthetic bytes"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        var transaction = new AtomicFileTransaction(new ConcurrentChangeReplacer(concurrent));
        FileCommitResult result = await transaction.CommitAsync(
            path,
            updated,
            Hash(original),
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.RecoveryRequired, result.Status);
        Assert.Equal("target_changed_during_replace", result.ErrorCode);
        Assert.NotNull(result.BackupPath);
        Assert.Equal(concurrent, await File.ReadAllBytesAsync(result.BackupPath!, TestContext.Current.CancellationToken));
        Assert.Equal(updated, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceFailure_ReportsUnchangedAndPreservesOriginal()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "synthetic original"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        var transaction = new AtomicFileTransaction(new ThrowingReplacer());
        FileCommitResult result = await transaction.CommitAsync(
            path,
            "new content"u8.ToArray(),
            Hash(original),
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PartialReplaceFailure_RequiresRecoveryAndKeepsOriginalBackup()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "synthetic.sii");
        byte[] original = "synthetic original"u8.ToArray();
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        var transaction = new AtomicFileTransaction(new PartiallyMutatingReplacer(original));
        FileCommitResult result = await transaction.CommitAsync(
            path,
            "new content"u8.ToArray(),
            Hash(original),
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.RecoveryRequired, result.Status);
        Assert.NotNull(result.BackupPath);
        Assert.NotNull(result.RecoveryArtifactPath);
        Assert.Equal(original, await File.ReadAllBytesAsync(result.BackupPath!, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(result.RecoveryArtifactPath));
        Assert.NotEqual(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NewTarget_IsCreatedWithoutOverwritingAnExistingFile()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "new-snapshot.json");
        byte[] contents = "[]"u8.ToArray();

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            contents,
            expectedOriginalSha256: null,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.Succeeded, result.Status);
        Assert.Equal(contents, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingParentDirectory_ReportsFailureWithoutCreatingFiles()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "missing", "synthetic.sii");

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            path,
            "candidate"u8.ToArray(),
            expectedOriginalSha256: null,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal("target_directory_missing", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SymbolicLinkAsTargetDirectory_RejectsCommitWithoutWritingThroughLink()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        using var directory = new TemporaryDirectory();
        string actualDirectory = Path.Combine(directory.Path, "actual");
        string linkedDirectory = Path.Combine(directory.Path, "linked");
        Directory.CreateDirectory(actualDirectory);
        Directory.CreateSymbolicLink(linkedDirectory, actualDirectory);
        string targetPath = Path.Combine(linkedDirectory, "synthetic.sii");

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            targetPath,
            "candidate"u8.ToArray(),
            expectedOriginalSha256: null,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal("target_directory_is_link", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(actualDirectory));
    }

    [Fact]
    public async Task SymbolicLinkInAncestorOfTargetDirectory_RejectsCommitWithoutWritingThroughLink()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        using var directory = new TemporaryDirectory();
        string actualDirectory = Path.Combine(directory.Path, "actual");
        string linkedAncestor = Path.Combine(directory.Path, "linked");
        string actualNestedDirectory = Path.Combine(actualDirectory, "nested");
        Directory.CreateDirectory(actualNestedDirectory);
        Directory.CreateSymbolicLink(linkedAncestor, actualDirectory);
        string targetPath = Path.Combine(linkedAncestor, "nested", "synthetic.sii");

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            targetPath,
            "candidate"u8.ToArray(),
            expectedOriginalSha256: null,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal("target_path_contains_link", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(actualNestedDirectory));
        AssertNoTransactionArtifacts(actualNestedDirectory);
    }

    [Fact]
    public async Task SymbolicLinkAsTargetFile_RejectsCommitWithoutWritingThroughLink()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        using var directory = new TemporaryDirectory();
        string actualPath = Path.Combine(directory.Path, "actual.sii");
        string linkPath = Path.Combine(directory.Path, "linked.sii");
        byte[] original = "synthetic original"u8.ToArray();
        await File.WriteAllBytesAsync(actualPath, original, TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(linkPath, actualPath);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            linkPath,
            "candidate"u8.ToArray(),
            Hash(original),
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal("target_is_link", result.ErrorCode);
        Assert.Equal(original, await File.ReadAllBytesAsync(actualPath, TestContext.Current.CancellationToken));
        AssertNoTransactionArtifacts(directory.Path);
    }

    [Fact]
    public async Task DanglingSymbolicLinkAsTarget_RejectsCommitWithoutCreatingTargetOrArtifacts()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        using var directory = new TemporaryDirectory();
        string linkPath = Path.Combine(directory.Path, "linked.sii");
        string missingTargetPath = Path.Combine(directory.Path, "missing-target.sii");
        File.CreateSymbolicLink(linkPath, missingTargetPath);

        FileCommitResult result = await new AtomicFileTransaction().CommitAsync(
            linkPath,
            "candidate"u8.ToArray(),
            expectedOriginalSha256: null,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileCommitStatus.FailedUnchanged, result.Status);
        Assert.Equal("target_is_link", result.ErrorCode);
        Assert.False(File.Exists(missingTargetPath));
        Assert.Equal(missingTargetPath, new FileInfo(linkPath).LinkTarget);
        AssertNoTransactionArtifacts(directory.Path);
    }

    private static void AssertNoTransactionArtifacts(string directoryPath)
    {
        string[] artifacts = Directory.GetFiles(directoryPath)
            .Where(path => path.EndsWith(".tmp", StringComparison.Ordinal) ||
                           path.EndsWith(".bak", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(artifacts);
    }

    private static bool SkipWindowsLinkTestsOutsideGitHubActions() =>
        OperatingSystem.IsWindows() &&
        !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class NonSeekableCountingStream(byte[] content) : Stream
    {
        private readonly MemoryStream _source = new(content);

        public long BytesRead { get; private set; }

        public bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _source.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _source.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingReplacer : IAtomicFileReplacer
    {
        public void Replace(string stagedPath, string targetPath, string backupPath) =>
            throw new IOException("synthetic injected replace failure");
    }

    private sealed class PartiallyMutatingReplacer(byte[] original) : IAtomicFileReplacer
    {
        public void Replace(string stagedPath, string targetPath, string backupPath)
        {
            File.WriteAllBytes(backupPath, original);
            File.WriteAllText(targetPath, "synthetic uncertain bytes");
            throw new IOException("synthetic failure after target mutation");
        }
    }

    private sealed class ConcurrentChangeReplacer(byte[] concurrent) : IAtomicFileReplacer
    {
        public void Replace(string stagedPath, string targetPath, string backupPath)
        {
            File.WriteAllBytes(targetPath, concurrent);
            byte[] stagedContents = File.ReadAllBytes(stagedPath);
            File.WriteAllBytes(backupPath, concurrent);
            File.WriteAllBytes(targetPath, stagedContents);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsse-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
