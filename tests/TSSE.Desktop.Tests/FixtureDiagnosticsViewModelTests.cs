using System.Globalization;
using TSSE.Desktop.Services;
using TSSE.Desktop.ViewModels;
using Xunit;

namespace TSSE.Desktop.Tests;

public sealed class FixtureDiagnosticsViewModelTests
{
    [Fact]
    public void SyntheticExamplesDistinguishSyntaxHintAndByteEquality()
    {
        var demo = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        Assert.Contains("parsed syntax only", demo.Status, StringComparison.Ordinal);
        Assert.Contains("text prefix", demo.Signature, StringComparison.Ordinal);
        Assert.Contains("yes", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "opaque");
        Assert.Contains("unclassified syntax", demo.Status, StringComparison.Ordinal);
        Assert.Contains("yes", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "unclosed");
        Assert.Contains("invalid or incomplete", demo.Status, StringComparison.Ordinal);
        Assert.Contains("UnclosedBlock", demo.Error, StringComparison.Ordinal);
        Assert.Contains("not performed", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "encrypted");
        Assert.Contains("unsupported", demo.Status, StringComparison.Ordinal);
        Assert.Contains("encrypted prefix", demo.Signature, StringComparison.Ordinal);
        Assert.Contains("not performed", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = new FixtureDiagnosticsExample("external", "external");
        Assert.Equal("encrypted", demo.Selected.Key);
    }

    [Fact]
    public void GermanLabelsMakeTheSyntheticBoundaryVisible()
    {
        var demo = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("de")));
        Assert.Contains("kein Spielstand", demo.Description, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "binary");
        Assert.Contains("Binärpräfix", demo.Signature, StringComparison.Ordinal);
        Assert.Contains("nicht durchgeführt", demo.RoundTrip, StringComparison.Ordinal);
    }
}
