using TSSE.GameAdapters.Compatibility;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacySaveCompatibilityProbeTests
{
    private const string Fixture = "SiiNunit\n{\n save_container : demo {\n version: 55\n mod: \"未知\"\n }\n}\n";

    [Theory]
    [InlineData("ETS2")]
    [InlineData("ATS")]
    public void UnknownVersionCannotAuthorizeAccess(string game)
    {
        var registry = new GameAdapterRegistry();
        var document = SiiSyntaxDocument.Parse(Fixture);
        var report = LegacySaveCompatibilityProbe.Inspect(document, game, registry);
        Assert.Equal((ushort)55, report.Version.Version);
        Assert.Equal(CompatibilityState.UnsupportedUnknown, report.RegistryAssessment?.State);
        Assert.False(report.CanReadGameSave);
        Assert.False(report.CanWriteGameSave);
        Assert.Equal(Fixture, document.ToText());
        Assert.Equal(0, registry.RegistrationCount);
    }

    [Fact]
    public void ATestOnlyRegistryMatchDoesNotAuthorizeReadingOrWriting()
    {
        var registry = new GameAdapterRegistry();
        // This entry exists only to test the boundary, not to claim game compatibility.
        registry.RegisterVerified(new GameAdapterRegistration("synthetic-game", "55", true, true, "test-only"));
        var document = SiiSyntaxDocument.Parse(Fixture);
        var report = LegacySaveCompatibilityProbe.Inspect(document, "synthetic-game", registry);
        Assert.Equal(CompatibilityState.ReadWriteVerified, report.RegistryAssessment?.State);
        Assert.False(report.CanReadGameSave);
        Assert.False(report.CanWriteGameSave);
        Assert.Equal(CompatibilityState.UnsupportedUnknown,
            LegacySaveCompatibilityProbe.Inspect(document, "ETS2", registry).RegistryAssessment?.State);
    }

    [Fact]
    public void MalformedVersionNeverConsultsAnAdapterOrProducesPermission()
    {
        var registry = new GameAdapterRegistry();
        registry.RegisterVerified(new GameAdapterRegistration("synthetic-game", "55", true, true, "test-only"));
        string malformed = Fixture.Replace("version: 55", "version: 55\n version: 56", StringComparison.Ordinal);
        var document = SiiSyntaxDocument.Parse(malformed);
        var report = LegacySaveCompatibilityProbe.Inspect(document, "synthetic-game", registry);
        Assert.Equal(LegacySaveVersionStatus.AmbiguousVersion, report.Version.Status);
        Assert.Null(report.RegistryAssessment);
        Assert.False(report.CanReadGameSave);
        Assert.False(report.CanWriteGameSave);
        Assert.Equal(malformed, document.ToText());
    }
}
