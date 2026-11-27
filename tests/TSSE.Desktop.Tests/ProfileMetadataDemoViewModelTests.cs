using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class ProfileMetadataDemoViewModelTests
{
    [Fact]
    public void UndoAndRedoPreserveDraftAndRequireReviewIfItsBaseChanged()
    {
        var vm = new ProfileMetadataDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        vm.Input = "First";
        vm.ActionCommand.Execute("apply");
        vm.Input = "名前";
        vm.ActionCommand.Execute("undo");
        Assert.Equal("名前", vm.Input);
        Assert.True(vm.IsStale);
        Assert.Contains("Example_1", vm.DraftNotice, StringComparison.Ordinal);
        vm.ActionCommand.Execute("apply");
        Assert.Contains("Example_1", vm.CurrentName, StringComparison.Ordinal);
        vm.ActionCommand.Execute("redo");
        Assert.Equal("名前", vm.Input);
        Assert.False(vm.IsStale);
        vm.ActionCommand.Execute("undo");
        vm.ActionCommand.Execute("apply-reviewed");
        Assert.Contains("名前", vm.CurrentName, StringComparison.Ordinal);
        Assert.False(vm.IsStale);
        vm.ActionCommand.Execute("undo");
        Assert.Contains("Example_1", vm.CurrentName, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscardingDraftPreservesRedoHistory()
    {
        var vm = new ProfileMetadataDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        vm.Input = "First";
        vm.ActionCommand.Execute("apply");
        vm.Input = "Draft";
        vm.ActionCommand.Execute("undo");
        Assert.True(vm.IsStale);
        vm.ActionCommand.Execute("discard-draft");
        Assert.Equal("Example_1", vm.Input);
        Assert.False(vm.IsStale);
        vm.ActionCommand.Execute("redo");
        Assert.Contains("First", vm.CurrentName, StringComparison.Ordinal);
    }

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
