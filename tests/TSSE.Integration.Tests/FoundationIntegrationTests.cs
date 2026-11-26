using TSSE.Analytics;
using TSSE.Analytics.Models;
using TSSE.Analytics.Storage;
using TSSE.Core.IO;
using TSSE.GameAdapters.Compatibility;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class FoundationIntegrationTests
{
    [Fact]
    public void UnknownGameVersion_IsUnsupportedAndCannotWrite()
    {
        var registry = new GameAdapterRegistry();

        CompatibilityAssessment support = registry.Assess("ETS2", "unverified-v97");

        Assert.Equal(CompatibilityState.UnsupportedUnknown, support.State);
        Assert.False(support.CanRead);
        Assert.False(support.CanWrite);
        Assert.Equal(0, registry.RegistrationCount);
    }

    [Fact]
    public void VerifiedRegistration_RejectsEntryWithoutReadSupport()
    {
        var registry = new GameAdapterRegistry();

        Assert.Throws<ArgumentException>(() => registry.RegisterVerified(
            new GameAdapterRegistration("ETS2", "synthetic-v1", CanRead: false, CanWrite: false, "synthetic-test-evidence")));

        Assert.Equal(0, registry.RegistrationCount);
        Assert.Equal(CompatibilityState.UnsupportedUnknown, registry.Assess("ETS2", "synthetic-v1").State);
    }

    [Fact]
    public void VerifiedReadOnlyRegistration_DoesNotEnableWritesOrCarryOverToAnotherVersion()
    {
        var registry = new GameAdapterRegistry();
        registry.RegisterVerified(new GameAdapterRegistration(
            "ETS2", "synthetic-v1", CanRead: true, CanWrite: false, "synthetic-test-evidence"));

        CompatibilityAssessment verified = registry.Assess("ets2", "SYNTHETIC-V1");
        CompatibilityAssessment unknownVersion = registry.Assess("ETS2", "synthetic-v2");

        Assert.Equal(CompatibilityState.ReadOnlyVerified, verified.State);
        Assert.True(verified.CanRead);
        Assert.False(verified.CanWrite);
        Assert.Equal(CompatibilityState.UnsupportedUnknown, unknownVersion.State);
        Assert.False(unknownVersion.CanRead);
        Assert.False(unknownVersion.CanWrite);
        Assert.Equal(1, registry.RegistrationCount);
    }

    [Fact]
    public void VerifiedRegistration_RejectsWriteWithoutReadSupport()
    {
        var registry = new GameAdapterRegistry();

        Assert.Throws<ArgumentException>(() => registry.RegisterVerified(
            new GameAdapterRegistration("ETS2", "synthetic-v1", CanRead: false, CanWrite: true, "synthetic-test-evidence")));

        Assert.Equal(0, registry.RegistrationCount);
    }

    [Fact]
    public void Analytics_SeparatesRecordedTransactionsFromSnapshotDeltas()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var transactions = new[]
        {
            new RecordedTransaction(end.AddDays(-3), 500m, "synthetic-income"),
            new RecordedTransaction(end.AddDays(-2), -100m, "synthetic-expense")
        };
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("s1", "ETS2", "synthetic", "unknown", end.AddDays(-7), 200m, 1000m, 1500m, "profile-1"),
            new LocalBalanceSnapshot("s2", "ETS2", "synthetic", "unknown", end, 400m, 1300m, 1900m, "profile-1")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, transactions, snapshots);

        Assert.Equal(500m, report.RecordedIncome);
        Assert.Equal(100m, report.RecordedExpenses);
        Assert.Equal(400m, report.RecordedNet);
        Assert.Equal(300m, report.BalanceDelta);
        Assert.Equal(400m, report.NetWorthDelta);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    public void Analytics_UsesInclusiveWindowBoundariesAndExcludesAdjacentData(int days)
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset start = end.AddDays(-days);
        var transactions = new[]
        {
            new RecordedTransaction(start, 120m, "synthetic-boundary-income"),
            new RecordedTransaction(end, -20m, "synthetic-boundary-expense"),
            new RecordedTransaction(start.AddTicks(-1), 1_000m, "outside-before-window"),
            new RecordedTransaction(end.AddTicks(1), -1_000m, "outside-after-window")
        };
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("start", "ETS2", "synthetic", "synthetic-v1", start, 100m, 1_000m, 1_500m, "profile-1"),
            new LocalBalanceSnapshot("end", "ETS2", "synthetic", "synthetic-v1", end, 300m, 1_250m, 1_900m, "profile-1"),
            new LocalBalanceSnapshot("before", "ETS2", "other", "synthetic-v1", start.AddTicks(-1), 5_000m, 9_000m, 10_000m, "profile-before"),
            new LocalBalanceSnapshot("after", "ETS2", "other", "synthetic-v1", end.AddTicks(1), 6_000m, 10_000m, 11_000m, "profile-after")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, days, transactions, snapshots);

        Assert.Equal(start, report.Start);
        Assert.Equal(end, report.End);
        Assert.Equal(2, report.RecordedTransactionCount);
        Assert.Equal(120m, report.RecordedIncome);
        Assert.Equal(20m, report.RecordedExpenses);
        Assert.Equal(100m, report.RecordedNet);
        Assert.Equal(250m, report.BalanceDelta);
        Assert.Equal(400m, report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenInputContainsMultipleProfilesWithSameLabel()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("a1", "ETS2", "same-label", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m, "profile-a"),
            new LocalBalanceSnapshot("b1", "ETS2", "same-label", "unknown", end.AddDays(-1), 900m, 9_000m, 9_500m, "profile-b")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenStableProfileIdIsReusedAcrossGames()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("s1", "ETS2", "same-label", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m, "profile-1"),
            new LocalBalanceSnapshot("s2", "ATS", "same-label", "unknown", end, 900m, 9_000m, 9_500m, "profile-1")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenEarliestTimestampIsAmbiguous()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("s1", "ETS2", "profile", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m, "profile-1"),
            new LocalBalanceSnapshot("s2", "ETS2", "profile", "unknown", end.AddDays(-2), 200m, 1_500m, 1_700m, "profile-1"),
            new LocalBalanceSnapshot("s3", "ETS2", "profile", "unknown", end, 400m, 1_900m, 2_100m, "profile-1")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenLatestTimestampIsAmbiguous()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("s1", "ETS2", "profile", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m, "profile-1"),
            new LocalBalanceSnapshot("s2", "ETS2", "profile", "unknown", end, 300m, 1_800m, 2_000m, "profile-1"),
            new LocalBalanceSnapshot("s3", "ETS2", "profile", "unknown", end, 400m, 1_900m, 2_100m, "profile-1")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_UsesStableProfileIdWhenItsDisplayLabelChanges()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("s1", "ETS2", "old-label", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m, "profile-1"),
            new LocalBalanceSnapshot("s2", "ETS2", "current-label", "unknown", end, 400m, 1_300m, 1_600m, "profile-1")
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Equal(300m, report.BalanceDelta);
        Assert.Equal(400m, report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenStableProfileIdentityIsMissing()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("old-a", "ETS2", "same-label", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m),
            new LocalBalanceSnapshot("old-b", "ETS2", "same-label", "unknown", end, 200m, 1_500m, 1_700m)
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public async Task SnapshotStore_PersistsOnlyLocalSyntheticSnapshots()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tsse-snapshots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "history.json");
            var store = new LocalSnapshotStore(path);
            var snapshot = new LocalBalanceSnapshot(
                "snapshot-1", "ETS2", "synthetic", "unknown",
                DateTimeOffset.Parse("2026-11-25T12:00:00Z"), 10m, 20m, null, "profile-1");

            FileCommitResult result = await store.AppendAsync(snapshot, TestContext.Current.CancellationToken);
            IReadOnlyList<LocalBalanceSnapshot> readBack = await store.ReadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(FileCommitStatus.Succeeded, result.Status);
            Assert.Single(readBack);
            Assert.Equal(snapshot, readBack[0]);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotStore_RejectsNewSnapshotsWithoutStableProfileIdentity()
    {
        string path = Path.Combine(Path.GetTempPath(), "tsse-snapshot-" + Guid.NewGuid().ToString("N"), "history.json");
        var store = new LocalSnapshotStore(path);
        var snapshot = new LocalBalanceSnapshot(
            "snapshot-legacy", "ETS2", "synthetic", "unknown",
            DateTimeOffset.Parse("2026-11-25T12:00:00Z"), 10m, 20m, null);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.AppendAsync(snapshot, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SnapshotStore_ReadsLegacySnapshotsWithoutStableProfileIdentity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tsse-legacy-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "history.json");
            await File.WriteAllTextAsync(path,
                "[{\"snapshotId\":\"old-1\",\"gameId\":\"ETS2\",\"profileLabel\":\"old\",\"saveVersion\":\"unknown\",\"capturedAt\":\"2026-11-25T12:00:00Z\",\"cash\":10,\"bankBalance\":20,\"netWorth\":null}]",
                TestContext.Current.CancellationToken);

            IReadOnlyList<LocalBalanceSnapshot> snapshots = await new LocalSnapshotStore(path)
                .ReadAsync(TestContext.Current.CancellationToken);

            LocalBalanceSnapshot snapshot = Assert.Single(snapshots);
            Assert.Null(snapshot.ProfileId);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[null]")]
    [InlineData("[{\"snapshotId\":\"duplicate\",\"gameId\":\"ETS2\",\"profileLabel\":\"profile\",\"saveVersion\":\"v1\",\"capturedAt\":\"2026-11-25T12:00:00Z\"},{\"snapshotId\":\"duplicate\",\"gameId\":\"ETS2\",\"profileLabel\":\"profile\",\"saveVersion\":\"v1\",\"capturedAt\":\"2026-11-26T12:00:00Z\"}]")]
    [InlineData("[{\"snapshotId\":\"missing-game\",\"profileLabel\":\"profile\",\"saveVersion\":\"v1\",\"capturedAt\":\"2026-11-25T12:00:00Z\"}]")]
    public async Task SnapshotStore_RejectsInvalidEntriesWithoutOverwritingHistory(string contents)
    {
        string directory = Path.Combine(Path.GetTempPath(), "tsse-invalid-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "history.json");
            await File.WriteAllTextAsync(path, contents, TestContext.Current.CancellationToken);
            var store = new LocalSnapshotStore(path);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.AppendAsync(CreateSyntheticSnapshot(), TestContext.Current.CancellationToken));

            Assert.Equal(contents, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotStore_RejectsSymbolicLinkTargetBeforeReadingOrAppending()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        string directory = Path.Combine(Path.GetTempPath(), "tsse-linked-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string? linkPath = null;
        try
        {
            string actualPath = Path.Combine(directory, "actual.json");
            linkPath = Path.Combine(directory, "history.json");
            await File.WriteAllTextAsync(actualPath, "[]", TestContext.Current.CancellationToken);
            File.CreateSymbolicLink(linkPath, actualPath);
            var store = new LocalSnapshotStore(linkPath);

            await Assert.ThrowsAsync<IOException>(
                () => store.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<IOException>(
                () => store.AppendAsync(CreateSyntheticSnapshot(), TestContext.Current.CancellationToken));

            Assert.Equal("[]", await File.ReadAllTextAsync(actualPath, TestContext.Current.CancellationToken));
            Assert.Equal(actualPath, new FileInfo(linkPath).LinkTarget);
        }
        finally
        {
            if (linkPath is not null)
                File.Delete(linkPath);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotStore_RejectsDanglingSymbolicLinkInsteadOfTreatingItAsMissing()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        string directory = Path.Combine(Path.GetTempPath(), "tsse-dangling-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string? linkPath = null;
        try
        {
            linkPath = Path.Combine(directory, "history.json");
            string missingTargetPath = Path.Combine(directory, "missing.json");
            File.CreateSymbolicLink(linkPath, missingTargetPath);
            var store = new LocalSnapshotStore(linkPath);

            await Assert.ThrowsAsync<IOException>(
                () => store.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<IOException>(
                () => store.AppendAsync(CreateSyntheticSnapshot(), TestContext.Current.CancellationToken));

            Assert.Equal(missingTargetPath, new FileInfo(linkPath).LinkTarget);
            Assert.False(File.Exists(missingTargetPath));
        }
        finally
        {
            if (linkPath is not null)
                File.Delete(linkPath);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotStore_RejectsSymbolicLinkAncestorBeforeCreatingDirectories()
    {
        if (SkipWindowsLinkTestsOutsideGitHubActions())
            return;

        string directory = Path.Combine(Path.GetTempPath(), "tsse-linked-snapshot-parent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string? linkedDirectory = null;
        try
        {
            string actualDirectory = Path.Combine(directory, "actual");
            linkedDirectory = Path.Combine(directory, "linked");
            string wouldBeCreatedDirectory = Path.Combine(actualDirectory, "new");
            Directory.CreateDirectory(actualDirectory);
            Directory.CreateSymbolicLink(linkedDirectory, actualDirectory);
            var store = new LocalSnapshotStore(Path.Combine(linkedDirectory, "new", "history.json"));

            await Assert.ThrowsAsync<IOException>(
                () => store.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<IOException>(
                () => store.AppendAsync(CreateSyntheticSnapshot(), TestContext.Current.CancellationToken));

            Assert.False(Directory.Exists(wouldBeCreatedDirectory));
        }
        finally
        {
            if (linkedDirectory is not null && Directory.Exists(linkedDirectory))
                Directory.Delete(linkedDirectory);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SnapshotStore_RejectsFileInParentPathInsteadOfReportingEmptyHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tsse-file-snapshot-parent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string blockingFile = Path.Combine(directory, "blocking-file");
            await File.WriteAllTextAsync(blockingFile, "preserve", TestContext.Current.CancellationToken);
            var store = new LocalSnapshotStore(Path.Combine(blockingFile, "new", "history.json"));

            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.ReadAsync(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.AppendAsync(CreateSyntheticSnapshot(), TestContext.Current.CancellationToken));

            Assert.Equal("preserve", await File.ReadAllTextAsync(blockingFile, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static LocalBalanceSnapshot CreateSyntheticSnapshot() => new(
        "snapshot-new",
        "ETS2",
        "synthetic-profile",
        "unknown",
        DateTimeOffset.Parse("2026-11-26T12:00:00Z"),
        100m,
        200m,
        300m,
        "profile-new");

    private static bool SkipWindowsLinkTestsOutsideGitHubActions() =>
        OperatingSystem.IsWindows() &&
        !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
}
