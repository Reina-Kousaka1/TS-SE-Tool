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
    public void Fixture_FieldEditChangesOnlyTheSelectedValueAndPreservesOpaqueSyntax()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.sii");
        string originalText = File.ReadAllText(path);
        SiiSyntaxDocument original = SiiSyntaxDocument.Parse(originalText);

        SiiSyntaxDocument changed = original.WithFieldValue(
            "economy",
            "_nameless.synthetic",
            "bank",
            "2000000001");

        string expectedText = originalText.Replace(
            "bank: 1234567890",
            "bank: 2000000001",
            StringComparison.Ordinal);
        Assert.Equal(originalText, original.ToText());
        Assert.Equal(expectedText, changed.ToText());
        Assert.Contains(changed.Blocks, block => block.Type == "mod_extension" && block.Id == "_nameless.mod");
        SiiBlock changedEconomy = Assert.Single(
            changed.Blocks,
            block => block.Type == "economy" && block.Id == "_nameless.synthetic");
        Assert.Contains(changedEconomy.Fields, field => field.Name == "counter[0]" && field.Value == "3130117026");
        Assert.Contains(changedEconomy.Fields, field => field.Name == "extension_ref" && field.Value == "_nameless.mod");
        Assert.Contains(changedEconomy.Fields, field => field.Name == "mod_payload" && field.Value == "\"retain {quoted} data\"");
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
        SiiBlock[] children = document.Blocks.Skip(1).ToArray();
        Assert.All(children, block =>
        {
            Assert.Equal(1, block.NestingDepth);
            Assert.Equal(1, block.ParentStartLine);
            Assert.Equal(1, block.ParentStartColumn);
        });
    }

    [Fact]
    public void Parser_ReportsNestedBlockOwnershipAndColumns()
    {
        const string input = "root {\n child : a {\n  nested {\n  }\n }\n}";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        SiiBlock root = Assert.Single(document.Blocks, block => block.Type == "root");
        SiiBlock child = Assert.Single(document.Blocks, block => block.Type == "child");
        SiiBlock nested = Assert.Single(document.Blocks, block => block.Type == "nested");
        Assert.Equal(0, root.NestingDepth);
        Assert.Null(root.ParentStartLine);
        Assert.Equal(1, root.StartColumn);
        Assert.Equal(1, child.NestingDepth);
        Assert.Equal(child.StartLine, nested.ParentStartLine);
        Assert.Equal(child.StartColumn, nested.ParentStartColumn);
        Assert.Equal(2, nested.NestingDepth);
    }

    [Fact]
    public void Parser_RejectsUnbalancedBracesBeforeAnyWriteLayerCanUseIt()
    {
        SiiSyntaxException unclosed = Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("root {\n child: 1\n"));
        SiiSyntaxException unexpectedClose = Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse("}\n"));

        Assert.Equal(SiiSyntaxErrorCode.UnclosedBlock, unclosed.ErrorCode);
        Assert.Equal(SiiSyntaxErrorCode.UnexpectedClosingBrace, unexpectedClose.ErrorCode);
        Assert.Equal(1, unexpectedClose.ColumnNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# synthetic comment only\n")]
    [InlineData("SiiNunit\n")]
    public void Parser_RejectsDocumentsWithoutBlocks(string input)
    {
        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(() => SiiSyntaxDocument.Parse(input));

        Assert.Equal(1, exception.LineNumber);
        Assert.Equal(SiiSyntaxErrorCode.NoBlocks, exception.ErrorCode);
    }

    [Fact]
    public void Parser_PreservesAndSurfacesUnclassifiedLinesAndRejectsEditingThem()
    {
        const string input = "SiiNunit\n{\n root : _nameless.synthetic {\n  future syntax without a delimiter\n  future_value: <opaque payload>\n }\n}\n";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        SiiOpaqueLine opaqueLine = Assert.Single(document.OpaqueLines);
        Assert.Equal(4, opaqueLine.LineNumber);
        Assert.Equal("  future syntax without a delimiter", opaqueLine.RawLine);
        Assert.Equal("root", opaqueLine.ContainingBlockType);
        Assert.Equal("_nameless.synthetic", opaqueLine.ContainingBlockId);
        Assert.Contains(Assert.Single(document.Blocks, block => block.Type == "root").Fields,
            field => field.Name == "future_value" && field.RawValue == " <opaque payload>");
        Assert.Throws<InvalidOperationException>(() => document.WithFieldValue(
            "root", "_nameless.synthetic", "future_value", "<replacement>"));
    }

    [Fact]
    public void Parser_ReportsInlineUnclassifiedSyntaxInsteadOfDroppingItsSemanticsSilently()
    {
        const string input = "root { unknown inline syntax }";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        SiiOpaqueLine opaqueLine = Assert.Single(document.OpaqueLines);
        Assert.Equal(1, opaqueLine.LineNumber);
        Assert.Equal("root", opaqueLine.ContainingBlockType);
        Assert.Equal(input, opaqueLine.RawLine);
    }

    [Fact]
    public void Parser_PreservesDuplicateOpaqueReferencesWithoutClaimingToResolveThem()
    {
        const string input = "future_object : _nameless.synthetic {\n ref: _nameless.external\n ref: _nameless.external\n}\n";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        SiiBlock block = Assert.Single(document.Blocks);
        Assert.Equal("future_object", block.Type);
        Assert.Equal(2, block.Fields.Count(field => field.Name == "ref" && field.Value == "_nameless.external"));
        Assert.Empty(document.OpaqueLines);
    }

    [Fact]
    public void Parser_PreservesAnUnclassifiedTopLevelTrailer()
    {
        const string input = "root {\n value: 1\n}\nfuture trailer syntax\n";

        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(input);

        Assert.Equal(input, document.ToText());
        SiiOpaqueLine line = Assert.Single(document.OpaqueLines);
        Assert.Equal(4, line.LineNumber);
        Assert.Null(line.ContainingBlockType);
        Assert.Equal("future trailer syntax", line.RawLine);
    }

    [Fact]
    public void ReaderParserRoundtripsLargeValidInputWithinRaisedExplicitLimits()
    {
        string largeValue = new('x', 1_100_000);
        string input = $"root {{\n payload: \"{largeValue}\"\n}}";
        SiiSyntaxParseLimits limits = CreateLimits(
            maximumCharacters: 1_200_000,
            maximumLines: 4,
            maximumLineLength: 1_150_000);

        SiiSyntaxDocument document = SiiSyntaxDocument.ParseReader(new StringReader(input), limits);

        Assert.Equal(input, document.ToText());
        Assert.Single(document.Blocks);
        Assert.Empty(document.OpaqueLines);
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
    public void ReaderParserStopsAfterOneCharacterBeyondTheConfiguredLimit()
    {
        var reader = new CountingTextReader("root {\n}\nextra input that must not be consumed");
        SiiSyntaxParseLimits limits = CreateLimits(maximumCharacters: 8, maximumLineLength: 8);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.ParseReader(reader, limits));

        Assert.Contains("character limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(limits.MaximumCharacters + 1, reader.CharactersRead);
        Assert.False(reader.IsDisposed);
    }

    [Fact]
    public void ReaderParserStopsWhenALineExceedsItsConfiguredLimit()
    {
        string input = "root {" + "x".PadLeft(10_000, 'x');
        var reader = new CountingTextReader(input);
        SiiSyntaxParseLimits limits = CreateLimits(maximumCharacters: 20_000, maximumLineLength: 12);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.ParseReader(reader, limits));

        Assert.Contains("length limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, exception.LineNumber);
        Assert.True(reader.CharactersRead < input.Length);
        Assert.False(reader.IsDisposed);
    }

    [Fact]
    public void ReaderParserStopsWhenTheConfiguredLineLimitIsExceeded()
    {
        string input = "root {\n" + new string('x', 100_000);
        var reader = new CountingTextReader(input);
        SiiSyntaxParseLimits limits = CreateLimits(
            maximumCharacters: input.Length,
            maximumLines: 1,
            maximumLineLength: input.Length);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.ParseReader(reader, limits));

        Assert.Contains("line limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, exception.LineNumber);
        Assert.True(reader.CharactersRead < input.Length);
        Assert.False(reader.IsDisposed);
    }

    [Fact]
    public void ReaderParserAllowsATrailingTerminatorAtTheExactLineLimit()
    {
        const string input = "root {\n}\n";
        SiiSyntaxParseLimits limits = CreateLimits(maximumLines: 2);

        SiiSyntaxDocument document = SiiSyntaxDocument.ParseReader(new StringReader(input), limits);

        Assert.Equal(input, document.ToText());
    }

    [Fact]
    public void ReaderParserTracksLineNumbersAcrossCrLfTerminators()
    {
        var reader = new CountingTextReader("root {\r\n value: 123456789\r\n}");
        SiiSyntaxParseLimits limits = CreateLimits(maximumCharacters: 100, maximumLineLength: 10);

        SiiSyntaxException exception = Assert.Throws<SiiSyntaxException>(
            () => SiiSyntaxDocument.ParseReader(reader, limits));

        Assert.Contains("length limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void ReaderParserRoundtripsInputAndLeavesReaderOpen()
    {
        const string input = "root {\n value: nil\n}\n";
        var reader = new CountingTextReader(input);

        SiiSyntaxDocument document = SiiSyntaxDocument.ParseReader(reader);

        Assert.Equal(input, document.ToText());
        Assert.Equal(input.Length, reader.CharactersRead);
        Assert.False(reader.IsDisposed);
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

    private sealed class CountingTextReader(string content) : TextReader
    {
        private readonly StringReader _reader = new(content);

        public int CharactersRead { get; private set; }
        public bool IsDisposed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            int read = _reader.Read(buffer, index, count);
            CharactersRead += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing)
                _reader.Dispose();
            base.Dispose(disposing);
        }
    }
}
