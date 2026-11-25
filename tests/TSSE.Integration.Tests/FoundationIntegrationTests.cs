using System.Text.Json;
using TSSE.Analytics;
using TSSE.Analytics.Models;
using TSSE.Analytics.Storage;
using TSSE.Bridge.Protocol;
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
            new LocalBalanceSnapshot("s1", "ETS2", "synthetic", "unknown", end.AddDays(-7), 200m, 1000m, 1500m),
            new LocalBalanceSnapshot("s2", "ETS2", "synthetic", "unknown", end, 400m, 1300m, 1900m)
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, transactions, snapshots);

        Assert.Equal(500m, report.RecordedIncome);
        Assert.Equal(100m, report.RecordedExpenses);
        Assert.Equal(400m, report.RecordedNet);
        Assert.Equal(300m, report.BalanceDelta);
        Assert.Equal(400m, report.NetWorthDelta);
    }

    [Fact]
    public void Analytics_OmitsSnapshotDeltaWhenInputContainsMultipleProfiles()
    {
        DateTimeOffset end = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new LocalBalanceSnapshot("a1", "ETS2", "profile-a", "unknown", end.AddDays(-2), 100m, 1_000m, 1_200m),
            new LocalBalanceSnapshot("b1", "ETS2", "profile-b", "unknown", end.AddDays(-1), 900m, 9_000m, 9_500m)
        };

        AnalyticsWindow report = AnalyticsCalculator.Calculate(end, 7, [], snapshots);

        Assert.Null(report.BalanceDelta);
        Assert.Null(report.NetWorthDelta);
    }

    [Fact]
    public void Bridge_ReturnsActualReadOnlyFoundationCapabilities()
    {
        const string request = "{\"protocolVersion\":1,\"requestId\":\"health-1\",\"method\":\"engine.status\"}";

        using JsonDocument response = JsonDocument.Parse(BridgeProtocol.HandleLine(request));
        JsonElement root = response.RootElement;
        JsonElement engine = root.GetProperty("engine");

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("foundation-only", engine.GetProperty("processState").GetString());
        Assert.Equal(0, engine.GetProperty("verifiedGameAdapterCount").GetInt32());
        Assert.False(engine.GetProperty("saveWriteEnabled").GetBoolean());
    }

    [Fact]
    public void Bridge_RejectsUnknownMethodsAndProtocolVersions()
    {
        string methodResponse = BridgeProtocol.HandleLine(
            "{\"protocolVersion\":1,\"requestId\":\"r1\",\"method\":\"save.write\"}");
        string versionResponse = BridgeProtocol.HandleLine(
            "{\"protocolVersion\":2,\"requestId\":\"r2\",\"method\":\"engine.status\"}");

        Assert.Contains("method_not_supported", methodResponse, StringComparison.Ordinal);
        Assert.Contains("protocol_version_unsupported", versionResponse, StringComparison.Ordinal);
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
                DateTimeOffset.Parse("2026-11-25T12:00:00Z"), 10m, 20m, null);

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
}
