using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyProfileReadInspectorTests
{
    [Fact]
    public void ReadsKnownLegacyValuesWithoutRejectingUnknownSyntax()
    {
        const string input = "SiiNunit\n{\n economy : e {\n  bank: b\n  experience_points: 0005\n  bus_experience_points: -2\n  unknown syntax retained\n }\n bank : b {\n  money_account: -42\n  mod: \"kept\"\n }\n}\n";
        var document = SiiSyntaxDocument.Parse(input);

        var values = LegacyProfileReadInspector.InspectText(document);

        Assert.Equal(-42, values.Balance);
        Assert.Equal(5u, values.Experience);
        Assert.Equal(-2, values.CoachExperience);
        Assert.Single(document.OpaqueLines);
        Assert.Contains("unknown syntax retained", document.ToText(), StringComparison.Ordinal);
        Assert.Contains("mod: \"kept\"", document.ToText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("economy : e {\nbank: b\nexperience_points: 1\n}\nbank : b {\nmoney_account: 5\n}\neconomy : f {\nbank: b\nexperience_points: 2\n}")]
    [InlineData("economy : e {\nbank: b\nexperience_points: 1\n}\nbank : b {\nmoney_account: not-a-number\n}")]
    [InlineData("economy : e {\nbank: b\nexperience_points: 4294967295\n}\nbank : b {\nmoney_account: 5\n}")]
    [InlineData("economy : e {\nbank: b\nbank: b\nexperience_points: 1\n}\nbank : b {\nmoney_account: 5\n}")]
    public void RejectsAmbiguousOrInvalidKnownFields(string input)
        => Assert.Throws<InvalidDataException>(() => LegacyProfileReadInspector.InspectText(SiiSyntaxDocument.Parse(input)));
}
