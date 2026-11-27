using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiSyntaxComparisonTests
{
    [Fact]
    public void ReportsExplicitSyntaxChangesWithoutExposingValues()
    {
        const string before = "root {\n economy : e {\n  money: 10\n  old: 1\n  mod: \"秘密\"\n }\n trailer : t {\n }\n}\n";
        const string after = "root {\n economy : e {\n  money: 20\n  new: 2\n  mod: \"秘密\"\n }\n vehicle : v {\n }\n}\n";
        var differences = SiiSyntaxComparison.Compare(SiiSyntaxDocument.Parse(before), SiiSyntaxDocument.Parse(after));
        Assert.Equal(new[] {
            new SiiSyntaxDifference(SiiSyntaxDifferenceKind.FieldChanged, "economy", "e", "money"),
            new SiiSyntaxDifference(SiiSyntaxDifferenceKind.FieldRemoved, "economy", "e", "old"),
            new SiiSyntaxDifference(SiiSyntaxDifferenceKind.FieldAdded, "economy", "e", "new"),
            new SiiSyntaxDifference(SiiSyntaxDifferenceKind.BlockRemoved, "trailer", "t", null),
            new SiiSyntaxDifference(SiiSyntaxDifferenceKind.BlockAdded, "vehicle", "v", null)
        }, differences);
        Assert.DoesNotContain(differences, item => item.ToString().Contains("秘密", StringComparison.Ordinal));
    }

    [Fact]
    public void FindsMovedBlocksAndIgnoresFormattingAndOrder()
    {
        const string before = "root {\n a : x {\n  child : c {\n   value: 1\n  }\n }\n b : y {\n }\n}\n";
        const string after = "root {\n b : y {\n  child : c {\n   value:   1  \n  }\n }\n a : x {\n }\n}\n";
        var difference = Assert.Single(SiiSyntaxComparison.Compare(SiiSyntaxDocument.Parse(before), SiiSyntaxDocument.Parse(after)));
        Assert.Equal(new SiiSyntaxDifference(SiiSyntaxDifferenceKind.BlockMoved, "child", "c", null), difference);
    }

    [Theory]
    [InlineData("root {\n a : x {\n }\n b : x {\n }\n}\n")]
    [InlineData("root {\n a {\n }\n a {\n }\n}\n")]
    [InlineData("root {\n a : x {\n field: 1\n field: 2\n }\n}\n")]
    [InlineData("root {\n unclassified text\n}\n")]
    public void AmbiguousOrUnclassifiedSyntaxIsRejectedOnEitherSide(string input)
    {
        var invalid = SiiSyntaxDocument.Parse(input);
        var valid = SiiSyntaxDocument.Parse("root {\n value: 1\n}\n");
        Assert.Throws<InvalidOperationException>(() => SiiSyntaxComparison.Compare(valid, invalid));
        Assert.Throws<InvalidOperationException>(() => SiiSyntaxComparison.Compare(invalid, valid));
        Assert.Equal(input, invalid.ToText());
    }
}
