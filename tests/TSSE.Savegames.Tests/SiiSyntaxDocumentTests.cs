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
    public void Parser_RejectsUnbalancedBracesBeforeAnyWriteLayerCanUseIt()
    {
        Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("root {\n child: 1\n"));
        Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("}\n"));
    }

    [Fact]
    public void WithFieldValue_RejectsAmbiguousAndStructuralEdits()
    {
        const string input = "root {\n item : a {\n value: 1\n value: 2\n }\n}";
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Throws<InvalidOperationException>(() => document.WithFieldValue("item", "a", "value", "3"));
        Assert.Throws<ArgumentException>(() => document.WithFieldValue("item", "a", "missing", "4\n forged: true"));
    }
}
