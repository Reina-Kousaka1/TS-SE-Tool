using TSSE.Core.Editing;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyProfilePreviewTests
{
    [Theory]
    [InlineData("e")]
    [InlineData("b")]
    public void AmbiguousEconomyOrBankIdAcrossBlockTypesFailsClosed(string duplicateId)
    {
        string input = Synthetic.Replace(" economy : e {", " mod : " + duplicateId + " {\n  unknown: preserved\n }\n economy : e {", StringComparison.Ordinal);
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfilePreview(document, "e"));
        Assert.Equal(input, document.ToText());
    }

    private const string Synthetic = "SiiNunit\n{\n economy : e {\n  bank: b\n  experience_points: 0005\n  mod_value: \"未知\"\n }\n bank : b {\n  money_account: -42\n  unknown: nil\n }\n}\n";

    [Fact]
    public void ProfileChangesOnlyRequestedFieldsAndPreservesUnknownSyntax()
    {
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Synthetic), "e");
        Assert.Equal(new ProfileValues(-42, 5), editor.Current);
        editor.Apply(new ProfileValues(long.MinValue, 5));
        string changed = Synthetic.Replace("money_account: -42", "money_account: -9223372036854775808", StringComparison.Ordinal);
        Assert.Equal(changed, editor.PreviewText);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Apply(new ProfileValues(long.MinValue, uint.MaxValue)));
        Assert.Equal(5u, editor.Current.Experience);
        Assert.Equal(changed, editor.PreviewText);
        Assert.True(editor.Undo());
        Assert.Equal(Synthetic, editor.PreviewText);
        Assert.False(editor.IsDirty);
        Assert.True(editor.Redo());
        Assert.Equal(changed, editor.PreviewText);
    }

    [Theory]
    [InlineData("bank: b", "bank: nil")]
    [InlineData("bank: b", "bank: missing")]
    [InlineData("experience_points: 0005", "experience_points: 4294967296")]
    [InlineData("experience_points: 0005", "experience_points: 4294967295")]
    [InlineData("money_account: -42", "money_account: invalid")]
    [InlineData("unknown: nil", "unknown: nil\n  money_account: 1")]
    public void InvalidOrAmbiguousInputFailsBeforeEditing(string from, string to)
    {
        string input = Synthetic.Replace(from, to, StringComparison.Ordinal);
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfilePreview(document, "e"));
        Assert.Equal(input, document.ToText());
    }

    [Fact]
    public void UnchangedValuesDoNotNormalizeOrDiscardRedo()
    {
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Synthetic), "e");
        editor.Apply(new ProfileValues(10, 5));
        Assert.True(editor.Undo());
        editor.Apply(new ProfileValues(-42, 5));
        Assert.Equal(Synthetic, editor.PreviewText);
        Assert.True(editor.CanRedo);
    }

    [Fact]
    public void ExistingCoachExperienceIsAnOptionalSignedInt32EditAndUndoable()
    {
        string input = Synthetic.Replace("  experience_points: 0005\n", "  experience_points: 0005\n  bus_experience_points: 7\n", StringComparison.Ordinal);
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e");
        Assert.Equal(7L, editor.Current.CoachExperience);

        editor.Apply(editor.Current with { CoachExperience = int.MaxValue });

        Assert.Contains("  bus_experience_points: 2147483647\n", editor.PreviewText, StringComparison.Ordinal);
        Assert.Equal("bus_experience_points", Assert.Single(editor.Changes).FieldName);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Contains("  bus_experience_points: 2147483647\n", editor.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void CoachExperienceIsNotAddedWhenTheSelectedDocumentDoesNotContainIt()
    {
        var editor = new LegacyProfilePreview(SiiSyntaxDocument.Parse(Synthetic), "e");

        Assert.Null(editor.Current.CoachExperience);
        Assert.Throws<InvalidOperationException>(() => editor.Apply(editor.Current with { CoachExperience = 1 }));
        Assert.Equal(Synthetic, editor.PreviewText);
    }

    [Theory]
    [InlineData("bus_experience_points: invalid")]
    [InlineData("bus_experience_points: 2147483648")]
    public void MalformedCoachExperienceFailsClosed(string field)
    {
        string input = Synthetic.Replace("  experience_points: 0005\n", $"  experience_points: 0005\n  {field}\n", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new LegacyProfilePreview(SiiSyntaxDocument.Parse(input), "e"));
    }
}
