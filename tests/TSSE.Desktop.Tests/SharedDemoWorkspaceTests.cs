using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using TSSE.GameAdapters.Compatibility;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class SharedDemoWorkspaceTests
{
    [Fact]
    public void StaleJobAndProfileDraftsRequireExplicitReview()
    {
        var text = new UiTextCatalog(CultureInfo.GetCultureInfo("en"));
        var workspace = new DemoWorkspace();
        var profile = new ProfileDemoViewModel(text, workspace);
        var job = new JobDemoViewModel(text, workspace);
        profile.BalanceInput = "20";
        profile.ActionCommand.Execute("balance");
        profile.BalanceInput = "30";
        job.ActionCommand.Execute("undo");
        Assert.Equal("30", profile.BalanceInput);
        Assert.True(profile.IsBalanceStale);
        string before = profile.PreviewText;
        profile.ActionCommand.Execute("balance");
        Assert.Equal(before, profile.PreviewText);
        profile.ActionCommand.Execute("balance-reviewed");
        Assert.Contains("money_account: 30", profile.PreviewText, StringComparison.Ordinal);
        Assert.False(profile.IsBalanceStale);

        job.UnitsInput = "10";
        job.ActionCommand.Execute("apply");
        job.UnitsInput = "15";
        profile.ActionCommand.Execute("undo");
        Assert.True(job.IsUnitsStale);
        Assert.Contains("3", job.UnitsDraftNotice, StringComparison.Ordinal);
        before = job.PreviewText;
        job.ActionCommand.Execute("apply");
        Assert.Equal(before, job.PreviewText);
        job.ActionCommand.Execute("apply-reviewed");
        Assert.Contains("units_count: 15", job.PreviewText, StringComparison.Ordinal);
        Assert.False(job.IsUnitsStale);
    }

    [Fact]
    public void LevelChangeKeepsAnExistingExperienceDraft()
    {
        var profile = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        profile.ExperienceInput = "99";
        profile.LevelInput = "5";
        profile.ActionCommand.Execute("level");
        Assert.Equal("99", profile.ExperienceInput);
        Assert.True(profile.IsExperienceStale);
        Assert.Contains("4399", profile.ExperienceDraftNotice, StringComparison.Ordinal);
        profile.ActionCommand.Execute("experience-reviewed");
        Assert.Contains("experience_points: 99", profile.PreviewText, StringComparison.Ordinal);
    }
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
