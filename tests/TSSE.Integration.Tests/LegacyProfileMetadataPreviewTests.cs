using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyProfileMetadataPreviewTests
{
    private const string Fixture = "SiiNunit\n{\n user_profile : demo {\n  profile_name: \"Example_1\"\n  cached_experience: 4294967295\n  cached_distance: 0\n  mod_value: \"未知\"\n }\n}\n";

    [Fact]
    public void NameEditPreservesUnknownDataAndIsUndoable()
    {
        var preview = new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(Fixture), "demo");
        Assert.Equal("Example_1", preview.Name);
        Assert.Equal(uint.MaxValue, preview.CachedExperience);
        Assert.Equal(0u, preview.CachedDistance);
        preview.SetName("Other_2");
        Assert.Contains("profile_name: Other_2", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod_value: \"未知\"", preview.PreviewText, StringComparison.Ordinal);
        Assert.True(preview.Undo());
        Assert.Equal(Fixture, preview.PreviewText);
        Assert.True(preview.Redo());
        Assert.Equal("Other_2", preview.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("quote\"name")]
    [InlineData("bad\\name")]
    [InlineData("line\nbreak")]
    public void UnsupportedNamesDoNotChangeHistory(string name)
    {
        var preview = new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(Fixture), "demo");
        preview.SetName("Changed");
        Assert.True(preview.Undo());
        Assert.Throws<ArgumentException>(() => preview.SetName(name));
        Assert.Equal(Fixture, preview.PreviewText);
        Assert.True(preview.CanRedo);
    }

    [Theory]
    [InlineData("名前", "\\xe5\\x90\\x8d\\xe5\\x89\\x8d")]
    [InlineData("Test 😀", "Test \\xf0\\x9f\\x98\\x80")]
    public void UnicodeNamesRoundTripThroughStrictUtf8Escapes(string name, string encoded)
    {
        var preview = new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(Fixture), "demo");
        preview.SetName(name);
        Assert.Contains("profile_name: \"" + encoded + "\"", preview.PreviewText, StringComparison.Ordinal);
        Assert.Equal(name, preview.Name);
        string edited = preview.PreviewText;
        Assert.True(preview.Undo());
        Assert.Equal(Fixture, preview.PreviewText);
        Assert.True(preview.Redo());
        Assert.Equal(edited, preview.PreviewText);
    }

    [Theory]
    [InlineData("\\xff")]
    [InlineData("\\xc3")]
    [InlineData("\\x41")]
    [InlineData("\\q")]
    public void UnsupportedOrMalformedEscapesAreRejected(string encoded)
    {
        string input = Fixture.Replace("\"Example_1\"", "\"" + encoded + "\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(input), "demo"));
    }

    [Theory]
    [InlineData("cached_distance: 0", "cached_distance: -1")]
    [InlineData("cached_experience: 4294967295", "cached_experience: 4294967296")]
    [InlineData("profile_name: \"Example_1\"", "profile_name: Example\n  profile_name: Other")]
    public void RejectsInvalidOrAmbiguousMetadata(string original, string replacement)
    {
        string input = Fixture.Replace(original, replacement, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(input), "demo"));
    }

    [Fact]
    public void RejectsOverlongNameAndIdSharedAcrossTypes()
    {
        var preview = new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(Fixture), "demo");
        Assert.Throws<ArgumentException>(() => preview.SetName(new string('a', 65)));
        Assert.Equal(Fixture, preview.PreviewText);
        string ambiguous = Fixture.Replace("\n}\n", "\n mod : demo {\n }\n}\n", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfileMetadataPreview(SiiSyntaxDocument.Parse(ambiguous), "demo"));
    }
}
