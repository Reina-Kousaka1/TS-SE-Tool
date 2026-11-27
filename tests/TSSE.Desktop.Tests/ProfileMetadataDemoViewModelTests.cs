using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class ProfileMetadataDemoViewModelTests
{
    [Fact]
    public void SyntheticNameCanBeChangedUndoneAndRedone()
    {
        var viewModel = new ProfileMetadataDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Contains("100", viewModel.CachedValues, StringComparison.Ordinal);
        viewModel.Input = "New_Profile";
        viewModel.ActionCommand.Execute("apply");
        Assert.Contains("New_Profile", viewModel.CurrentName, StringComparison.Ordinal);
        viewModel.ActionCommand.Execute("undo");
        Assert.Equal("Example_1", viewModel.Input);
        viewModel.ActionCommand.Execute("redo");
        Assert.Equal("New_Profile", viewModel.Input);
        viewModel.Input = "unsupported\"name";
        viewModel.ActionCommand.Execute("apply");
        Assert.Contains("New_Profile", viewModel.CurrentName, StringComparison.Ordinal);
        Assert.Contains("No change", viewModel.Message, StringComparison.Ordinal);
        viewModel.Input = "名前 😀";
        viewModel.ActionCommand.Execute("apply");
        Assert.Contains("名前 😀", viewModel.CurrentName, StringComparison.Ordinal);
    }
}
