using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class VehicleDemoViewModelTests
{
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
