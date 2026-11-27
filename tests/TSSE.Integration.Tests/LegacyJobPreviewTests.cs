using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyJobPreviewTests
{
    private const string Synthetic = "SiiNunit\n{\n player : p {\n  current_job: j\n }\n player_job : j {\n  cargo: cargo.synthetic\n  units_count: 0003\n  mod_data: \"保持\"\n }\n}\n";

    [Fact]
    public void JobUnitsCanBeEditedAndUndoneWithoutChangingOtherFields()
    {
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(Synthetic), "p");
        Assert.True(editor.HasActiveJob);
        Assert.Equal("cargo.synthetic", editor.CargoReference);
        Assert.Equal(3, editor.UnitsCount);
        editor.SetUnitsCount(3);
        Assert.Equal(Synthetic, editor.PreviewText);
        editor.SetUnitsCount(int.MaxValue);
        string changed = Synthetic.Replace("units_count: 0003", "units_count: 2147483647", StringComparison.Ordinal);
        Assert.Equal(changed, editor.PreviewText);
        Assert.True(editor.Undo());
        Assert.Equal(Synthetic, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal(changed, editor.PreviewText);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetUnitsCount(-1));
        Assert.Equal(changed, editor.PreviewText);
    }

    [Theory]
    [InlineData("nil")]
    [InlineData("null")]
    public void NoActiveJobIsReportedWithoutInventingOne(string emptyReference)
    {
        string input = Synthetic.Replace("current_job: j", "current_job: " + emptyReference, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.False(editor.HasActiveJob);
        Assert.Throws<InvalidOperationException>(() => editor.SetUnitsCount(1));
        Assert.Equal(input, editor.PreviewText);
    }

    [Theory]
    [InlineData("current_job: j", "current_job: missing")]
    [InlineData("units_count: 0003", "units_count: -1")]
    [InlineData("units_count: 0003", "units_count: 2147483648")]
    [InlineData("units_count: 0003", "units_count: 3\n  units_count: 4")]
    public void InvalidOrAmbiguousInputsFailClosed(string oldText, string replacement)
    {
        string input = Synthetic.Replace(oldText, replacement, StringComparison.Ordinal);
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => new LegacyJobPreview(document, "p"));
        Assert.Equal(input, document.ToText());
    }
}
