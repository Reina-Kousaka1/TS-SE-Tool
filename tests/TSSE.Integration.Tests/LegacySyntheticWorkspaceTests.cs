using TSSE.Core.Editing;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacySyntheticWorkspaceTests
{
    [Fact]
    public void ExplicitTruckSelectionUsesTheSameHistory()
    {
        string input = Fixture.Replace("\n}\n", "\n vehicle : t2 {\n  fuel_relative: 0.25\n }\n}\n", StringComparison.Ordinal);
        var workspace = new LegacySyntheticWorkspace(SiiSyntaxDocument.Parse(input), "e", "p", "t", "r");
        Assert.Equal(new[] { "t", "t2" }, workspace.TruckIds());
        workspace.SelectTruck("t2").SetValue("fuel_relative", 1);
        Assert.Equal(0.5f, workspace.Truck.GetValue("fuel_relative"));
        Assert.True(workspace.Undo());
        Assert.Equal(input, workspace.PreviewText);
        Assert.Throws<InvalidOperationException>(() => workspace.SelectTruck("missing"));
        Assert.Equal(input, workspace.PreviewText);
    }
    [Fact]
    public void TrailerIdentifiersComeFromTheDocumentInSourceOrder()
    {
        string input = Fixture.Replace("\n}\n", "\n trailer : second {\n  trailer_body_wear: 0.1\n }\n trailer : third {\n  trailer_body_wear: 0.2\n }\n}\n", StringComparison.Ordinal);
        var workspace = new LegacySyntheticWorkspace(SiiSyntaxDocument.Parse(input), "e", "p", "t", "r");
        Assert.Equal(new[] { "r", "second", "third" }, workspace.TrailerIds());
        workspace.SelectTrailer("third").SetValue("trailer_body_wear", 0);
        Assert.Equal(0f, workspace.SelectTrailer("third").GetValue("trailer_body_wear"));
        Assert.True(workspace.Undo());
        Assert.Equal(input, workspace.PreviewText);
    }

    [Theory]
    [InlineData("trailer : dup {\n }\n trailer : dup {\n }")]
    [InlineData("trailer {\n }")]
    public void AmbiguousOrAnonymousTrailerCannotBeSilentlyOmitted(string blocks)
    {
        string input = Fixture.Replace("\n}\n", "\n " + blocks + "\n}\n", StringComparison.Ordinal);
        var workspace = new LegacySyntheticWorkspace(SiiSyntaxDocument.Parse(input), "e", "p", "t", "r");
        Assert.Throws<InvalidOperationException>(() => workspace.TrailerIds());
        Assert.Equal(input, workspace.PreviewText);
        Assert.False(workspace.IsDirty);
    }
    [Fact]
    public void ExplicitTrailerSelectionSharesHistoryAndRejectsMissingTargets()
    {
        var workspace = Create();
        var selected = workspace.SelectTrailer("r");
        selected.SetValue("trailer_body_wear", 0);
        Assert.True(workspace.Undo());
        Assert.Equal(0.4f, selected.GetValue("trailer_body_wear"));
        Assert.True(workspace.Redo());
        Assert.Equal(0f, selected.GetValue("trailer_body_wear"));
        string before = workspace.PreviewText;
        Assert.Throws<InvalidOperationException>(() => workspace.SelectTrailer("missing"));
        Assert.Equal(before, workspace.PreviewText);
    }
    private const string Fixture = "SiiNunit\n{\n economy : e {\n  bank: b\n  experience_points: 5\n }\n bank : b {\n  money_account: 10\n }\n player : p {\n  current_job: j\n }\n player_job : j {\n  cargo: cargo.synthetic\n  units_count: 3\n }\n vehicle : t {\n  fuel_relative: 0.5\n }\n trailer : r {\n  trailer_body_wear: 0.4\n  mod_field: \"未知\"\n }\n}\n";

    [Fact]
    public void EditsAcrossDomainsShareOneHistoryAndPreserveUnknownFields()
    {
        var workspace = Create();
        workspace.Profile.Apply(new ProfileValues(20, 5));
        workspace.Truck.SetValue("fuel_relative", 1);
        workspace.Trailer.SetValue("trailer_body_wear", 0);
        workspace.Job.SetUnitsCount(8);
        Assert.Contains("money_account: 20", workspace.PreviewText, StringComparison.Ordinal);
        Assert.Contains("fuel_relative: 1", workspace.PreviewText, StringComparison.Ordinal);
        Assert.Contains("trailer_body_wear: 0", workspace.PreviewText, StringComparison.Ordinal);
        Assert.Contains("units_count: 8", workspace.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod_field: \"未知\"", workspace.PreviewText, StringComparison.Ordinal);
        Assert.Equal(new[] { "money_account", "units_count", "fuel_relative", "trailer_body_wear" },
            workspace.Changes.Select(change => change.FieldName));
        Assert.True(workspace.Undo());
        Assert.Equal(3, workspace.Job.UnitsCount);
        Assert.True(workspace.Undo());
        Assert.Equal(0.4f, workspace.Trailer.GetValue("trailer_body_wear"));
        Assert.True(workspace.Undo());
        Assert.Equal(0.5f, workspace.Truck.GetValue("fuel_relative"));
        Assert.True(workspace.Undo());
        Assert.Equal(Fixture, workspace.PreviewText);
        Assert.False(workspace.IsDirty);
        Assert.True(workspace.Redo());
        Assert.Equal(20, workspace.Profile.Current.Balance);
    }

    [Fact]
    public void InvalidEditDoesNotDiscardOtherDomainChangesOrRedo()
    {
        var workspace = Create();
        workspace.Profile.Apply(new ProfileValues(20, 5));
        workspace.Job.SetUnitsCount(8);
        Assert.True(workspace.Undo());
        string before = workspace.PreviewText;
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.Truck.SetValue("fuel_relative", 2));
        Assert.Equal(before, workspace.PreviewText);
        Assert.True(workspace.CanRedo);
        Assert.True(workspace.Redo());
        Assert.Equal(8, workspace.Job.UnitsCount);
    }

    private static LegacySyntheticWorkspace Create() =>
        new(SiiSyntaxDocument.Parse(Fixture), "e", "p", "t", "r");
}
