using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyPlayerTruckAssignmentTests
{
    private const string Input = "player : p {\n assigned_truck: a\n trucks: 2\n trucks[0]: a\n trucks[1]: b\n mod: \"未知\"\n}\nvehicle : a {\n}\nvehicle : b {\n}\n";

    [Fact]
    public void ValidAssignmentChangesOnlyReferenceAndSupportsUndo()
    {
        var assignment = new LegacyPlayerTruckAssignment(SiiSyntaxDocument.Parse(Input), "p");
        Assert.Equal(new[] { "a", "b" }, assignment.EligibleTrucks);
        assignment.Assign("a");
        Assert.Equal(Input, assignment.PreviewText);
        assignment.Assign("b");
        Assert.Equal(Input.Replace("assigned_truck: a", "assigned_truck: b", StringComparison.Ordinal), assignment.PreviewText);
        Assert.True(assignment.Undo());
        Assert.Equal(Input, assignment.PreviewText);
        Assert.True(assignment.Redo());
        Assert.Equal("b", assignment.AssignedTruck);
    }

    [Theory]
    [InlineData("trucks[1]: b", "trucks[1]: missing")]
    [InlineData("trucks[1]: b", "trucks[1]: a")]
    [InlineData("trucks[1]: b", "trucks[1]: nil")]
    [InlineData("assigned_truck: a", "assigned_truck: missing")]
    [InlineData("assigned_truck: a", "assigned_truck: a\n assigned_truck: b")]
    [InlineData("vehicle : b", "trailer : b")]
    public void InvalidReferencesFailBeforeAnyModification(string oldText, string replacement)
    {
        string input = Input.Replace(oldText, replacement, StringComparison.Ordinal);
        var assignment = new LegacyPlayerTruckAssignment(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => assignment.Assign("a"));
        Assert.Equal(input, assignment.PreviewText);
        Assert.False(assignment.Undo());
    }

    [Fact]
    public void UnlistedOrAmbiguousTargetCannotBeAssigned()
    {
        var assignment = new LegacyPlayerTruckAssignment(SiiSyntaxDocument.Parse(Input), "p");
        Assert.Throws<InvalidOperationException>(() => assignment.Assign("missing"));
        string ambiguous = Input + "mod : b {\n}\n";
        var invalid = new LegacyPlayerTruckAssignment(SiiSyntaxDocument.Parse(ambiguous), "p");
        Assert.Throws<InvalidOperationException>(() => invalid.Assign("b"));
        Assert.Equal(ambiguous, invalid.PreviewText);
    }

    [Theory]
    [InlineData("nil")]
    [InlineData("null")]
    public void UnassignedPlayerCanSelectAListedTruck(string emptyReference)
    {
        string input = Input.Replace("assigned_truck: a", "assigned_truck: " + emptyReference, StringComparison.Ordinal);
        var assignment = new LegacyPlayerTruckAssignment(SiiSyntaxDocument.Parse(input), "p");
        assignment.Assign("b");
        Assert.Equal("b", assignment.AssignedTruck);
        Assert.True(assignment.Undo());
        Assert.Equal(input, assignment.PreviewText);
    }
}
