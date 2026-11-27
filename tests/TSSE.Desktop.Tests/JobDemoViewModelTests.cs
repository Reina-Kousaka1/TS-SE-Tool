using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class JobDemoViewModelTests
{
    [Fact]
    public void DistanceEditingUpdatesPreviewAndCanBeUndone()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        string before = demo.PreviewText;
        demo.DistanceInput = "500";
        demo.ActionCommand.Execute("distance");
        Assert.Contains("planned_distance_km: 500", demo.PreviewText, StringComparison.Ordinal);
        Assert.Contains("planned_distance_km: 120 → 500", demo.ChangeSummary, StringComparison.Ordinal);
        Assert.Contains("units_count: 3", demo.PreviewText, StringComparison.Ordinal);
        demo.DistanceInput = "-1";
        demo.ActionCommand.Execute("distance");
        Assert.Contains("planned_distance_km: 500", demo.PreviewText, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        Assert.Equal(before, demo.PreviewText);
        Assert.Equal("No changes.", demo.ChangeSummary);
        Assert.Equal("-1", demo.DistanceInput);
    }
    [Fact]
    public void HistoryLimitIsExplainedAndLastPreviewRemainsIntact()
    {
        var demo = new JobDemoViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        for (int i = 1; i <= 15; i++)
        {
            demo.UnitsInput = (i + 10).ToString(CultureInfo.InvariantCulture);
            demo.ActionCommand.Execute("apply");
        }
        string before = demo.PreviewText;
        demo.UnitsInput = "100";
        demo.ActionCommand.Execute("apply");
        Assert.Equal(before, demo.PreviewText);
        Assert.Contains("history is full", demo.Message, StringComparison.Ordinal);
        demo.ActionCommand.Execute("undo");
        demo.UnitsInput = "100";
        Assert.True(demo.IsUnitsStale);
        demo.ActionCommand.Execute("apply-reviewed");
        Assert.Contains("units_count: 100", demo.PreviewText, StringComparison.Ordinal);
    }
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
        Assert.Equal("-1", demo.UnitsInput);
        demo.ActionCommand.Execute("redo");
        Assert.Equal(changed, demo.PreviewText);
        Assert.Equal("-1", demo.UnitsInput);
    }
}
