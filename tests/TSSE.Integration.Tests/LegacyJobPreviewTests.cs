using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyJobPreviewTests
{
    [Fact]
    public void FerryValuesAreValidatedTogetherAndUndoAsOneEdit()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  ferry_time: 0001\n  ferry_price: 0002", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetFerryDetails(3, -1));
        Assert.Equal(input, editor.PreviewText);
        editor.SetFerryDetails(3, int.MaxValue);
        Assert.Equal(3, editor.FerryTime);
        Assert.Equal(int.MaxValue, editor.FerryPrice);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        editor.SetFerryDetails(1, 2);
        Assert.True(editor.CanRedo);
        Assert.True(editor.Redo());
        Assert.Equal(int.MaxValue, editor.FerryPrice);
    }

    [Theory]
    [InlineData("ferry_price: -1")]
    [InlineData("ferry_price: 2147483648")]
    [InlineData("ferry_price: 1\n  ferry_price: 2")]
    [InlineData("other: 1")]
    public void InvalidExistingFerryFieldRejectsWholeBatch(string price)
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  ferry_time: 1\n  " + price, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SetFerryDetails(5, 5));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void UrgencyEditPreservesUnknownFieldsAndRedoAfterRejectedValues()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  urgency: 00", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SetUrgency(0);
        Assert.Equal(input, editor.PreviewText);
        editor.SetUrgency(2);
        Assert.Contains("urgency: 2", editor.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetUrgency(3));
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal(2, editor.Urgency);
    }

    [Theory]
    [InlineData("urgency: -1")]
    [InlineData("urgency: 3")]
    [InlineData("urgency: nil")]
    [InlineData("urgency: 2147483648")]
    [InlineData("urgency: 0\n  urgency: 1")]
    [InlineData("other: 0")]
    public void InvalidUrgencyCannotBeOverwritten(string field)
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  " + field, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SetUrgency(1));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void CargoDetailsAreAtomicAndUndoAsOneStep()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  cargo_model_index: 0002", StringComparison.Ordinal)
            .Replace("\n}\n", "\n job_offer_data : offer {\n  cargo: cargo.other\n }\n}\n", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetCargoDetails("cargo.other", 7, -1));
        Assert.Throws<InvalidOperationException>(() => editor.SetCargoDetails("cargo.unknown", 7, 4));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
        editor.SetCargoDetails("cargo.other", 7, 4);
        Assert.Equal("cargo.other", editor.CargoReference);
        Assert.Equal(7, editor.UnitsCount);
        Assert.Equal(4, editor.CargoModelIndex);
        Assert.Equal(3, editor.Changes.Count);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal("cargo.other", editor.CargoReference);
    }

    [Fact]
    public void CargoSelectionRequiresObservedReferencesAndSupportsUndo()
    {
        string input = Synthetic.Replace("\n}\n", "\n job_offer_data : offer {\n  cargo: cargo.other\n }\n}\n", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Equal(new[] { "cargo.synthetic", "cargo.other" }, editor.ObservedCargoReferences());
        Assert.Throws<InvalidOperationException>(() => editor.SelectObservedCargo("cargo.unknown"));
        Assert.Equal(input, editor.PreviewText);
        editor.SelectObservedCargo("cargo.other");
        Assert.Equal("cargo.other", editor.CargoReference);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal("cargo.other", editor.CargoReference);
    }

    [Theory]
    [InlineData("cargo: nil")]
    [InlineData("cargo: cargo.other\n  cargo: cargo.extra")]
    [InlineData("cargo: cargo.")]
    public void InvalidCargoCatalogDoesNotChangeHistory(string field)
    {
        string input = Synthetic.Replace("\n}\n", "\n job_offer_data : offer {\n  " + field + "\n }\n}\n", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SetUnitsCount(9);
        Assert.True(editor.Undo());
        Assert.Throws<InvalidOperationException>(() => editor.SelectObservedCargo("cargo.synthetic"));
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.CanRedo);
    }

    [Fact]
    public void CargoModelIndexEditPreservesOtherFieldsAndUndo()
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  cargo_model_index: 0002", StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        editor.SetCargoModelIndex(2);
        Assert.Equal(input, editor.PreviewText);
        editor.SetCargoModelIndex(int.MaxValue);
        Assert.Contains("cargo_model_index: 2147483647", editor.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod_data: \"保持\"", editor.PreviewText, StringComparison.Ordinal);
        Assert.True(editor.Undo());
        Assert.Equal(input, editor.PreviewText);
        Assert.True(editor.Redo());
        Assert.Equal(int.MaxValue, editor.CargoModelIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetCargoModelIndex(-1));
    }

    [Theory]
    [InlineData("cargo_model_index: -1")]
    [InlineData("cargo_model_index: 2147483648")]
    [InlineData("cargo_model_index: 1\n  cargo_model_index: 2")]
    [InlineData("other: 1")]
    public void InvalidCargoModelIndexCannotBeOverwritten(string field)
    {
        string input = Synthetic.Replace("units_count: 0003", "units_count: 0003\n  " + field, StringComparison.Ordinal);
        var editor = new LegacyJobPreview(SiiSyntaxDocument.Parse(input), "p");
        Assert.Throws<InvalidOperationException>(() => editor.SetCargoModelIndex(3));
        Assert.Equal(input, editor.PreviewText);
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData("p")]
    [InlineData("j")]
    public void PlayerAndJobIdentifiersMustBeUniqueAcrossBlockTypes(string duplicateId)
    {
        string input = Synthetic.Replace(" player : p {", " other_type : " + duplicateId + " {\n  unknown: preserved\n }\n player : p {", StringComparison.Ordinal);
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => new LegacyJobPreview(document, "p"));
        Assert.Equal(input, document.ToText());
    }

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
