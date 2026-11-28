using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiFixtureInspectorTests
{
    [Theory]
    [InlineData(1u, BinarySiiHeaderHint.LegacyDocumentedVersion)]
    [InlineData(2u, BinarySiiHeaderHint.LegacyDocumentedVersion)]
    [InlineData(3u, BinarySiiHeaderHint.LegacyDocumentedVersion)]
    [InlineData(0u, BinarySiiHeaderHint.UnrecognizedVersion)]
    [InlineData(uint.MaxValue, BinarySiiHeaderHint.UnrecognizedVersion)]
    public void IncompleteBinaryHeaderDoesNotPassFullPayloadValidation(uint version, BinarySiiHeaderHint expected)
    {
        byte[] input = new byte[8];
        "BSII"u8.CopyTo(input);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(4), version);
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, report.Status);
        Assert.Equal(expected, report.BinaryHeader);
        Assert.Equal(version, report.BinaryFormatVersion);
        Assert.Null(report.ByteRoundTripEqual);
        using var stream = new ShortReadStream(input, 2);
        Assert.Equal(report, SiiFixtureInspector.Inspect(stream));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void IncompleteBinaryHeaderDoesNotInventAVersion(int length)
    {
        byte[] input = new byte[length];
        "BSII"u8.CopyTo(input);
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(BinarySiiHeaderHint.Incomplete, report.BinaryHeader);
        Assert.Null(report.BinaryFormatVersion);
        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, report.Status);
    }

    [Fact]
    public void DuplicateHintsCountRepeatedIdsAndFieldsWithoutTreatingIndexedFieldsAsDuplicates()
    {
        byte[] input = Encoding.UTF8.GetBytes("root {\n item : a {\n  value: 1\n  value: 2\n  array[0]: a\n  array[1]: b\n }\n other : a {\n  value: 3\n }\n}\n");
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(SiiFixtureStatus.ParsedSyntaxOnly, report.Status);
        Assert.Equal(1, report.DuplicateBlockIdOccurrences);
        Assert.Equal(1, report.DuplicateFieldOccurrences);
        Assert.True(report.DuplicateInspectionComplete);
        Assert.True(report.ByteRoundTripEqual);
        using var stream = new MemoryStream(input);
        Assert.Equal(report, SiiFixtureInspector.Inspect(stream));
        SiiFixtureReport invalid = SiiFixtureInspector.Inspect("}"u8);
        Assert.Null(invalid.DuplicateBlockIdOccurrences);
        Assert.Null(invalid.DuplicateFieldOccurrences);
        Assert.Null(invalid.DuplicateInspectionComplete);
    }

    [Theory]
    [InlineData("ScsC", SiiSignatureHint.EncryptedPrefix)]
    [InlineData("BSII", SiiSignatureHint.BinaryPrefix)]
    [InlineData("3nK\u0001", SiiSignatureHint.ThreeNkPrefix)]
    public void MalformedKnownPrefixesAreNotReportedAsSuccessfullyDecoded(string prefix, SiiSignatureHint hint)
    {
        byte[] input = Encoding.UTF8.GetBytes(prefix + "\nroot {\n value: 1\n}\n");
        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);
        Assert.Equal(hint, report.SignatureHint);
        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, report.Status);
        Assert.Null(report.ByteRoundTripEqual);
        using var stream = new ShortReadStream(input, 2);
        Assert.Equal(report, SiiFixtureInspector.Inspect(stream));
    }

    [Fact]
    public void ValidScsCBinaryCopyReportsBoundedStructureWithoutValuesOrWriteClaims()
    {
        byte[] input = EncryptContainer(MinimalBinaryFixture());

        SiiFixtureReport report = SiiFixtureInspector.Inspect(input);

        Assert.Equal(SiiFixtureStatus.DecodedBinaryStructureOnly, report.Status);
        Assert.Equal(SiiSignatureHint.EncryptedPrefix, report.SignatureHint);
        Assert.Equal(SiiPayloadFormat.Binary, report.PayloadFormat);
        Assert.True(report.ContainerAuthenticationUnverified);
        Assert.False(report.ThreeNkDecoded);
        Assert.Equal(3u, report.BinaryFormatVersion);
        Assert.Equal(1, report.BlockCount);
        Assert.Null(report.OpaqueLineCount);
        Assert.Null(report.ByteRoundTripEqual);
    }

    [Fact]
    public void InvalidScsCIsReportedWithoutEscapingFormatException()
    {
        SiiFixtureReport report = SiiFixtureInspector.Inspect("ScsC"u8);

        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, report.Status);
        Assert.Equal(SiiSaveContainerError.Truncated, report.ContainerError);
        Assert.Null(report.BlockCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Scs")]
    [InlineData("BSI")]
    [InlineData("3nK")]
    [InlineData("scsc")]
    public void IncompleteOrDifferentPrefixesAreNotClassified(string input)
    {
        Assert.Equal(SiiSignatureHint.Unknown, SiiFixtureInspector.Inspect(Encoding.UTF8.GetBytes(input)).SignatureHint);
    }

    [Fact]
    public void TextPrefixIsOnlyAHintAndDoesNotProveValidSyntax()
    {
        SiiFixtureReport report = SiiFixtureInspector.Inspect("SiiN"u8);
        Assert.Equal(SiiSignatureHint.TextPrefix, report.SignatureHint);
        Assert.Equal(SiiFixtureStatus.InvalidSyntax, report.Status);
    }
    [Fact]
    public void ShortReadsProduceTheSameReportAndLeaveTheStreamOpen()
    {
        byte[] input = Encoding.UTF8.GetBytes("root {\n label: \"Grüße 🚚\"\n}\n");
        using var stream = new ShortReadStream(input, 3);
        SiiFixtureReport fromStream = SiiFixtureInspector.Inspect(stream);
        SiiFixtureReport fromBytes = SiiFixtureInspector.Inspect(input);
        Assert.Equal(fromBytes, fromStream);
        Assert.False(fromStream.ByteLengthIsLowerBound);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void OversizedStreamStopsAtLimitPlusOneAndReportsOnlyALowerBound()
    {
        using var stream = new ShortReadStream(new byte[SiiEncodedText.MaximumBytes + 1000], 4096);
        SiiFixtureReport report = SiiFixtureInspector.Inspect(stream);
        Assert.Equal(SiiFixtureStatus.TooLarge, report.Status);
        Assert.Equal(SiiEncodedText.MaximumBytes + 1, stream.Position);
        Assert.Equal(SiiEncodedText.MaximumBytes + 1, report.ByteLength);
        Assert.True(report.ByteLengthIsLowerBound);
        Assert.Null(report.Sha256);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void ExactLimitIsNotMisreportedAsOversized()
    {
        using var stream = new ShortReadStream(new byte[SiiEncodedText.MaximumBytes], 4096);
        SiiFixtureReport report = SiiFixtureInspector.Inspect(stream);
        Assert.Equal(SiiFixtureStatus.UnsupportedTextOrContainer, report.Status);
        Assert.False(report.ByteLengthIsLowerBound);
        Assert.Equal(SiiEncodedText.MaximumBytes, stream.Position);
    }

    [Fact]
    public void StreamReadFailureDoesNotBecomeASuccessfulReport()
    {
        using var stream = new FailingStream();
        Assert.Throws<IOException>(() => SiiFixtureInspector.Inspect(stream));
    }

    private sealed class ShortReadStream(byte[] bytes, int maxRead) : MemoryStream(bytes, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, maxRead));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Synthetic read failure");
    }

    private static byte[] EncryptContainer(byte[] payload)
    {
        byte[] key = [
            0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
            0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
            0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
            0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(payload);
        byte[] compressedBytes = compressed.ToArray();
        Array.Resize(ref compressedBytes, (compressedBytes.Length + 15) / 16 * 16);
        byte[] iv = Enumerable.Range(0, 16).Select(value => (byte)(value + 17)).ToArray();
        byte[] ciphertext;
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(compressedBytes, 0, compressedBytes.Length);
        }
        byte[] container = new byte[56 + ciphertext.Length];
        "ScsC"u8.CopyTo(container);
        iv.CopyTo(container, 36);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(container.AsSpan(52), (uint)payload.Length);
        ciphertext.CopyTo(container, 56);
        return container;
    }

    private static byte[] MinimalBinaryFixture()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("BSII"u8);
        writer.Write(3u);
        writer.Write(0u);
        writer.Write((byte)1);
        writer.Write(1u);
        byte[] name = Encoding.ASCII.GetBytes("test");
        writer.Write((uint)name.Length);
        writer.Write(name);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write((byte)0xff);
        writer.Write(0x42ul);
        writer.Write(0u);
        writer.Write((byte)0);
        return stream.ToArray();
    }
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
        Assert.False(report.DuplicateInspectionComplete);
    }

    [Fact]
    public void SyntaxDiagnosticPreservesParserColumnWhenAvailable()
    {
        SiiFixtureReport report = SiiFixtureInspector.Inspect("}"u8);
        Assert.Equal(SiiFixtureStatus.InvalidSyntax, report.Status);
        Assert.Equal(1, report.ErrorLine);
        Assert.Equal(1, report.ErrorColumn);
        using var stream = new MemoryStream("}"u8.ToArray());
        Assert.Equal(report, SiiFixtureInspector.Inspect(stream));
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
