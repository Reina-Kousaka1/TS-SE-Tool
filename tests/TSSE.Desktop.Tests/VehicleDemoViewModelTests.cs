using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class VehicleDemoViewModelTests
{
    [Fact]
    public void TrailerAssignmentUsesSharedHistoryAndLeavesTruckAssignmentIntact()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var workspace = new DemoWorkspace();
        var vehicle = new VehicleDemoViewModel(text, workspace);
        var profile = new ProfileDemoViewModel(text, workspace);
        vehicle.SelectCategory("Trailers");
        string original = vehicle.PreviewText;
        vehicle.SelectedTrailer = "demo.trailer2";
        vehicle.ActionCommand.Execute("assign-trailer");
        Assert.Contains("assigned_trailer: demo.trailer2", vehicle.PreviewText, StringComparison.Ordinal);
        Assert.Contains("assigned_truck: demo.truck", vehicle.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo.trailer2", vehicle.AssignedTrailerLabel, StringComparison.Ordinal);
        profile.ActionCommand.Execute("undo");
        Assert.Equal(original, vehicle.PreviewText);
        profile.ActionCommand.Execute("redo");
        Assert.Contains("demo.trailer2", vehicle.AssignedTrailerLabel, StringComparison.Ordinal);
    }
    [Fact]
    public void ExplicitTruckAssignmentIsUndoableAcrossSections()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var workspace = new DemoWorkspace();
        var vehicle = new VehicleDemoViewModel(text, workspace);
        var profile = new ProfileDemoViewModel(text, workspace);
        string original = vehicle.PreviewText;
        Assert.Contains("demo.truck", vehicle.AssignedTruckLabel, StringComparison.Ordinal);
        vehicle.SelectedTruck = "demo.truck2";
        vehicle.ActionCommand.Execute("assign-truck");
        Assert.Contains("assigned_truck: demo.truck2", vehicle.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo.truck2", vehicle.AssignedTruckLabel, StringComparison.Ordinal);
        profile.ActionCommand.Execute("undo");
        Assert.Equal(original, vehicle.PreviewText);
        Assert.Contains("demo.truck", vehicle.AssignedTruckLabel, StringComparison.Ordinal);
        profile.ActionCommand.Execute("redo");
        Assert.Contains("demo.truck2", vehicle.AssignedTruckLabel, StringComparison.Ordinal);
    }
    [Fact]
    public void FullTruckRepairTargetsSelectedTruckAndCanBeUndone()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectedTruck = "demo.truck2";
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("truck-all");
        Assert.Contains("engine_wear: 0.2", demo.PreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("engine_wear: 0.4", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("fuel_relative: 0.25", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"second truck\"", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.DoesNotContain("engine_wear: 0.4", demo.PreviewText, StringComparison.Ordinal);
    }
    [Fact]
    public void FullTrailerChainRepairIsVisibleAndUndoableAsOneDemoChange()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectCategory("Trailers");
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("chain-all");
        Assert.DoesNotContain("cargo_damage: 0.25", demo.PreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("wheels_wear[0]: 0.6", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"second\"", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.DoesNotContain("cargo_damage: 0.25", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void FullRepairStartsAtSelectedTrailer()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectCategory("Trailers");
        demo.SelectedTrailer = "demo.trailer2";
        demo.ActionCommand.Execute("chain-all");
        Assert.Contains("cargo_damage: 0.1", demo.PreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("cargo_damage: 0.25", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("trailer_body_wear: 0.2", demo.PreviewText, StringComparison.Ordinal);
    }
    [Fact]
    public void TruckSelectionIsolatesRatioAndOdometerDraftsAndSharesHistory()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Equal(new[] { "demo.truck", "demo.truck2" }, demo.TruckChoices);
        string original = demo.PreviewText;
        demo.ValueInput = "0.8";
        demo.OdometerInput = "200";
        demo.SelectedTruck = "demo.truck2";
        Assert.Equal("0.25", demo.ValueInput);
        Assert.Equal("700", demo.OdometerInput);
        Assert.Contains("No example accessories", demo.AccessorySummary, StringComparison.Ordinal);
        demo.ValueInput = "0.9";
        demo.ActionCommand.Execute("apply");
        demo.OdometerInput = "800";
        demo.ActionCommand.Execute("odometer");
        Assert.Contains("odometer: 800", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"second truck\"", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectedTruck = "demo.truck";
        Assert.Equal("0.8", demo.ValueInput);
        Assert.Equal("200", demo.OdometerInput);
        demo.ActionCommand.Execute("undo");
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.SelectedTruck = "demo.truck2";
        Assert.Equal("0.25", demo.ValueInput);
        Assert.Equal("700", demo.OdometerInput);
    }

    [Fact]
    public void TruckDraftBecomesStaleAfterAnotherViewUndoesThatTruck()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var workspace = new DemoWorkspace();
        var first = new VehicleDemoViewModel(text, workspace);
        var second = new VehicleDemoViewModel(text, workspace);
        first.SelectedTruck = "demo.truck2";
        first.OdometerInput = "800";
        first.ActionCommand.Execute("odometer");
        first.OdometerInput = "900";
        first.SelectedTruck = "demo.truck";
        second.ActionCommand.Execute("undo");
        first.SelectedTruck = "demo.truck2";
        Assert.Equal("900", first.OdometerInput);
        Assert.True(first.IsOdometerStale);
        string before = first.PreviewText;
        first.ActionCommand.Execute("odometer");
        Assert.Equal(before, first.PreviewText);
        first.ActionCommand.Execute("odometer-reviewed");
        Assert.Contains("odometer: 900", first.PreviewText, StringComparison.Ordinal);
    }
    [Fact]
    public void TrailerSelectionEditsOnlyChosenTrailerAndUsesSharedHistory()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectCategory("Trailers");
        Assert.Equal(new[] { "demo.trailer", "demo.trailer2" }, demo.TrailerChoices);
        Assert.Contains("demo.trailer — trailer block", demo.PlayerTrailerSummary, StringComparison.Ordinal);
        Assert.Contains("demo.trailer2 — trailer block", demo.PlayerTrailerSummary, StringComparison.Ordinal);
        string original = demo.PreviewText;
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("0.25", demo.ValueInput);
        Assert.Contains("No example accessories", demo.AccessorySummary, StringComparison.Ordinal);
        demo.ValueInput = "0.75";
        demo.ActionCommand.Execute("apply");
        Assert.Contains("cargo_damage: 0.1", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("cargo_damage: 0.75", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"second\"", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectedTrailer = "demo.trailer";
        Assert.Equal("0.1", demo.ValueInput);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("0.75", demo.ValueInput);
    }

    [Fact]
    public void DraftsAreIsolatedPerTrailerAndStaleAfterChainRepair()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.SelectCategory("Trailers");
        demo.SelectedField = demo.Fields.Single(item => item.Name == "trailer_body_wear");
        demo.ValueInput = "0.9";
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("0.7", demo.ValueInput);
        demo.ValueInput = "0.6";
        demo.SelectedTrailer = "demo.trailer";
        Assert.Equal("0.9", demo.ValueInput);
        demo.ActionCommand.Execute("chain");
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("0.6", demo.ValueInput);
        Assert.True(demo.IsStaleDraft);
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("apply");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("apply-stale");
        Assert.Contains("trailer_body_wear: 0.6", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectedTrailer = "not-a-demo-trailer";
        Assert.Equal("demo.trailer2", demo.SelectedTrailer);
    }
    [Theory]
    [InlineData("1.00000001")]
    [InlineData("1.00000000000000001")]
    [InlineData("-1e-99")]
    [InlineData("1e-99")]
    public void RoundedOrUnderflowedDraftCannotChangeVehicle(string input)
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.ValueInput = input;
        demo.ActionCommand.Execute("apply");
        Assert.Equal(original, demo.PreviewText);
        Assert.Contains("Invalid", demo.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void OdometerDemoValidatesBoundsPreservesFractionAndSupportsHistory()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.OdometerInput = "4294967296";
        demo.ActionCommand.Execute("odometer");
        Assert.Equal(original, demo.PreviewText);
        demo.OdometerInput = "4294967295";
        demo.ActionCommand.Execute("odometer");
        Assert.Contains("odometer: 4294967295", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("odometer_float_part: &3f000000", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("odometer: 4294967295", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void OdometerDraftRequiresReviewAfterSharedUndo()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.OdometerInput = "200";
        demo.ActionCommand.Execute("odometer");
        demo.OdometerInput = "300";
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsOdometerStale);
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("odometer");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("odometer-reviewed");
        Assert.Contains("odometer: 300", demo.PreviewText, StringComparison.Ordinal);
        Assert.False(demo.IsOdometerStale);
        demo.SelectCategory("Trailers");
        demo.OdometerInput = "400";
        demo.ActionCommand.Execute("odometer");
        Assert.Contains("odometer: 400", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void TrailerOdometerDraftsAreIsolatedByVehicleAndCategory()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.OdometerInput = "111";
        demo.SelectCategory("Trailers");
        Assert.Equal("500", demo.OdometerInput);
        demo.OdometerInput = "600";
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("900", demo.OdometerInput);
        demo.OdometerInput = "1000";
        demo.ActionCommand.Execute("odometer");
        demo.SelectedTrailer = "demo.trailer";
        Assert.Equal("600", demo.OdometerInput);
        demo.ActionCommand.Execute("odometer");
        demo.ActionCommand.Execute("undo");
        Assert.Equal("500", demo.OdometerInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("600", demo.OdometerInput);
        demo.SelectCategory("Trucks");
        Assert.Equal("111", demo.OdometerInput);
        demo.SelectCategory("Trailers");
        demo.SelectedTrailer = "demo.trailer2";
        Assert.Equal("1000", demo.OdometerInput);
    }
    [Fact]
    public void AccessoryInspectionReportsSyntheticStatusesWithoutChangingTheWorkspace()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string before = demo.PreviewText;
        Assert.Contains("demo.accessory — known legacy type", demo.AccessorySummary, StringComparison.Ordinal);
        Assert.Contains("demo.mod — unknown type", demo.AccessorySummary, StringComparison.Ordinal);
        Assert.Contains("demo.missing — missing target", demo.AccessorySummary, StringComparison.Ordinal);
        Assert.Equal(before, demo.PreviewText);
        demo.SelectCategory("Trailers");
        Assert.Contains("demo.accessory — known legacy type", demo.AccessorySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("demo.missing", demo.AccessorySummary, StringComparison.Ordinal);
        Assert.Equal(before, demo.PreviewText);
    }
    [Fact]
    public void FieldAndCategorySwitchingPreservesDraftAndFlagsStaleBase()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var workspace = new DemoWorkspace();
        var vehicle = new VehicleDemoViewModel(text, workspace);
        var job = new JobDemoViewModel(text, workspace);
        vehicle.ValueInput = "1";
        vehicle.ActionCommand.Execute("apply");
        vehicle.ValueInput = "0.3";
        vehicle.SelectedField = vehicle.Fields.Single(item => item.Name == "engine_wear");
        vehicle.ValueInput = "not yet valid";
        vehicle.SelectCategory("Trailers");
        vehicle.ValueInput = "0.6";
        vehicle.SelectCategory("Trucks");
        Assert.Equal("0.3", vehicle.ValueInput);
        vehicle.SelectedField = vehicle.Fields.Single(item => item.Name == "engine_wear");
        Assert.Equal("not yet valid", vehicle.ValueInput);
        job.ActionCommand.Execute("undo");
        vehicle.SelectedField = vehicle.Fields.Single(item => item.Name == "fuel_relative");
        Assert.Equal("0.3", vehicle.ValueInput);
        Assert.True(vehicle.IsStaleDraft);
        Assert.Contains("0.5", vehicle.DraftNotice, StringComparison.Ordinal);
        string before = vehicle.PreviewText;
        vehicle.ActionCommand.Execute("apply");
        Assert.Equal(before, vehicle.PreviewText);
        vehicle.ActionCommand.Execute("apply-stale");
        Assert.Contains("fuel_relative: 0.3", vehicle.PreviewText, StringComparison.Ordinal);
        Assert.False(vehicle.IsStaleDraft);
        vehicle.SelectCategory("Trailers");
        Assert.Equal("0.6", vehicle.ValueInput);
    }
    [Fact]
    public void TrailerChainDemoRepairsBothBodiesInOneUndo()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.False(demo.CanRepairChain);
        demo.SelectCategory("Trailers");
        Assert.True(demo.CanRepairChain);
        string before = demo.PreviewText;
        demo.ActionCommand.Execute("chain");
        Assert.DoesNotContain("trailer_body_wear: 0.7", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"second\"", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
    }
    [Fact]
    public void WheelRepairIsSeparateAndUndoableInBothCategories()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.ActionCommand.Execute("wheels");
        Assert.Contains("wheels_wear[0]: 0", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("engine_wear: 0.2", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Contains("wheels_wear[0]: 0.2", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectCategory("Trailers");
        demo.ActionCommand.Execute("wheels");
        Assert.Contains("wheels_wear[0]: 0", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Contains("wheels_wear[0]: 0.4", demo.PreviewText, StringComparison.Ordinal);
    }
    [Fact]
    public void RepairAndRefuelAreIndependentUndoableSyntheticOperations()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string original = demo.PreviewText;
        demo.ActionCommand.Execute("repair");
        Assert.Contains("engine_wear: 0\n", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("fuel_relative: 0.5", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("refuel");
        Assert.Contains("fuel_relative: 1", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Contains("fuel_relative: 0.5", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        demo.SelectCategory("Trailers");
        Assert.False(demo.CanRefuel);
    }
    [Fact]
    public void VehicleCategoriesShareHistoryAndUndoRedoUpdatesPreview()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.ValueInput = "1";
        demo.ActionCommand.Execute("apply");
        Assert.Contains("fuel_relative: 1", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"preserved\"", demo.PreviewText, StringComparison.Ordinal);
        demo.SelectCategory("Trailers");
        demo.ValueInput = "0";
        demo.ActionCommand.Execute("apply");
        Assert.Contains("cargo_damage: 0", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal("0.1", demo.ValueInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("0", demo.ValueInput);
        demo.SelectCategory("Trucks");
        Assert.Contains("fuel_relative: 1", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("cargo_damage: 0\n", demo.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidInputCannotChangeSyntheticData()
    {
        var demo = new VehicleDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("de")));
        string before = demo.PreviewText;
        demo.ValueInput = "NaN";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(before, demo.PreviewText);
        Assert.Contains("Ungültiger", demo.Message, StringComparison.Ordinal);
        demo.ValueInput = "1,5";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(before, demo.PreviewText);
    }
}
