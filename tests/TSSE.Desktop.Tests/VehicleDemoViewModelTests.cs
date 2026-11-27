using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class VehicleDemoViewModelTests
{
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
        Assert.DoesNotContain("odometer: 400", demo.PreviewText, StringComparison.Ordinal);
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
