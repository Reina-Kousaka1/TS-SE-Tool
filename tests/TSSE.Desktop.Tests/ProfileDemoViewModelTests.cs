using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class ProfileDemoViewModelTests
{
    [Fact]
    public void ScratchEditsValidateAndUndoWithoutLoadingASave()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Contains("not loaded from a game", demo.Description, StringComparison.Ordinal);
        demo.BalanceInput = "-123";
        demo.ActionCommand.Execute("balance");
        demo.ExperienceInput = "not a number";
        demo.ActionCommand.Execute("experience");
        Assert.Contains("Invalid", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal("0", demo.BalanceInput);
        Assert.Equal("0", demo.ExperienceInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal("-123", demo.BalanceInput);
        Assert.Contains("No game file was changed", demo.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanDemoClearlyDescribesTemporaryData()
    {
        var demo = new ProfileDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("de")));
        Assert.Contains("nicht aus einem Spiel", demo.Description, StringComparison.Ordinal);
    }
}
