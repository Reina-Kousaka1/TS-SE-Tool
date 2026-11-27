using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TSSE.Core.IO;
using Xunit;

namespace TSSE.Core.Tests;

public sealed class AtomicFileRecoveryPreviewerTests
{
    [Fact]
    public async Task Analyze_MissingParentDoesNotClaimTargetIsAbsent()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "missing", "profile.sii");
        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath, _ => null, TestContext.Current.CancellationToken);

        Assert.False(result.IsComplete);
        Assert.Null(result.TargetExists);
        Assert.Empty(result.Candidates);
        Assert.False(Directory.Exists(Path.GetDirectoryName(targetPath)));
    }

    [Fact]
    public async Task Analyze_OffersSeparateCopyForValidCandidateAndLeavesAllFilesUntouched()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        string candidatePath = Path.Combine(directory.Path, $".profile.sii.tsse-{Guid.NewGuid():N}.tmp");
        byte[] target = "CORRUPT"u8.ToArray();
        byte[] candidate = "VALID synthetic payload"u8.ToArray();
        await File.WriteAllBytesAsync(targetPath, target, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(candidatePath, candidate, TestContext.Current.CancellationToken);

        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath,
            bytes => bytes.Span.SequenceEqual("VALID synthetic payload"u8)
                ? null
                : "synthetic_validation_failed",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsComplete);
        Assert.Equal(true, result.TargetExists);
        Assert.Equal(RecoveryPayloadState.Invalid, result.Target!.State);
        Assert.Equal("synthetic_validation_failed", result.Target.ErrorCode);
        RecoveryPayloadPreview preview = Assert.Single(result.Candidates);
        Assert.Equal(RecoveryPayloadState.Valid, preview.State);
        Assert.Equal(RecoveryPreviewAction.CopyExactBytesToSeparateFile, preview.ProposedAction);
        Assert.Equal(candidate.LongLength, preview.LengthBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(candidate)), preview.Sha256);
        Assert.Equal(target, await File.ReadAllBytesAsync(targetPath, TestContext.Current.CancellationToken));
        Assert.Equal(candidate, await File.ReadAllBytesAsync(candidatePath, TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    [Fact]
    public async Task Analyze_DoesNotProposeRecoveryForInvalidCandidate()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        string candidatePath = Path.Combine(directory.Path, $".profile.sii.tsse-{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(candidatePath, "invalid"u8.ToArray(), TestContext.Current.CancellationToken);

        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath,
            _ => "synthetic_invalid",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsComplete);
        Assert.Equal(false, result.TargetExists);
        RecoveryPayloadPreview candidate = Assert.Single(result.Candidates);
        Assert.Equal(RecoveryPayloadState.Invalid, candidate.State);
        Assert.Equal("synthetic_invalid", candidate.ErrorCode);
        Assert.Equal(RecoveryPreviewAction.None, candidate.ProposedAction);
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public async Task Analyze_ReportsValidatorExceptionWithoutSuggestingAnAction()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        string candidatePath = Path.Combine(directory.Path, $".profile.sii.tsse-{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(candidatePath, "candidate"u8.ToArray(), TestContext.Current.CancellationToken);

        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath,
            _ => throw new InvalidDataException("synthetic validator failure"),
            TestContext.Current.CancellationToken);

        RecoveryPayloadPreview candidate = Assert.Single(result.Candidates);
        Assert.Equal(RecoveryPayloadState.Unavailable, candidate.State);
        Assert.Equal("payload_validation_failed", candidate.ErrorCode);
        Assert.Equal(RecoveryPreviewAction.None, candidate.ProposedAction);
        Assert.False(result.IsComplete);
        Assert.Equal("payload_analysis_incomplete", result.ErrorCode);
        Assert.Equal("candidate"u8.ToArray(), await File.ReadAllBytesAsync(candidatePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Analyze_DetectsValidatorMutationAndKeepsOriginalCandidateBytes()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        string candidatePath = Path.Combine(directory.Path, $".profile.sii.tsse-{Guid.NewGuid():N}.tmp");
        byte[] candidateBytes = "candidate"u8.ToArray();
        bool validatorMutatedBuffer = false;
        await File.WriteAllBytesAsync(candidatePath, candidateBytes, TestContext.Current.CancellationToken);

        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath,
            bytes =>
            {
                if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment) && segment.Array is not null)
                {
                    segment.Array[segment.Offset] = (byte)'X';
                    validatorMutatedBuffer = true;
                }
                return null;
            },
            TestContext.Current.CancellationToken);

        RecoveryPayloadPreview candidate = Assert.Single(result.Candidates);
        Assert.True(validatorMutatedBuffer);
        Assert.Equal(RecoveryPayloadState.Invalid, candidate.State);
        Assert.Equal("payload_mutated_during_validation", candidate.ErrorCode);
        Assert.Equal(RecoveryPreviewAction.None, candidate.ProposedAction);
        Assert.Equal(candidateBytes, await File.ReadAllBytesAsync(candidatePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Analyze_ReportsLinkCandidateUnavailableWithoutFollowingIt()
    {
        if (OperatingSystem.IsWindows() &&
            !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            return;

        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profile.sii");
        string outsidePath = Path.Combine(directory.Path, "outside.bin");
        string linkPath = Path.Combine(directory.Path, $".profile.sii.tsse-{Guid.NewGuid():N}.tmp");
        byte[] outsideBytes = "must remain untouched"u8.ToArray();
        await File.WriteAllBytesAsync(outsidePath, outsideBytes, TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(linkPath, outsidePath);

        AtomicFileRecoveryPreviewResult result = await new AtomicFileRecoveryPreviewer().AnalyzeAsync(
            targetPath,
            _ => null,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsComplete);
        Assert.Equal("payload_analysis_incomplete", result.ErrorCode);
        RecoveryPayloadPreview candidate = Assert.Single(result.Candidates);
        Assert.Equal(RecoveryPayloadState.Unavailable, candidate.State);
        Assert.Equal("artifact_is_link", candidate.ErrorCode);
        Assert.Equal(RecoveryPreviewAction.None, candidate.ProposedAction);
        Assert.Equal(outsideBytes, await File.ReadAllBytesAsync(outsidePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Analyze_PropagatesCancellationBeforeReadingCandidates()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AtomicFileRecoveryPreviewer().AnalyzeAsync(
                Path.Combine(directory.Path, "profile.sii"),
                _ => null,
                cancellation.Token));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsse-recovery-preview-tests-" + Guid.NewGuid().ToString("N"));
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
