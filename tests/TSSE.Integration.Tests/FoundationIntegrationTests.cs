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

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    public void InGameAnalytics_UsesGameTimeAndBuildsPerDaySnapshotChanges(int days)
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] snapshots = Enumerable.Range(0, days + 1)
            .Select(day => CreateInGameSnapshot(
                $"day-{day}", "profile-1", TimeSpan.FromDays(day), capturedAt.AddHours(day * 2),
                cash: 100m + day * 10m,
                bank: 1_000m + day * 100m,
                netWorth: 1_200m + day * 110m))
            .ToArray();

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(days), days, snapshots);

        Assert.Equal(InGameAnalyticsStatus.Available, report.Status);
        Assert.Equal(TimeSpan.Zero, report.StartGameTime);
        Assert.Equal(TimeSpan.FromDays(days), report.EndGameTime);
        Assert.Equal(days * 10m, report.SnapshotCashChange);
        Assert.Equal(days * 100m, report.SnapshotBankBalanceChange);
        Assert.Equal(days * 110m, report.SnapshotNetWorthChange);
        Assert.Equal(days, report.DailySummaries.Count);
        Assert.All(report.DailySummaries, day =>
        {
            Assert.Equal(2, day.SnapshotCount);
            Assert.Equal(10m, day.SnapshotCashChange);
            Assert.Equal(100m, day.SnapshotBankBalanceChange);
            Assert.Equal(110m, day.SnapshotNetWorthChange);
        });
    }

    [Fact]
    public void InGameAnalytics_IsolatesExactProfileAndGame()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] snapshots =
        [
            CreateInGameSnapshot("a", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("b", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(1), 130m, 1_040m, 1_250m),
            CreateInGameSnapshot("c", "profile-1", TimeSpan.FromDays(8), capturedAt.AddHours(2), 160m, 1_060m, 1_270m),
            CreateInGameSnapshot("other-profile-a", "profile-2", TimeSpan.FromDays(1), capturedAt.AddHours(2), 9_000m, 90_000m, 100_000m),
            CreateInGameSnapshot("other-profile-b", "profile-2", TimeSpan.FromDays(2), capturedAt.AddHours(3), 1_000m, 10_000m, 11_000m),
            CreateInGameSnapshot("other-game-a", "profile-1", TimeSpan.FromDays(1), capturedAt.AddHours(4), 8_000m, 80_000m, 90_000m, gameId: "ATS"),
            CreateInGameSnapshot("other-game-b", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(5), 2_000m, 20_000m, 22_000m, gameId: "ATS")
        ];

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(8), 7, snapshots);

        Assert.Equal(InGameAnalyticsStatus.Available, report.Status);
        Assert.Equal(3, report.SnapshotCount);
        Assert.Equal(60m, report.SnapshotCashChange);
        Assert.Equal(60m, report.SnapshotBankBalanceChange);
        Assert.Equal(70m, report.SnapshotNetWorthChange);
    }

    [Fact]
    public void InGameAnalytics_WithholdsChangesAcrossClockReset()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] snapshots =
        [
            CreateInGameSnapshot("before", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("later", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(1), 200m, 1_100m, 1_300m),
            CreateInGameSnapshot("restored", "profile-1", TimeSpan.FromDays(1.5), capturedAt.AddHours(2), 50m, 900m, 1_000m)
        ];

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, snapshots);

        Assert.Equal(InGameAnalyticsStatus.ClockResetDetected, report.Status);
        Assert.Null(report.SnapshotCashChange);
        Assert.Null(report.SnapshotBankBalanceChange);
        Assert.Empty(report.DailySummaries);
    }

    [Fact]
    public void InGameAnalytics_WithholdsChangesAcrossSaveVersionBoundary()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] snapshots =
        [
            CreateInGameSnapshot("before", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m, saveVersion: "v1"),
            CreateInGameSnapshot("after", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(1), 200m, 1_100m, 1_300m, saveVersion: "v2")
        ];

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, snapshots);

        Assert.Equal(InGameAnalyticsStatus.SaveVersionBoundary, report.Status);
        Assert.Null(report.SnapshotBankBalanceChange);
    }

    [Fact]
    public void InGameAnalytics_WithholdsDuplicateTimesAndIncompleteClockData()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] duplicateEndpoint =
        [
            CreateInGameSnapshot("first", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("last-a", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(1), 200m, 1_100m, 1_300m),
            CreateInGameSnapshot("last-b", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(2), 250m, 1_150m, 1_350m)
        ];
        LocalBalanceSnapshot[] missingClock =
        [
            CreateInGameSnapshot("first", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("missing-time", "profile-1", null, capturedAt.AddHours(1), 150m, 1_050m, 1_250m),
            CreateInGameSnapshot("last", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(2), 200m, 1_100m, 1_300m)
        ];
        LocalBalanceSnapshot[] duplicateInteriorTime =
        [
            CreateInGameSnapshot("first", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("middle-a", "profile-1", TimeSpan.FromDays(1.5), capturedAt.AddHours(1), 150m, 1_050m, 1_250m),
            CreateInGameSnapshot("middle-b", "profile-1", TimeSpan.FromDays(1.5), capturedAt.AddHours(2), 175m, 1_075m, 1_275m),
            CreateInGameSnapshot("last", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(3), 200m, 1_100m, 1_300m)
        ];
        LocalBalanceSnapshot[] duplicateId =
        [
            CreateInGameSnapshot("same-id", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("same-id", "profile-1", TimeSpan.FromDays(2), capturedAt.AddHours(1), 200m, 1_100m, 1_300m)
        ];
        LocalBalanceSnapshot[] nullEntry = [null!];

        InGameAnalyticsReport ambiguous = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, duplicateEndpoint);
        InGameAnalyticsReport incomplete = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, missingClock);
        InGameAnalyticsReport duplicateIntermediate = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, duplicateInteriorTime);
        InGameAnalyticsReport duplicateIdentifier = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, duplicateId);
        InGameAnalyticsReport malformed = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7, nullEntry);

        Assert.Equal(InGameAnalyticsStatus.AmbiguousGameTime, ambiguous.Status);
        Assert.Null(ambiguous.SnapshotBankBalanceChange);
        Assert.Equal(InGameAnalyticsStatus.MissingInGameTime, incomplete.Status);
        Assert.Null(incomplete.SnapshotBankBalanceChange);
        Assert.Equal(InGameAnalyticsStatus.AmbiguousGameTime, duplicateIntermediate.Status);
        Assert.Null(duplicateIntermediate.SnapshotBankBalanceChange);
        Assert.Equal(InGameAnalyticsStatus.InvalidSnapshotData, duplicateIdentifier.Status);
        Assert.Null(duplicateIdentifier.SnapshotBankBalanceChange);
        Assert.Equal(InGameAnalyticsStatus.InvalidSnapshotData, malformed.Status);
        Assert.Null(malformed.SnapshotBankBalanceChange);
    }

    [Fact]
    public void InGameAnalytics_ReportsInsufficientHistoryAndMissingGameTime()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        InGameAnalyticsReport insufficient = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7,
            [CreateInGameSnapshot("one", "profile-1", TimeSpan.FromDays(2), capturedAt, 100m, 1_000m, 1_200m)]);
        InGameAnalyticsReport noClock = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7,
            [CreateInGameSnapshot("legacy", "profile-1", null, capturedAt, 100m, 1_000m, 1_200m)]);
        InGameAnalyticsReport incompleteSingleCapture = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(3), 7,
            [
                CreateInGameSnapshot("clocked", "profile-1", TimeSpan.FromDays(2), capturedAt, 100m, 1_000m, 1_200m),
                CreateInGameSnapshot("unclocked", "profile-1", null, capturedAt, 150m, 1_050m, 1_250m)
            ]);

        Assert.Equal(InGameAnalyticsStatus.InsufficientSnapshots, insufficient.Status);
        Assert.Equal(InGameAnalyticsStatus.NoInGameTime, noClock.Status);
        Assert.Equal(InGameAnalyticsStatus.MissingInGameTime, incompleteSingleCapture.Status);
        Assert.Null(insufficient.SnapshotBankBalanceChange);
        Assert.Null(noClock.SnapshotBankBalanceChange);
        Assert.Null(incompleteSingleCapture.SnapshotBankBalanceChange);
    }

    [Fact]
    public void InGameAnalytics_WithholdsPartialWindowDelta()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] partialHistory =
        [
            CreateInGameSnapshot("first", "profile-1", TimeSpan.FromDays(1), capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("last", "profile-1", TimeSpan.FromDays(6), capturedAt.AddHours(1), 200m, 1_100m, 1_300m)
        ];

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(7), 7, partialHistory);

        Assert.Equal(InGameAnalyticsStatus.InsufficientCoverage, report.Status);
        Assert.Equal(2, report.SnapshotCount);
        Assert.Null(report.SnapshotCashChange);
        Assert.Null(report.SnapshotBankBalanceChange);
        Assert.Null(report.SnapshotNetWorthChange);
        Assert.Empty(report.DailySummaries);
    }

    [Fact]
    public void InGameAnalytics_WithholdsDailyDeltaWithoutBothDayBoundaries()
    {
        DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-11-26T12:00:00Z");
        LocalBalanceSnapshot[] sparseHistory =
        [
            CreateInGameSnapshot("start", "profile-1", TimeSpan.Zero, capturedAt, 100m, 1_000m, 1_200m),
            CreateInGameSnapshot("midday", "profile-1", TimeSpan.FromHours(12), capturedAt.AddHours(1), 150m, 1_050m, 1_250m),
            CreateInGameSnapshot("end", "profile-1", TimeSpan.FromDays(7), capturedAt.AddHours(2), 200m, 1_100m, 1_300m)
        ];

        InGameAnalyticsReport report = AnalyticsCalculator.CalculateInGameDays(
            "ETS2", "profile-1", TimeSpan.FromDays(7), 7, sparseHistory);

        Assert.Equal(InGameAnalyticsStatus.Available, report.Status);
        Assert.Equal(100m, report.SnapshotCashChange);
        Assert.All(report.DailySummaries, day => Assert.Null(day.SnapshotCashChange));
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
                DateTimeOffset.Parse("2026-11-25T12:00:00Z"), 10m, 20m, null, "profile-1", TimeSpan.FromDays(2.5));

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
    public async Task SnapshotStore_BoundedReadStopsAtTheFirstOverflowByte()
    {
        var source = new NonSeekableCountingStream(System.Text.Encoding.ASCII.GetBytes("0123456789"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LocalSnapshotStore.ReadBoundedBytesAsync(
                source,
                maximumBytes: 8,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(9, source.BytesRead);
        Assert.False(source.IsDisposed);
    }

    [Fact]
    public async Task SnapshotStore_BoundedReadAcceptsExactLimitWithoutTakingOwnership()
    {
        byte[] content = System.Text.Encoding.ASCII.GetBytes("12345678");
        var source = new NonSeekableCountingStream(content);

        byte[] result = await LocalSnapshotStore.ReadBoundedBytesAsync(
            source,
            maximumBytes: content.Length,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(content, result);
        Assert.Equal(content.Length, source.BytesRead);
        Assert.False(source.IsDisposed);
    }

    [Fact]
    public async Task SnapshotStore_BoundedReadDetectsGrowthAfterInitialLengthCheck()
    {
        var source = new GrowingSeekableStream(
            System.Text.Encoding.ASCII.GetBytes("123456789"),
            reportedLength: 8);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LocalSnapshotStore.ReadBoundedBytesAsync(
                source,
                maximumBytes: 8,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(9, source.BytesRead);
        Assert.False(source.IsDisposed);
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
    public async Task SnapshotStore_RejectsNegativeInGameTime()
    {
        string path = Path.Combine(Path.GetTempPath(), "tsse-negative-game-time-" + Guid.NewGuid().ToString("N"), "history.json");
        var store = new LocalSnapshotStore(path);
        var snapshot = new LocalBalanceSnapshot(
            "snapshot-negative", "ETS2", "synthetic", "unknown",
            DateTimeOffset.Parse("2026-11-25T12:00:00Z"), 10m, 20m, null, "profile-1", TimeSpan.FromSeconds(-1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.AppendAsync(snapshot, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
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
            Assert.Null(snapshot.InGameTime);
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

    private static LocalBalanceSnapshot CreateInGameSnapshot(
        string snapshotId,
        string profileId,
        TimeSpan? inGameTime,
        DateTimeOffset capturedAt,
        decimal? cash,
        decimal? bank,
        decimal? netWorth,
        string saveVersion = "v1",
        string gameId = "ETS2") =>
        new(snapshotId, gameId, profileId, saveVersion, capturedAt, cash, bank, netWorth, profileId, inGameTime);

    private static bool SkipWindowsLinkTestsOutsideGitHubActions() =>
        OperatingSystem.IsWindows() &&
        !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    private sealed class NonSeekableCountingStream(byte[] content) : Stream
    {
        private readonly MemoryStream _source = new(content);

        public int BytesRead { get; private set; }
        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int requested = Math.Min(buffer.Length, 3);
            int read = _source.Read(buffer[..requested]);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing)
                _source.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class GrowingSeekableStream(byte[] content, long reportedLength) : MemoryStream(content)
    {
        public int BytesRead { get; private set; }
        public bool IsDisposed { get; private set; }

        public override long Length => reportedLength;

        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
