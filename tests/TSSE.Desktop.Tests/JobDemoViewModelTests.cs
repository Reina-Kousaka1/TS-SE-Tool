using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class JobDemoViewModelTests
{
    [Fact]
    public void UrgencyValidatesAndProtectsStaleDrafts()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.UrgencyInput = "3";
        demo.ActionCommand.Execute("urgency");
        Assert.Equal(original, demo.PreviewText);
        demo.UrgencyInput = "1";
        demo.ActionCommand.Execute("urgency");
        Assert.Contains("urgency: 1", demo.PreviewText, StringComparison.Ordinal);
        demo.UrgencyInput = "2";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsUrgencyStale);
        demo.ActionCommand.Execute("urgency");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("urgency-reviewed");
        Assert.Contains("urgency: 2", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        demo.UrgencyInput = "1";
        demo.ActionCommand.Execute("discard-urgency");
        Assert.Equal("0", demo.UrgencyInput);
        Assert.False(demo.IsUrgencyStale);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("urgency: 2", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void CargoDetailsApplyTogetherAndRejectInvalidOrStaleDrafts()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.SelectedCargo = "cargo.alternate";
        demo.UnitsInput = "7";
        demo.ModelInput = "invalid";
        demo.ActionCommand.Execute("cargo-details");
        Assert.Equal(original, demo.PreviewText);
        demo.ModelInput = "4";
        demo.ActionCommand.Execute("cargo-details");
        Assert.Equal("cargo.alternate", demo.Cargo);
        Assert.Contains("units_count: 7", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("cargo_model_index: 4", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectedCargo = "cargo.third";
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        Assert.True(demo.IsCargoDetailsStale);
        demo.ActionCommand.Execute("cargo-details");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("cargo-details-reviewed");
        Assert.Equal("cargo.third", demo.Cargo);
        Assert.False(demo.IsCargoDetailsStale);
    }

    [Fact]
    public void ObservedCargoSelectionUsesHistoryAndRequiresReviewWhenStale()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Contains("cargo.alternate", demo.CargoChoices);
        demo.SelectedCargo = "cargo.alternate";
        demo.ActionCommand.Execute("cargo");
        Assert.Equal("cargo.alternate", demo.Cargo);
        demo.SelectedCargo = "cargo.third";
        demo.ActionCommand.Execute("undo");
        Assert.Equal("cargo.synthetic", demo.Cargo);
        Assert.True(demo.IsCargoStale);
        demo.ActionCommand.Execute("cargo");
        Assert.Equal("cargo.synthetic", demo.Cargo);
        demo.ActionCommand.Execute("cargo-reviewed");
        Assert.Equal("cargo.third", demo.Cargo);
        Assert.False(demo.IsCargoStale);
        demo.ActionCommand.Execute("undo");
        Assert.Equal("cargo.synthetic", demo.Cargo);
    }

    [Fact]
    public void DiscardCargoDraftKeepsOtherDraftsAndRedo()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectedCargo = "cargo.alternate";
        demo.ActionCommand.Execute("cargo");
        demo.SelectedCargo = "cargo.third";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsCargoStale);
        demo.UnitsInput = "invalid";
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("discard-cargo");
        Assert.Equal("cargo.synthetic", demo.SelectedCargo);
        Assert.False(demo.IsCargoStale);
        Assert.Equal("invalid", demo.UnitsInput);
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("cargo.alternate", demo.Cargo);
        Assert.Equal("cargo.alternate", demo.SelectedCargo);
    }

    [Fact]
    public void DiscardingJobDraftsIndividuallyPreservesOtherDraftsAndRedo()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.ModelInput = "2";
        demo.ActionCommand.Execute("model");
        demo.ModelInput = "3";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsModelStale);
        demo.UnitsInput = "invalid";
        demo.DistanceInput = "250";
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("discard-model");
        Assert.Equal("0", demo.ModelInput);
        Assert.False(demo.IsModelStale);
        Assert.Equal("invalid", demo.UnitsInput);
        Assert.Equal("250", demo.DistanceInput);
        demo.ActionCommand.Execute("discard-units");
        Assert.Equal("3", demo.UnitsInput);
        Assert.Equal("250", demo.DistanceInput);
        demo.ActionCommand.Execute("discard-distance");
        Assert.Equal("120", demo.DistanceInput);
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("cargo_model_index: 2", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void CargoModelIndexDraftIsValidatedAndRequiresReviewWhenStale()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.ModelInput = "2147483648";
        demo.ActionCommand.Execute("model");
        Assert.Equal(original, demo.PreviewText);
        demo.ModelInput = "2";
        demo.ActionCommand.Execute("model");
        Assert.Contains("cargo_model_index: 2", demo.PreviewText, StringComparison.Ordinal);
        demo.ModelInput = "3";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsModelStale);
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("model");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("model-reviewed");
        Assert.Contains("cargo_model_index: 3", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
    }

    [Fact]
    public void RouteSwapIsVisibleAndUndoableWithoutChangingCargo()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        Assert.Contains("Source: company.demo_source → target: company.demo_target", demo.Route, StringComparison.Ordinal);
        demo.ActionCommand.Execute("swap-route");
        Assert.Contains("Source: company.demo_target → target: company.demo_source", demo.Route, StringComparison.Ordinal);
        Assert.Equal("cargo.synthetic", demo.Cargo);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("Source: company.demo_target", demo.Route, StringComparison.Ordinal);
    }
    [Fact]
    public void ApplyBothIsOneUndoableEditAndRejectsInvalidDraft()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.UnitsInput = "7";
        demo.DistanceInput = "invalid";
        demo.ActionCommand.Execute("apply-both");
        Assert.Equal(original, demo.PreviewText);
        demo.DistanceInput = "250";
        demo.ActionCommand.Execute("apply-both");
        Assert.Contains("units_count: 7", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("planned_distance_km: 250", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("planned_distance_km: 250", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyBothRejectsEitherStaleDraftWithoutPartialChange()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.UnitsInput = "7";
        demo.ActionCommand.Execute("apply");
        demo.UnitsInput = "8";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsUnitsStale);
        string before = demo.PreviewText;
        demo.DistanceInput = "250";
        demo.ActionCommand.Execute("apply-both");
        Assert.Equal(before, demo.PreviewText);
        Assert.True(demo.IsUnitsStale);
        Assert.True(demo.IsEitherStale);
        demo.DistanceInput = "invalid";
        demo.ActionCommand.Execute("apply-both-reviewed");
        Assert.Equal(before, demo.PreviewText);
        demo.DistanceInput = "250";
        demo.ActionCommand.Execute("apply-both-reviewed");
        Assert.Contains("units_count: 8", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("planned_distance_km: 250", demo.PreviewText, StringComparison.Ordinal);
        Assert.False(demo.IsEitherStale);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
    }
    [Fact]
    public void DistanceEditingUpdatesPreviewAndCanBeUndone()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string before = demo.PreviewText;
        demo.DistanceInput = "500";
        demo.ActionCommand.Execute("distance");
        Assert.Contains("planned_distance_km: 500", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("planned_distance_km: 120 → 500", demo.ChangeSummary, StringComparison.Ordinal);
        Assert.Contains("units_count: 3", demo.PreviewText, StringComparison.Ordinal);
        demo.DistanceInput = "-1";
        demo.ActionCommand.Execute("distance");
        Assert.Contains("planned_distance_km: 500", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
        Assert.Equal("No changes.", demo.ChangeSummary);
        Assert.Equal("-1", demo.DistanceInput);
    }
    [Fact]
    public void HistoryLimitIsExplainedAndLastPreviewRemainsIntact()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        for (int i = 1; i <= 15; i++)
        {
            demo.UnitsInput = (i + 10).ToString(CultureInfo.InvariantCulture);
            demo.ActionCommand.Execute("apply");
        }
        string before = demo.PreviewText;
        demo.UnitsInput = "100";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(before, demo.PreviewText);
        Assert.Contains("history is full", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        demo.UnitsInput = "100";
        Assert.True(demo.IsUnitsStale);
        demo.ActionCommand.Execute("apply-reviewed");
        Assert.Contains("units_count: 100", demo.PreviewText, StringComparison.Ordinal);
    }
    [Fact]
    public void ScratchJobValidatesAndUndoRedoPreservesUnknownData()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Equal("cargo.synthetic", demo.Cargo);
        Assert.Contains("cannot change a game file", demo.Description, StringComparison.Ordinal);
        string original = demo.PreviewText;
        demo.UnitsInput = "42";
        demo.ActionCommand.Execute("apply");
        Assert.Contains("units_count: 42", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"preserved\"", demo.PreviewText, StringComparison.Ordinal);
        string changed = demo.PreviewText;
        demo.UnitsInput = "-1";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(changed, demo.PreviewText);
        Assert.Contains("Invalid", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        Assert.Equal("-1", demo.UnitsInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal(changed, demo.PreviewText);
        Assert.Equal("-1", demo.UnitsInput);
    }
}
