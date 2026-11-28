using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyCoachProfileInspectorTests
{
    [Fact]
    public void ReadsOnlyHistoricallyModeledCoachMetricsAndResolvesExistingReferences()
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(Fixture);
        string unchanged = document.ToText();

        var result = new LegacyCoachProfileInspector(document, "economy-id").Snapshot;

        Assert.Equal(4321, result.Counters["bus_experience_points"]);
        Assert.Equal(-17, result.Counters["bus_total_distance"]);
        Assert.Null(result.Counters["bus_game_time"]);
        Assert.Equal(new[] { "stop-a" }, result.StopIds);
        Assert.Equal("log-id", result.JobLogId);
        Assert.Equal(2, result.JobLogVersion);
        Assert.Equal(1, result.JobLogEntryCount);
        Assert.Equal(new LegacyCoachStopSnapshot("stop-a", true, 1), Assert.Single(result.Stops));
        Assert.Equal(unchanged, document.ToText());
    }

    [Fact]
    public void MissingCoachFieldsAreReportedAsAbsentWithoutInventingDefaults()
    {
        const string input = "SiiNunit\n{\n economy : e {\n }\n}\n";
        var result = new LegacyCoachProfileInspector(SiiSyntaxDocument.Parse(input), "e").Snapshot;

        Assert.All(result.Counters.Values, Assert.Null);
        Assert.Empty(result.StopIds);
        Assert.Null(result.JobLogId);
    }

    [Theory]
    [InlineData("bus_stops: 2\n  bus_stops[0]: stop-a")]
    [InlineData("bus_stops: 1\n  bus_stops[1]: stop-a")]
    [InlineData("bus_stops: 0\n  bus_stops[x]: stop-a")]
    public void IncompleteOrMalformedArraysFailClosed(string fields)
    {
        string input = "SiiNunit\n{\n economy : e {\n  " + fields + "\n }\n}\n";

        Assert.Throws<InvalidOperationException>(() => new LegacyCoachProfileInspector(SiiSyntaxDocument.Parse(input), "e"));
    }

    [Fact]
    public void DanglingReferencesAndMalformedMetricsFailClosed()
    {
        string dangling = Fixture.Replace("bus_stop : stop-a", "other : stop-a", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyCoachProfileInspector(SiiSyntaxDocument.Parse(dangling), "economy-id"));

        string malformed = Fixture.Replace("bus_experience_points: 4321", "bus_experience_points: 2147483648", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyCoachProfileInspector(SiiSyntaxDocument.Parse(malformed), "economy-id"));
    }

    private const string Fixture = "SiiNunit\n{\n economy : economy-id {\n  bus_experience_points: 4321\n  bus_total_distance: -17\n  bus_stops: 1\n  bus_stops[0]: stop-a\n  bus_job_log: log-id\n }\n bus_stop : stop-a {\n  discovered: true\n  lines_offer: 1\n  lines_offer[0]: line-a\n }\n bus_job_log : log-id {\n  version: 2\n  entries: 1\n  entries[0]: entry-a\n }\n}\n";
}
