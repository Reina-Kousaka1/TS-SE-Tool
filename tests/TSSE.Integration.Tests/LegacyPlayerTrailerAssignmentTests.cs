using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyPlayerTrailerAssignmentTests
{
    private const string Input = "player : p {\n assigned_trailer: a\n trailers: 2\n trailers[0]: a\n trailers[1]: b\n mod: \"未知\"\n}\ntrailer : a {\n}\ntrailer : b {\n}\n";

    [Fact]
    public void ExplicitAssignmentPreservesOtherFieldsAndSupportsUndo()
    {
        var assignment = new LegacyPlayerTrailerAssignment(SiiSyntaxDocument.Parse(Input), "p");
        Assert.Equal(new[] { "a", "b" }, assignment.EligibleTrailers);
        assignment.Assign("b");
        Assert.Equal(Input.Replace("assigned_trailer: a", "assigned_trailer: b", StringComparison.Ordinal), assignment.PreviewText);
        Assert.True(assignment.Undo());
        Assert.Equal(Input, assignment.PreviewText);
        Assert.True(assignment.Redo());
        Assert.Equal("b", assignment.AssignedTrailer);
    }

    [Theory]
    [InlineData("trailers[1]: b", "trailers[1]: missing")]
    [InlineData("trailers[1]: b", "trailers[1]: a")]
    [InlineData("assigned_trailer: a", "assigned_trailer: missing")]
    [InlineData("assigned_trailer: a", "assigned_trailer: a\n assigned_trailer: b")]
    [InlineData("trailer : b", "vehicle : b")]
    public void InvalidReferencesCannotBeOverwritten(string before, string after)
    {
        string input = Input.Replace(before, after, StringComparison.Ordinal);
        var assignment = new LegacyPlayerTrailerAssignment(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => assignment.Assign("a"));
        Assert.Equal(input, assignment.PreviewText);
        Assert.False(assignment.Undo());
    }

    [Fact]
    public void UnlistedAndAmbiguousTargetsAreRejected()
    {
        var assignment = new LegacyPlayerTrailerAssignment(SiiSyntaxDocument.Parse(Input), "p");
        Assert.Throws<InvalidOperationException>(() => assignment.Assign("missing"));
        string input = Input + "mod : b {\n}\n";
        var ambiguous = new LegacyPlayerTrailerAssignment(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => ambiguous.Assign("b"));
        Assert.Equal(input, ambiguous.PreviewText);
    }
}
