using TSSE.GameAdapters.Compatibility;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacySaveVersionInspectorTests
{
    [Fact]
    public void ReadsOnlyTheUniqueDirectContainerVersionWithoutChangingUnknownFields()
    {
        const string input = "SiiNunit\n{\n save_container : info {\n  version: 55\n  mod: \"未知\"\n }\n delivery_log : other {\n  version: 999\n }\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        var result = LegacySaveVersionInspector.Inspect(document);
        Assert.Equal(LegacySaveVersionStatus.LegacyFieldFound, result.Status);
        Assert.Equal((ushort)55, result.Version);
        Assert.Equal(input, document.ToText());
    }

    [Theory]
    [InlineData("version: 0", LegacySaveVersionStatus.InvalidVersion)]
    [InlineData("version: 65536", LegacySaveVersionStatus.InvalidVersion)]
    [InlineData("version: -1", LegacySaveVersionStatus.InvalidVersion)]
    [InlineData("version: 1\n version: 2", LegacySaveVersionStatus.AmbiguousVersion)]
    [InlineData("other: 55", LegacySaveVersionStatus.MissingVersion)]
    public void UnsupportedVersionValuesFailClosed(string field, LegacySaveVersionStatus status)
    {
        string input = "SiiNunit\n{\n save_container : info {\n " + field + "\n }\n}\n";
        var result = LegacySaveVersionInspector.Inspect(SiiSyntaxDocument.Parse(input));
        Assert.Equal(status, result.Status);
        Assert.Null(result.Version);
    }

    [Theory]
    [InlineData("root {\n version: 55\n}\n", LegacySaveVersionStatus.InvalidWrapper)]
    [InlineData("SiiNunit\n{\n other : x {\n version: 55\n }\n}\n", LegacySaveVersionStatus.MissingContainer)]
    [InlineData("SiiNunit\n{\n save_container : a {\n version: 1\n }\n save_container : b {\n version: 2\n }\n}\n", LegacySaveVersionStatus.AmbiguousContainer)]
    [InlineData("SiiNunit\n{\n other : x {\n  save_container : a {\n version: 55\n  }\n }\n}\n", LegacySaveVersionStatus.InvalidContainer)]
    [InlineData("SiiNunit\n{\n save_container : a {\n version: 55\n unsupported line\n }\n}\n", LegacySaveVersionStatus.UnclassifiedSyntax)]
    public void WrongStructureNeverProducesAVersion(string input, LegacySaveVersionStatus status)
    {
        var result = LegacySaveVersionInspector.Inspect(SiiSyntaxDocument.Parse(input));
        Assert.Equal(status, result.Status);
        Assert.Null(result.Version);
    }
}
