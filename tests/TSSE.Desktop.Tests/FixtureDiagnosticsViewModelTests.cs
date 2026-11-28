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
        demo.Selected = demo.Examples.Single(item => item.Key == "duplicates");
        Assert.Contains("block IDs: 1", demo.Duplicates, StringComparison.Ordinal);
        Assert.Contains("blocks: 1", demo.Duplicates, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "legacy-info");
        Assert.Contains("55 (compatibility unverified)", demo.LegacyVersion, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "coaches");
        Assert.Contains("Synthetic historical Coaches preview", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("XP: 4321", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("referenced stops: 1", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("job-log entries: 1", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("bus references: 1", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("current bus-job reference: resolved", demo.CoachSummary, StringComparison.Ordinal);
        Assert.Contains("vehicle modes my/assigned: truck/coach", demo.CoachSummary, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "opaque");
        Assert.Contains("unclassified syntax", demo.Status, StringComparison.Ordinal);
        Assert.Contains("Incomplete", demo.Duplicates, StringComparison.Ordinal);
        Assert.Contains("yes", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "unclosed");
        Assert.Contains("invalid or incomplete", demo.Status, StringComparison.Ordinal);
        Assert.Contains("UnclosedBlock", demo.Error, StringComparison.Ordinal);
        Assert.Contains("not performed", demo.RoundTrip, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "unexpected-close");
        Assert.Contains("line 1, column 1", demo.Error, StringComparison.Ordinal);
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
        demo.Selected = demo.Examples.Single(item => item.Key == "unexpected-close");
        Assert.Contains("Zeile 1, Spalte 1", demo.Error, StringComparison.Ordinal);
        demo.Selected = demo.Examples.Single(item => item.Key == "binary");
        Assert.Contains("BSII-Formatversionshinweis: 2", demo.BinaryHeader, StringComparison.Ordinal);
        Assert.Contains("ungeprüft", demo.BinaryHeader, StringComparison.Ordinal);
        Assert.Contains("nicht verfügbar", demo.Duplicates, StringComparison.Ordinal);
        Assert.Contains("Binärpräfix", demo.Signature, StringComparison.Ordinal);
        Assert.Contains("nicht durchgeführt", demo.RoundTrip, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitFileBytesAreDecodedInMemoryAndNeverReportedAsCompatible()
    {
        var viewModel = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        byte[] bytes = "SiiNunit\n{\n demo : sample {\n  unknown: \"kept\"\n }\n}\n"u8.ToArray();

        viewModel.InspectSelectedBytes(bytes);

        Assert.Contains("Decoded text SII payload", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("2 parsed blocks", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("not game compatibility", viewModel.ImportResult, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitTextImportDisplaysOnlyUnambiguousKnownProfileValues()
    {
        var viewModel = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        byte[] bytes = "SiiNunit\n{\n economy : e {\n  bank: b\n  experience_points: 500\n  extra: \"kept\"\n }\n bank : b {\n  money_account: 987654\n }\n}\n"u8.ToArray();

        viewModel.InspectSelectedBytes(bytes);

        Assert.Contains("balance 987654; XP 500", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("mapping unverified", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.DoesNotContain("extra", viewModel.ImportResult, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitTextImportLabelsHistoricalCoachFieldsWithoutClaimingDlcSupport()
    {
        var viewModel = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("en")));
        byte[] bytes = "SiiNunit\n{\n economy : e {\n  bus_experience_points: 55\n }\n}\n"u8.ToArray();

        viewModel.InspectSelectedBytes(bytes);

        Assert.Contains("Historical Coaches fields detected", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("schema compatibility unverified", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("current DLC support is unconfirmed", viewModel.ImportResult, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSelectedBytesReturnControlledFailure()
    {
        var viewModel = new FixtureDiagnosticsViewModel(new UiTextCatalog(CultureInfo.GetCultureInfo("de")));

        viewModel.InspectSelectedBytes("not a save"u8);

        Assert.Contains("nicht unterstützte oder ungültige Nutzdaten", viewModel.ImportResult, StringComparison.Ordinal);
        Assert.Contains("keine Daten geändert", viewModel.ImportResult, StringComparison.Ordinal);
    }
}
