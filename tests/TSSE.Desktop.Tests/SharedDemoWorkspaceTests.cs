using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using TSSE.GameAdapters.Compatibility;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class SharedDemoWorkspaceTests
{
    [Fact]
    public void CrossPageUndoRedoKeepsOtherChangesAndPreservesDrafts()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var catalog = new PageCatalog(new FoundationStatusService(new GameAdapterRegistry()), text);
        ProfileDemoViewModel profile = catalog.Sections.Single(section => section.PageKey == "savegame-explorer").ProfileDemo;
        VehicleDemoViewModel vehicle = catalog.Sections.Single(section => section.PageKey == "vehicle-studio").VehicleDemo;
        JobDemoViewModel job = catalog.Sections.Single(section => section.PageKey == "jobs").JobDemo;

        profile.BalanceInput = "25";
        profile.ActionCommand.Execute("balance");
        job.UnitsInput = "unfinished draft";
        vehicle.ValueInput = "1";
        vehicle.ActionCommand.Execute("apply");
        Assert.Contains("money_account: 25", job.PreviewText, StringComparison.Ordinal);
        Assert.Contains("fuel_relative: 1", profile.PreviewText, StringComparison.Ordinal);

        job.ActionCommand.Execute("undo");
        Assert.Equal("unfinished draft", job.UnitsInput);
        Assert.Equal("0.5", vehicle.ValueInput);
        Assert.Contains("money_account: 25", vehicle.PreviewText, StringComparison.Ordinal);
        profile.ActionCommand.Execute("redo");
        Assert.Equal("1", vehicle.ValueInput);
        Assert.Equal("unfinished draft", job.UnitsInput);

        job.UnitsInput = "8";
        job.ActionCommand.Execute("apply");
        Assert.Contains("units_count: 8", profile.PreviewText, StringComparison.Ordinal);
        vehicle.ActionCommand.Execute("undo");
        Assert.Equal("3", job.UnitsInput);
        Assert.Contains("fuel_relative: 1", job.PreviewText, StringComparison.Ordinal);
        Assert.Contains("money_account: 25", job.PreviewText, StringComparison.Ordinal);
    }
}
