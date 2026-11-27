using System.Security.Cryptography;
using System.Text;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiFixtureInspectorTests
{
    [Theory]
    [InlineData(false, SiiTextEncoding.Utf8)]
    [InlineData(true, SiiTextEncoding.Utf8Bom)]
    public void SyntheticTextReportsSyntaxOnlyAndExactBytes(bool bom, SiiTextEncoding expectedEncoding)
    {
        var encoding = new UTF8Encoding(bom, true);
        byte[] input = encoding.GetPreamble().Concat(encoding.GetBytes("root {\r\n value: \"未知\"\r\n}\r\n")).ToArray();
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(SiiFixtureStatus.ParsedSyntaxOnly, report.Status);
        Assert.Equal(expectedEncoding, report.Encoding);
        Assert.True(report.ByteRoundTripEqual);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(input)), report.Sha256);
        Assert.Equal(1, report.BlockCount);
        Assert.Equal(0, report.OpaqueLineCount);
    }

    [Fact]
    public void OpaqueSyntaxIsExplicitlyDistinguishedFromCleanSyntax()
    {
        byte[] input = Encoding.UTF8.GetBytes("root {\n value: 1\n unknown syntax\n}\n");
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(SiiFixtureStatus.UnclassifiedSyntax, report.Status);
        Assert.True(report.ByteRoundTripEqual);
        Assert.Equal(1, report.OpaqueLineCount);
    }

    [Fact]
    public void InvalidSyntaxAndUnsupportedBytesAreNotReportedAsParsed()
    {
        SiiFixtureReport unclosed = SiiFixtureInspector.Inspect(Encoding.UTF8.GetBytes("root {\n value: 1\n"));
        Assert.Equal(SiiFixtureStatus.InvalidSyntax, unclosed.Status);
        Assert.Equal(SiiSyntaxErrorCode.UnclosedBlock, unclosed.SyntaxError);
        Assert.Null(unclosed.ByteRoundTripEqual);
        SiiFixtureReport binary = SiiFixtureInspector.Inspect(new byte[] { 0xc3, 0x28 });
        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, binary.Status);
        Assert.Null(binary.BlockCount);
        Assert.Null(binary.ByteRoundTripEqual);
    }

    [Fact]
    public void OverLimitInputIsRejectedBeforeHashingOrParsing()
    {
        var input = new byte[SiiEncodedText.MaximumBytes + 1];
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(SiiFixtureStatus.TooLarge, report.Status);
        Assert.Null(report.Sha256);
        Assert.Null(report.Encoding);
    }
}
