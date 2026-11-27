using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class ProfileDemoViewModelTests
{
    [Fact]
    public void DiscardingOneStaleDraftPreservesTheOtherAndUndoRedo()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        demo.BalanceInput = "10";
        demo.ActionCommand.Execute("balance");
        demo.ExperienceInput = "20";
        demo.ActionCommand.Execute("experience");
        demo.BalanceInput = "11";
        demo.ExperienceInput = "21";
        demo.ActionCommand.Execute("undo");
        demo.ActionCommand.Execute("undo");
        Assert.True(demo.IsBalanceStale);
        Assert.True(demo.IsExperienceStale);
        string unchanged = demo.PreviewText;
        demo.ActionCommand.Execute("discard-balance");
        Assert.Equal("0", demo.BalanceInput);
        Assert.Equal("21", demo.ExperienceInput);
        Assert.False(demo.IsBalanceStale);
        Assert.True(demo.IsExperienceStale);
        Assert.Equal(unchanged, demo.PreviewText);
        demo.ActionCommand.Execute("discard-experience");
        Assert.Equal("0", demo.ExperienceInput);
        Assert.False(demo.IsExperienceStale);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("10", demo.BalanceInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("20", demo.ExperienceInput);
    }

    [Fact]
    public void ScratchEditsValidateAndUndoWithoutLoadingASave()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Contains("not loaded from a game", demo.Description, StringComparison.Ordinal);
        demo.BalanceInput = "-123";
        demo.ActionCommand.Execute("balance");
        Assert.Contains("money_account: -123", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"preserved\"", demo.PreviewText, StringComparison.Ordinal);
        demo.ExperienceInput = "not a number";
        demo.ActionCommand.Execute("experience");
        Assert.Contains("Invalid", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Contains("money_account: 0", demo.PreviewText, StringComparison.Ordinal);
        Assert.Equal("0", demo.BalanceInput);
        Assert.Equal("not a number", demo.ExperienceInput);
        demo.ActionCommand.Execute("redo");
        Assert.Contains("money_account: -123", demo.PreviewText, StringComparison.Ordinal);
        Assert.Equal("-123", demo.BalanceInput);
        Assert.Contains("No game file was changed", demo.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanDemoClearlyDescribesTemporaryData()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("de")));
        Assert.Contains("nicht aus einem Spiel", demo.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyLevelSelectionUpdatesOnlySyntheticExperienceAndSupportsUndo()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en-US")));
        demo.LevelInput = "5";
        demo.ActionCommand.Execute("level");
        Assert.Equal("4399", demo.ExperienceInput);
        Assert.Contains("level 5", demo.LevelProgress, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"preserved\"", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("ats");
        Assert.Contains("ATS", demo.LevelProgress, StringComparison.Ordinal);
        string before = demo.PreviewText;
        demo.LevelInput = "151";
        demo.ActionCommand.Execute("level");
        Assert.Equal(before, demo.PreviewText);
        demo.ActionCommand.Execute("undo");
        Assert.Equal("0", demo.ExperienceInput);
    }
}
