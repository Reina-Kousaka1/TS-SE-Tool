using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyPlayerTrailerInspectorTests
{
    [Fact]
    public void DuplicatePlayerIdAcrossBlockTypesPreventsInspection()
    {
        const string input = "player : p {\n trailers: 0\n}\nmod : p {\n unknown: preserved\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => LegacyPlayerTrailerInspector.Inspect(document, "p"));
        Assert.Equal(input, document.ToText());
    }

    [Fact]
    public void ReferencesReportMissingAmbiguousOtherAndDuplicatesWithoutChangingData()
    {
        const string input = "player : p {\n trailers: 6\n trailers[0]: a\n trailers[1]: missing\n trailers[2]: other\n trailers[3]: dup\n trailers[4]: a\n trailers[5]: nil\n mod: \"保持\"\n}\ntrailer : a {\n}\nmod : other {\n}\ntrailer : dup {\n}\nmod : dup {\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        var references = LegacyPlayerTrailerInspector.Inspect(document, "p");
        Assert.Equal(new[] { TrailerReferenceState.TrailerBlock, TrailerReferenceState.Missing,
            TrailerReferenceState.OtherBlock, TrailerReferenceState.Ambiguous,
            TrailerReferenceState.TrailerBlock, TrailerReferenceState.Empty }, references.Select(item => item.State));
        Assert.True(references[0].IsDuplicate);
        Assert.True(references[4].IsDuplicate);
        Assert.False(references[5].IsDuplicate);
        Assert.Equal(input, document.ToText());
    }

    [Theory]
    [InlineData("trailers: 2\n trailers[0]: a")]
    [InlineData("trailers: 1\n trailers[1]: a")]
    [InlineData("trailers: 2\n trailers[0]: a\n trailers[0]: b")]
    [InlineData("trailers: 1025")]
    [InlineData("trailers: -1")]
    public void InvalidPlayerArraysFailClosed(string fields)
    {
        string input = "player : p {\n " + fields + "\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => LegacyPlayerTrailerInspector.Inspect(document, "p"));
        Assert.Equal(input, document.ToText());
    }
}
