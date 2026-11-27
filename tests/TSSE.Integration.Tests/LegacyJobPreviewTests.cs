using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyJobPreviewTests
{
    [Fact]
    public void RouteSwapPreservesUnrelatedDataAndHasOneUndoStep()
    {
        string input = Synthetic.Replace("cargo: cargo.synthetic", "cargo: cargo.synthetic\n  source_company: company.α\n  target_company: company.β", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SwapRoute();
        Assert.Equal("company.β", editor.SourceCompany);
        Assert.Equal("company.α", editor.TargetCompany);
        Assert.Equal(2, editor.Changes.Count);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
        Assert.True(editor.Redo());
        Assert.Equal("company.β", editor.SourceCompany);
    }

    [Theory]
    [InlineData("target_company: nil")]
    [InlineData("target_company: null")]
    [InlineData("target_company: a\n  target_company: b")]
    [InlineData("other: b")]
    public void InvalidRouteDoesNotPartiallySwap(string target)
    {
        string input = Synthetic.Replace("cargo: cargo.synthetic", "cargo: cargo.synthetic\n  source_company: company.a\n  " + target, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SwapRoute());
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void EqualReferencesAreANoOpAndPreserveRedo()
    {
        string input = Synthetic.Replace("cargo: cargo.synthetic", "cargo: cargo.synthetic\n  source_company: company.a\n  target_company: company.a", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SetUnitsCount(4);
        Assert.True(editor.Undo());
        editor.SwapRoute();
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.CanRedo);
    }
    [Fact]
    public void BothJobValuesAreAtomicAndShareOneUndoStep()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  planned_distance_km: 00120", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SetUnitsAndPlannedDistance(7, 250);
        Assert.Equal(7, editor.UnitsCount);
        Assert.Equal(250, editor.PlannedDistanceKm);
        Assert.Equal(2, editor.Changes.Count);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        string changed = editor.PreviewText;
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetUnitsAndPlannedDistance(8, -1));
        Assert.Equal(changed, editor.PreviewText);
    }

    [Fact]
    public void AtomicJobEditRejectsInvalidSecondFieldBeforeChangingFirst()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  planned_distance_km: broken", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SetUnitsAndPlannedDistance(7, 250));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }
    [Fact]
    public void PlannedDistanceIsAnIndependentUndoableSyntheticEdit()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  planned_distance_km: 00120", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Equal(120, editor.PlannedDistanceKm);
        editor.SetPlannedDistanceKm(120);
        Assert.Equal(input, editor.PreviewText);
        editor.SetPlannedDistanceKm(int.MaxValue);
        Assert.Contains("planned_distance_km: 2147483647", editor.PreviewText, StringComparison.Ordinal);
        Assert.Contains("units_count: 0003", editor.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetPlannedDistanceKm(-1));
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal(int.MaxValue, editor.PlannedDistanceKm);
    }

    [Theory]
    [InlineData("planned_distance_km: -1")]
    [InlineData("planned_distance_km: 2147483648")]
    [InlineData("planned_distance_km: 1\n  planned_distance_km: 2")]
    public void InvalidPlannedDistanceCannotBeOverwritten(string field)
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  " + field, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SetPlannedDistanceKm(10));
        Assert.Equal(input, editor.PreviewText);
    }
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
