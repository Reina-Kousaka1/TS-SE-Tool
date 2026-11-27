using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class JobDemoViewModelTests
{
    [Fact]
    public void ScratchJobValidatesAndUndoRedoPreservesUnknownData()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Equal("cargo.synthetic", demo.Cargo);
        Assert.Contains("cannot change a game file", demo.Description, StringComparison.Ordinal);
        string original = demo.PreviewText;
        demo.UnitsInput = "42";
        demo.ActionCommand.Execute("apply");
        Assert.Contains("units_count: 42", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("demo_unknown: \"preserved\"", demo.PreviewText, StringComparison.Ordinal);
        string changed = demo.PreviewText;
        demo.UnitsInput = "-1";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(changed, demo.PreviewText);
        Assert.Contains("Invalid", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(original, demo.PreviewText);
        Assert.Equal("3", demo.UnitsInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal(changed, demo.PreviewText);
        Assert.Equal("42", demo.UnitsInput);
    }
}
