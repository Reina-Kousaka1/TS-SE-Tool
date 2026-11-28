using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;
using System.Text.RegularExpressions;

namespace TSSE.Integration.Tests;

public sealed class LegacyCoachJobInspectorTests
{
    [Fact]
    public void ReadsObservedJobStageAndPassengerReferencesWhileRetainingRawFields()
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(Fixture);
        string original = document.ToText();

        LegacyCoachJobInspection snapshot = new LegacyCoachJobInspector(document, "player-id").Snapshot;

        Assert.Equal(LegacyCoachReferenceState.Resolved, snapshot.CurrentJob!.State);
        Assert.Equal("player_bus_job", snapshot.CurrentJob.ResolvedType);
        LegacyCoachJobSnapshot job = Assert.Single(snapshot.Jobs);
        Assert.Equal(2, job.Stages!.Count);
        Assert.All(job.Stages, stage =>
        {
            Assert.Equal(LegacyCoachReferenceState.Resolved, stage.Reference.State);
            Assert.Equal("player_bus_job_stage", stage.Reference.ResolvedType);
            Assert.NotNull(stage.Source);
        });
        Assert.Contains(job.Stages, stage => stage.Source!.Fields.Any(field => field.Name == "source_bus_stop"));
        LegacyCoachPassengerSnapshot passenger = Assert.Single(job.Passengers!);
        Assert.Equal("player_bus_passenger", passenger.Reference.ResolvedType);
        Assert.NotNull(passenger.Source);
        Assert.Contains(passenger.Source!.Fields, field => field.Name == "unknown_passenger_field");
        Assert.Contains(job.Source.Fields, field => field.Name == "future_coach_field" && field.Value == "preserved");
        Assert.Equal(original, document.ToText());
    }

    [Fact]
    public void PreservesAbsentArraysAndReportsMissingOrAmbiguousReferences()
    {
        string input = RemoveArrayFields(RemoveArrayFields(Fixture, "stages"), "passengers");
        LegacyCoachJobSnapshot withoutArrays = Assert.Single(new LegacyCoachJobInspector(
            SiiSyntaxDocument.Parse(input), "player-id").Snapshot.Jobs);
        Assert.Null(withoutArrays.Stages);
        Assert.Null(withoutArrays.Passengers);

        input = InsertJobFields(input, "  stages: 1\n  stages[0]: missing-stage\n  passengers: 1\n  passengers[0]: missing-passenger");

        LegacyCoachJobSnapshot job = Assert.Single(new LegacyCoachJobInspector(
            SiiSyntaxDocument.Parse(input), "player-id").Snapshot.Jobs);

        LegacyCoachJobStageSnapshot stage = Assert.Single(job.Stages!);
        Assert.Equal(LegacyCoachReferenceState.Unresolved, stage.Reference.State);
        Assert.Null(stage.Source);
        LegacyCoachPassengerSnapshot passenger = Assert.Single(job.Passengers!);
        Assert.Equal(LegacyCoachReferenceState.Unresolved, passenger.Reference.State);
        Assert.Null(passenger.Source);
        Assert.Equal("future_job", new LegacyCoachJobInspector(
            SiiSyntaxDocument.Parse(Fixture.Replace("player_bus_job : job-a", "future_job : job-a", StringComparison.Ordinal)),
            "player-id").Snapshot.CurrentJob!.ResolvedType);
    }

    [Theory]
    [InlineData("stages: 2\n  stages[0]: stage-a\n")]
    [InlineData("stages: 1\n  stages[1]: stage-a\n")]
    [InlineData("stages: 1\n  stages[x]: stage-a\n")]
    public void RejectsIncompleteOrMalformedObservedReferenceArrays(string fields)
    {
        string input = InsertJobFields(RemoveArrayFields(Fixture, "stages"), "  " + fields);

        Assert.Throws<InvalidOperationException>(() => new LegacyCoachJobInspector(
            SiiSyntaxDocument.Parse(input), "player-id"));
    }

    private const string Fixture = """
        SiiNunit
        {
         player : player-id {
          current_bus_job: job-a
         }
         player_bus_job : job-a {
          stages: 2
          stages[0]: stage-a
          stages[1]: stage-b
          passengers: 1
          passengers[0]: passenger-a
          future_coach_field: preserved
         }
         player_bus_job_stage : stage-a {
          source_bus_stop: stop-a
          target_bus_stop: stop-b
         }
         player_bus_job_stage : stage-b {
          unknown_stage_field: retained
         }
         player_bus_passenger : passenger-a {
          unknown_passenger_field: retained
         }
        }
        """;

    private static string RemoveArrayFields(string input, string name) => Regex.Replace(
        input,
        $"(?m)^[\\t ]*{Regex.Escape(name)}(?:\\[[^\\]\\r\\n]*\\])?:[^\\r\\n]*(?:\\r?\\n|$)",
        string.Empty);

    private static string InsertJobFields(string input, string fields) => input.Replace(
        "player_bus_job : job-a {",
        "player_bus_job : job-a {\n" + fields,
        StringComparison.Ordinal);
}
