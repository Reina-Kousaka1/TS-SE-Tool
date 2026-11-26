using System.Globalization;
using System.Security.Cryptography;
using TSSE.Core.IO;
using Xunit;

namespace TSSE.Core.Tests;

public sealed class AtomicFileRecoveryInspectorTests
{
    [Fact]
    public void Inspect_ListsOnlyExactTopLevelArtifactsAndLeavesThemUntouched()
    {
        using var directory = new TemporaryDirectory();
        string targetName = "profile [1].sii";
        string targetPath = Path.Combine(directory.Path, targetName);
        string stagedPath = Path.Combine(directory.Path, $".{targetName}.tsse-{Guid.NewGuid():N}.tmp");
        string backupTimestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        string backupPath = Path.Combine(
            directory.Path,
            $".{targetName}.tsse-{backupTimestamp}-{Guid.NewGuid():N}.bak");
        string unrelatedPath = Path.Combine(directory.Path, $".other.sii.tsse-{Guid.NewGuid():N}.tmp");
        string nestedDirectory = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nestedDirectory);
        string nestedArtifactPath = Path.Combine(nestedDirectory, $".{targetName}.tsse-{Guid.NewGuid():N}.tmp");

        byte[] stagedContents = "uncommitted synthetic payload"u8.ToArray();
        byte[] backupContents = "previous synthetic target"u8.ToArray();
        File.WriteAllBytes(stagedPath, stagedContents);
        File.WriteAllBytes(backupPath, backupContents);
        File.WriteAllText(unrelatedPath, "not for this target");
        File.WriteAllText(nestedArtifactPath, "not in the target directory");

        AtomicRecoveryScanResult result = new AtomicFileRecoveryInspector().Inspect(targetPath);

        Assert.True(result.IsComplete);
        Assert.Null(result.ErrorCode);
        AtomicRecoveryArtifact staged = Assert.Single(
            result.Artifacts,
            artifact => artifact.Kind == AtomicRecoveryArtifactKind.StagedPayloadCandidate);
        AtomicRecoveryArtifact backup = Assert.Single(
            result.Artifacts,
            artifact => artifact.Kind == AtomicRecoveryArtifactKind.PreviousTargetBackupCandidate);
        Assert.Equal(stagedPath, staged.Path);
        Assert.Equal((long?)stagedContents.Length, staged.LengthBytes);
        Assert.Null(staged.ErrorCode);
        Assert.Equal(backupPath, backup.Path);
        Assert.Equal((long?)backupContents.Length, backup.LengthBytes);
        Assert.Null(backup.ErrorCode);
        Assert.NotNull(staged.LastWriteTimeUtc);
        Assert.NotNull(backup.LastWriteTimeUtc);
        Assert.Equal(stagedContents, File.ReadAllBytes(stagedPath));
        Assert.Equal(backupContents, File.ReadAllBytes(backupPath));
    }

    [Fact]
    public async Task Inspect_FindsBackupCreatedByAtomicFileTransaction()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        byte[] original = "previous synthetic target"u8.ToArray();
        await File.WriteAllBytesAsync(targetPath, original, TestContext.Current.CancellationToken);

        FileCommitResult commit = await new AtomicFileTransaction().CommitAsync(
            targetPath,
            "updated synthetic target"u8.ToArray(),
            Convert.ToHexString(SHA256.HashData(original)),
            _ => true,
            TestContext.Current.CancellationToken);

        AtomicRecoveryScanResult inventory = new AtomicFileRecoveryInspector().Inspect(targetPath);

        Assert.Equal(FileCommitStatus.Succeeded, commit.Status);
        Assert.NotNull(commit.BackupPath);
        Assert.True(inventory.IsComplete);
        AtomicRecoveryArtifact backup = Assert.Single(
            inventory.Artifacts,
            artifact => artifact.Kind == AtomicRecoveryArtifactKind.PreviousTargetBackupCandidate);
        Assert.Equal(commit.BackupPath, backup.Path);
        Assert.Equal((long?)original.Length, backup.LengthBytes);
    }

    [Fact]
    public void Inspect_ReportsMissingDirectoryWithoutCreatingIt()
    {
        using var directory = new TemporaryDirectory();
        string missingDirectory = Path.Combine(directory.Path, "missing");

        AtomicRecoveryScanResult result = new AtomicFileRecoveryInspector().Inspect(
            Path.Combine(missingDirectory, "profile.sii"));

        Assert.False(result.IsComplete);
        Assert.Equal("target_directory_missing", result.ErrorCode);
        Assert.Empty(result.Artifacts);
        Assert.False(Directory.Exists(missingDirectory));
    }

    [Fact]
    public void Inspect_StopsAtArtifactLimitAndMarksInventoryIncomplete()
    {
        using var directory = new TemporaryDirectory();
        const string targetName = "profile.sii";
        string targetPath = Path.Combine(directory.Path, targetName);
        for (int i = 0; i <= AtomicFileRecoveryInspector.MaximumArtifacts; i++)
        {
            string artifactPath = Path.Combine(directory.Path, $".{targetName}.tsse-{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(artifactPath, []);
        }

        AtomicRecoveryScanResult result = new AtomicFileRecoveryInspector().Inspect(targetPath);

        Assert.False(result.IsComplete);
        Assert.Equal("artifact_limit_exceeded", result.ErrorCode);
        Assert.Equal(AtomicFileRecoveryInspector.MaximumArtifacts, result.Artifacts.Count);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsse-recovery-tests-" + Guid.NewGuid().ToString("N"));
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
