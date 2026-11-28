using System.Text;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiEncodedTextTests
{
    private const string Input = "SiiNunit\r\n{\r\n economy : synthetic {\r\n  value: 1\r\n  mod: \"未知 🚛\"\r\n }\r\n}\r\n";

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    public void UnchangedRoundTripPreservesBytesAndEditedTextKeepsEncoding(string kind)
    {
        Encoding encoding = kind switch
        {
            "utf8" => new UTF8Encoding(false, true),
            "utf8-bom" => new UTF8Encoding(true, true),
            "utf16-le" => new UnicodeEncoding(false, true, true),
            "utf16-be" => new UnicodeEncoding(true, true, true),
            _ => throw new ArgumentException("Unknown test encoding", nameof(kind))
        };
        byte[] original = encoding.GetPreamble().Concat(encoding.GetBytes(Input)).ToArray();
        SiiEncodedText document = SiiEncodedText.Parse(original);
        Assert.Equal(original, document.ToBytes());
        SiiEncodedText changed = document.WithFieldValue("economy", "synthetic", "value", "2");
        byte[] expected = encoding.GetPreamble().Concat(encoding.GetBytes(Input.Replace("value: 1", "value: 2", StringComparison.Ordinal))).ToArray();
        Assert.Equal(expected, changed.ToBytes());
        Assert.Equal(original, document.ToBytes());
    }

    [Fact]
    public void InvalidEncodingsAndContainersAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => SiiEncodedText.Parse(new byte[] { 0xc3, 0x28 }));
        Assert.Throws<InvalidDataException>(() => SiiEncodedText.Parse(new byte[] { 0xff, 0xfe, 0x41 }));
        Assert.Throws<InvalidDataException>(() => SiiEncodedText.Parse(new byte[] { 0xff, 0xfe, 0x00, 0x00 }));
        Assert.Throws<InvalidDataException>(() => SiiEncodedText.Parse(Encoding.UTF8.GetBytes("root {\n value: a\0b\n}")));
        Assert.Throws<InvalidDataException>(() => SiiEncodedText.Parse(new byte[SiiEncodedText.MaximumBytes + 1]));
    }

    [Fact]
    public void InvalidReplacementCannotAlterOriginalDocument()
    {
        byte[] original = Encoding.UTF8.GetBytes(Input);
        SiiEncodedText document = SiiEncodedText.Parse(original);
        Assert.Throws<InvalidDataException>(() => document.WithFieldValue("economy", "synthetic", "value", "\ud800"));
        Assert.Equal(original, document.ToBytes());
    }

    [Fact]
    public void ParsesAndRoundTripsTextLargerThanNineMiB()
    {
        var builder = new StringBuilder("SiiNunit\n{\n test : synthetic {\n");
        string comment = "#" + new string('x', 1022) + "\n";
        for (int index = 0; index < 9_220; index++) builder.Append(comment);
        builder.Append(" value: 1\n }\n}\n");
        byte[] source = Encoding.UTF8.GetBytes(builder.ToString());

        Assert.True(source.Length > 9 * 1024 * 1024);
        SiiEncodedText parsed = SiiEncodedText.Parse(source);

        Assert.Equal(source, parsed.ToBytes());
        SiiBlock block = Assert.Single(parsed.Syntax.Blocks, item => item.Type == "test");
        Assert.Equal("1", Assert.Single(block.Fields).Value);
    }
}
