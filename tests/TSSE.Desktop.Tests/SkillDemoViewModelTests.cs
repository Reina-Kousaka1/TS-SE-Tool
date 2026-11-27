using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class SkillDemoViewModelTests
{
    [Fact]
    public void SkillEditingSharesUndoAndRejectsInvalidValues()
    {
        var workspace = new DemoWorkspace();
        var profile = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")), workspace);
        var skills = profile.Skills;
        string original = profile.PreviewText;
        skills.Input = "64";
        skills.ActionCommand.Execute("apply");
        Assert.Equal(original, profile.PreviewText);
        skills.Input = "63";
        skills.ActionCommand.Execute("apply");
        Assert.Contains("adr: 63", profile.PreviewText, StringComparison.Ordinal);
        skills.Selected = skills.Fields.Single(item => item.Name == "heavy");
        skills.Input = "6";
        skills.ActionCommand.Execute("apply");
        Assert.Contains("heavy: 6", profile.PreviewText, StringComparison.Ordinal);
        profile.ActionCommand.Execute("undo");
        Assert.Contains("heavy: 0", profile.PreviewText, StringComparison.Ordinal);
        profile.ActionCommand.Execute("undo");
        Assert.Equal(original, profile.PreviewText);
        profile.ActionCommand.Execute("redo");
        Assert.Contains("adr: 63", profile.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftsSurviveSelectionAndStaleDraftRequiresReview()
    {
        var profile = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        var skills = profile.Skills;
        skills.Input = "1";
        skills.ActionCommand.Execute("apply");
        skills.Input = "2";
        skills.Selected = skills.Fields.Single(item => item.Name == "heavy");
        skills.Input = "4";
        profile.ActionCommand.Execute("undo");
        skills.Selected = skills.Fields.Single(item => item.Name == "adr");
        Assert.Equal("2", skills.Input);
        Assert.True(skills.IsStale);
        string before = profile.PreviewText;
        skills.ActionCommand.Execute("apply");
        Assert.Equal(before, profile.PreviewText);
        skills.ActionCommand.Execute("reviewed");
        Assert.Contains("adr: 2", profile.PreviewText, StringComparison.Ordinal);
        skills.Selected = skills.Fields.Single(item => item.Name == "heavy");
        Assert.Equal("4", skills.Input);
    }
}
