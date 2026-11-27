using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyAccessoryInspectorTests
{
    [Fact]
    public void DuplicateOwnerIdAcrossBlockTypesPreventsInspection()
    {
        const string input = "vehicle : v {\n accessories: 0\n}\nmod : v {\n unknown: preserved\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => LegacyAccessoryInspector.InspectDeclaredArray(document, "vehicle", "v"));
        Assert.Equal(input, document.ToText());
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("trailer")]
    [InlineData("unknown_mod_type")]
    public void ExplicitArrayInspectionPreservesOrderUnknownTypesAndDuplicateReferences(string type)
    {
        string input = type + " : owner {\n accessories: 5\n accessories[0]: a\n accessories[1]: mod\n accessories[2]: missing\n accessories[3]: a\n accessories[4]: nil\n}\n" +
            "vehicle_accessory : a {\n data_path: \"synthetic\"\n}\nunknown_mod_accessory : mod {\n payload: \"保持\"\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        IReadOnlyList<AccessoryReference> values = LegacyAccessoryInspector.InspectDeclaredArray(document, type, "owner");
        Assert.Equal(new[] { "a", "mod", "missing", "a", "nil" }, values.Select(item => item.RawReference));
        Assert.Equal(new[] { AccessoryReferenceState.ResolvedKnownType, AccessoryReferenceState.ResolvedUnknownType,
            AccessoryReferenceState.Missing, AccessoryReferenceState.ResolvedKnownType, AccessoryReferenceState.Empty },
            values.Select(item => item.State));
        Assert.Equal("unknown_mod_accessory", values[1].TargetBlockType);
        Assert.Equal(AccessoryDataPathState.Present, values[0].DataPathState);
        Assert.Equal("\"synthetic\"", values[0].RawDataPath);
        Assert.Equal(AccessoryDataPathState.NotInspected, values[1].DataPathState);
        Assert.Null(values[1].RawDataPath);
        Assert.Equal(input, document.ToText());
    }

    [Theory]
    [InlineData("other: 1", AccessoryDataPathState.Missing)]
    [InlineData("data_path:", AccessoryDataPathState.Empty)]
    [InlineData("data_path: a\n data_path: b", AccessoryDataPathState.Ambiguous)]
    public void KnownAccessoryDataPathDoesNotGuessMissingOrAmbiguousValues(string field, AccessoryDataPathState expected)
    {
        string input = "vehicle : v {\n accessories: 1\n accessories[0]: a\n}\nvehicle_accessory : a {\n " + field + "\n unknown: \"保持\"\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        var value = Assert.Single(LegacyAccessoryInspector.InspectDeclaredArray(document, "vehicle", "v"));
        Assert.Equal(expected, value.DataPathState);
        Assert.Null(value.RawDataPath);
        Assert.Equal(input, document.ToText());
    }

    [Fact]
    public void DuplicateTargetIdsAreReportedAsAmbiguous()
    {
        const string input = "vehicle : v {\n accessories: 1\n accessories[0]: a\n}\nvehicle_accessory : a {\n}\nmod_accessory : a {\n}\n";
        AccessoryReference result = Assert.Single(LegacyAccessoryInspector.InspectDeclaredArray(SiiSyntaxDocument.Parse(input), "vehicle", "v"));
        Assert.Equal(AccessoryReferenceState.Ambiguous, result.State);
        Assert.Null(result.TargetBlockType);
    }

    [Theory]
    [InlineData("accessories: 2\n accessories[0]: a")]
    [InlineData("accessories: 1\n accessories[1]: a")]
    [InlineData("accessories: 2\n accessories[0]: a\n accessories[0]: b")]
    [InlineData("accessories: 1025")]
    [InlineData("accessories: -1")]
    public void InvalidArraysFailWithoutChangingText(string fields)
    {
        string input = "vehicle : v {\n " + fields + "\n}\n";
        var document = SiiSyntaxDocument.Parse(input);
        Assert.Throws<InvalidOperationException>(() => LegacyAccessoryInspector.InspectDeclaredArray(document, "vehicle", "v"));
        Assert.Equal(input, document.ToText());
    }
}
