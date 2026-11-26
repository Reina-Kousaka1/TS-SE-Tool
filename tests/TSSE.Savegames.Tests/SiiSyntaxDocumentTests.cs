using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiSyntaxDocumentTests
{
    [Fact]
    public void Fixture_RoundtripsExactlyAndKeepsUnknownValues()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.sii");
        string text = File.ReadAllText(path);

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(text);

        Assert.Equal(text, document.ToText());
        Assert.Contains(document.Blocks, block => block.Type == "mod_extension" && block.Id == "_nameless.mod");
        SiiBlock economy = Assert.Single(document.Blocks, block => block.Type == "economy");
        Assert.Contains(economy.Fields, field => field.Name == "optional_value" && field.Value == "nil");
        Assert.Contains(economy.Fields, field => field.Name == "counter[0]" && field.Value == "3130117026");
        Assert.Contains(economy.Fields, field => field.Name == "signed_boundary" && field.Value == "-9223372036854775808");
        Assert.Contains(economy.Fields, field => field.Name == "unsigned_boundary" && field.Value == "18446744073709551615");
        Assert.Contains(economy.Fields, field => field.Name == "mod_payload" && field.Value == "\"retain {quoted} data\"");
        Assert.Contains(economy.Fields, field => field.Name == "extension_ref" && field.Value == "_nameless.mod");

        SiiBlock modExtension = Assert.Single(document.Blocks, block => block.Type == "mod_extension");
        Assert.Contains(modExtension.Fields, field => field.Name == "opaque" && field.Value == "\"unknown block field\"");
    }

    [Fact]
    public void WithFieldValue_ChangesOnlyThatValueAndKeepsLineEndings()
    {
        const string input = "SiiNunit\r\n{\r\n  economy : _nameless.test {\r\n    bank: 100  \r\n    extra: nil\r\n  }\r\n}\r\n";
        const string expected = "SiiNunit\r\n{\r\n  economy : _nameless.test {\r\n    bank: 250  \r\n    extra: nil\r\n  }\r\n}\r\n";

        SiiSyntaxDocument original = SiiSyntaxDocument.Parse(input);
        SiiSyntaxDocument changed = original.WithFieldValue("economy", "_nameless.test", "bank", "250");

        Assert.Equal(input, original.ToText());
        Assert.Equal(expected, changed.ToText());
    }

    [Fact]
    public void Parser_PreservesFinalNewlineChoiceAndNestedBlockOrder()
    {
        const string input = "root {\nchild : a {\n value: 1\n}\nchild : b {\n value: 2\n}\n}";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        Assert.Equal(new[] { "root", "child", "child" }, document.Blocks.Select(block => block.Type));
        Assert.Equal(new string?[] { null, "a", "b" }, document.Blocks.Select(block => block.Id));
    }

    [Fact]
    public void Parser_PreservesSourceOrderWhenNestedBlocksStartOnTheSameLine()
    {
        const string input = "root { child : a { } child : b { } }";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        Assert.Equal(new[] { "root", "child", "child" }, document.Blocks.Select(block => block.Type));
        Assert.Equal(new string?[] { null, "a", "b" }, document.Blocks.Select(block => block.Id));
    }

    [Fact]
    public void Parser_RejectsUnbalancedBracesBeforeAnyWriteLayerCanUseIt()
    {
        Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("root {\n child: 1\n"));
        Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("}\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# synthetic comment only\n")]
    [InlineData("SiiNunit\n")]
    public void Parser_RejectsDocumentsWithoutBlocks(string input)
    {
        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse(input));

        Assert.Equal(1, exception.LineNumber);
    }

    [Fact]
    public void Parser_RejectsTextOverTheConfiguredCharacterLimit()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumCharacters: 8, maximumLineLength: 8);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root {\n}\n", limits));

        Assert.Contains("character limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_RejectsTooManyLinesBeforeBuildingTheDocument()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumLines: 1);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root {\n}\n", limits));

        Assert.Contains("line limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void Parser_RejectsAnOverlongLineBeforeAllocatingItsLineString()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumLineLength: 5);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root {\n}\n", limits));

        Assert.Contains("length limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, exception.LineNumber);
    }

    [Fact]
    public void Parser_RejectsExcessiveBlockNesting()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumNestingDepth: 1);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root {\n child {\n }\n}", limits));

        Assert.Contains("nesting limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void Parser_RejectsTooManyBlocks()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumBlocks: 1);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root { child { } }", limits));

        Assert.Contains("block limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_RejectsTooManyFieldsAcrossTheDocument()
    {
        SiiSyntaxParseLimits limits = CreateLimits(maximumFields: 1);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.Parse("root {\n first: 1\n second: 2\n}", limits));

        Assert.Contains("field limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, exception.LineNumber);
    }

    [Fact]
    public void FieldEditRetainsTheLimitsUsedToParseTheOriginalDocument()
    {
        const string input = "root {\n value: 1\n}";
        SiiSyntaxParseLimits limits = CreateLimits(
            maximumCharacters: input.Length + 2,
            maximumLineLength: input.Length + 2);
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input, limits);

        Assert.Throws<SiiSyntaxException>(
            () => document.WithFieldValue("root", null, "value", new string('9', 16)));
        Assert.Equal(input, document.ToText());
    }

    [Fact]
    public void ParseLimitsMustBePositiveAndInternallyConsistent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiiSyntaxParseLimits(0, 1, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SiiSyntaxParseLimits(10, 1, 1, 1, 1, 11));
    }

    [Fact]
    public void WithFieldValue_RejectsAmbiguousAndStructuralEdits()
    {
        const string input = "root {\n item : a {\n value: 1\n value: 2\n }\n}";
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Throws<InvalidOperationException>(() => document.WithFieldValue("item", "a", "value", "3"));
        Assert.Throws<ArgumentException>(() => document.WithFieldValue("item", "a", "missing", "4\n forged: true"));
    }

    private static SiiSyntaxParseLimits CreateLimits(
        int maximumCharacters = 1_024,
        int maximumLines = 128,
        int maximumBlocks = 128,
        int maximumFields = 128,
        int maximumNestingDepth = 128,
        int maximumLineLength = 512) =>
        new(maximumCharacters, maximumLines, maximumBlocks, maximumFields, maximumNestingDepth, maximumLineLength);
}
