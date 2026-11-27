using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class VehicleDemoViewModelTests
{
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
    public void VehicleSessionsRemainSeparateAndUndoRedoUpdatesPreview()
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
        Assert.Contains("cargo_damage: 0.1", demo.PreviewText, StringComparison.Ordinal);
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
