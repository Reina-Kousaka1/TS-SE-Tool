using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TSSE.Savegames.Diagnostics;
using Xunit;

namespace TSSE.Savegames.Tests;

public sealed class SiiSaveContainerDecoderTests
{
    private static readonly byte[] ScsKey =
    [
        0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
        0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
        0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
        0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
    ];

    private const string TextSave = "SiiNunit\n{\n economy : synthetic {\n  experience_points: 5\n }\n}\n";

    [Fact]
    public void DecodesPlainTextAndRawBinaryInMemory()
    {
        byte[] text = Encoding.UTF8.GetBytes(TextSave);
        using var decodedText = SiiSaveContainerDecoder.Decode(text);
        Assert.Equal(SiiPayloadFormat.Text, decodedText.Format);
        Assert.False(decodedText.ContainerAuthenticationUnverified);
        Assert.Equal(text, decodedText.Payload.ToArray());
        Assert.NotNull(decodedText.TextDocument);
        Assert.Null(decodedText.BinaryBlocks);

        using var decodedBinary = SiiSaveContainerDecoder.Decode(MinimalBinaryFixture());
        Assert.Equal(SiiPayloadFormat.Binary, decodedBinary.Format);
        Assert.Equal(3u, Assert.Single(decodedBinary.BinaryBlocks!).BinaryVersion);
        Assert.Null(decodedBinary.TextDocument);
    }

    [Fact]
    public void PayloadMemoryCannotMutateTheDecodedSaveSnapshot()
    {
        byte[] input = Encoding.UTF8.GetBytes(TextSave);
        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(input);
        input[0] = (byte)'X';

        ReadOnlyMemory<byte> exposed = decoded.Payload;
        Assert.True(MemoryMarshal.TryGetArray(exposed, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = (byte)'X';

        Assert.Equal(Encoding.UTF8.GetBytes(TextSave), decoded.Payload.ToArray());
        Assert.Equal(TextSave, decoded.TextDocument!.ToText());
    }

    [Fact]
    public void DisposingDecodedPayloadClearsItsSnapshotAndPreventsFurtherAccess()
    {
        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(Encoding.UTF8.GetBytes(TextSave));

        decoded.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = decoded.Payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecodesThreeNkWrappedTextAndReportsTheWrapper(bool alsoScsCWrapped)
    {
        byte[] wrapped = EncodeThreeNk(Encoding.UTF8.GetBytes(TextSave), seed: 251);
        if (alsoScsCWrapped)
            wrapped = EncryptContainer(wrapped);

        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(wrapped);

        Assert.Equal(SiiPayloadFormat.Text, decoded.Format);
        Assert.True(decoded.ThreeNkDecoded);
        Assert.Equal(alsoScsCWrapped, decoded.ContainerAuthenticationUnverified);
        Assert.Equal(TextSave, decoded.TextDocument!.ToText());
        Assert.Equal(Encoding.UTF8.GetBytes(TextSave), decoded.Payload.ToArray());
    }

    [Fact]
    public void DecodesThreeNkFixedFormatVector()
    {
        // Fixed synthetic vector derived from the published 3nK key schedule;
        // this does not use the test encoder below.
        byte[] vector = Convert.FromHexString(
            "336E4B010000ABB8C3CD291B6753BAE2E8EB715E2901072C4333F6C5EDCE4E7D1A3EF0C4B5DFA3FBAA83192D5E62E2D0A7855778393F270874479FFF9EA20A092F71D987");

        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(vector);

        Assert.True(decoded.ThreeNkDecoded);
        Assert.Equal(TextSave, decoded.TextDocument!.ToText());
    }

    [Fact]
    public void DecodesThreeNkWrappedBinaryPayload()
    {
        byte[] expected = MinimalBinaryFixture();

        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(EncodeThreeNk(expected, seed: 17));

        Assert.Equal(SiiPayloadFormat.Binary, decoded.Format);
        Assert.True(decoded.ThreeNkDecoded);
        Assert.False(decoded.ContainerAuthenticationUnverified);
        Assert.Equal(expected, decoded.Payload.ToArray());
        Assert.Equal(3u, Assert.Single(decoded.BinaryBlocks!).BinaryVersion);
    }

    [Fact]
    public void RejectsTruncatedOrUnsupportedThreeNkPayloads()
    {
        Assert.Equal(SiiSaveContainerError.Truncated,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode("3nK\u0001"u8)).Error);
        Assert.Equal(SiiSaveContainerError.UnsupportedPayload,
            Assert.Throws<SiiSaveContainerException>(() =>
                SiiSaveContainerDecoder.Decode(EncodeThreeNk("not an SII payload"u8.ToArray(), seed: 0))).Error);
    }

    [Fact]
    public void DecryptsScsCWithoutClaimingHeaderAuthentication()
    {
        byte[] container = EncryptContainer(Encoding.UTF8.GetBytes(TextSave));
        container[4] ^= 0xff; // HMAC is deliberately not verified by this read-only path.

        using var decoded = SiiSaveContainerDecoder.Decode(container);

        Assert.Equal(SiiPayloadFormat.Text, decoded.Format);
        Assert.True(decoded.ContainerAuthenticationUnverified);
        Assert.Equal(TextSave, decoded.TextDocument!.ToText());
        Assert.Equal(Encoding.UTF8.GetBytes(TextSave), decoded.Payload.ToArray());
    }

    [Fact]
    public void DecryptsScsCWrappedBinaryPayloadThroughTheBoundedBsiiReader()
    {
        byte[] payload = MinimalBinaryFixture();
        using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(EncryptContainer(payload));

        Assert.Equal(SiiPayloadFormat.Binary, decoded.Format);
        Assert.True(decoded.ContainerAuthenticationUnverified);
        Assert.Equal(payload, decoded.Payload.ToArray());
        Assert.Equal(3u, Assert.Single(decoded.BinaryBlocks!).BinaryVersion);
    }

    [Fact]
    public void RejectsScsCWithMissingZlibChecksumEvenWhenAnnouncedPayloadLengthMatches()
    {
        byte[] payload = Encoding.UTF8.GetBytes(TextSave);
        using var compressedStream = new MemoryStream();
        using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(payload);
        byte[] compressedWithoutChecksum = compressedStream.ToArray()[..^4];

        SiiSaveContainerException error = Assert.Throws<SiiSaveContainerException>(() =>
            SiiSaveContainerDecoder.Decode(EncryptCompressedData(compressedWithoutChecksum, (uint)payload.Length)));

        Assert.Equal(SiiSaveContainerError.DecompressionFailed, error.Error);
    }

    [Fact]
    public void RejectsMalformedAndOutOfBoundsContainers()
    {
        Assert.Equal(SiiSaveContainerError.UnsupportedPayload,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode("NOPE"u8)).Error);
        Assert.Equal(SiiSaveContainerError.Truncated,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode("ScsC"u8)).Error);

        byte[] misaligned = new byte[73];
        "ScsC"u8.CopyTo(misaligned);
        BinaryPrimitives.WriteUInt32LittleEndian(misaligned.AsSpan(52, 4), 1);
        Assert.Equal(SiiSaveContainerError.InvalidCiphertextLength,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(misaligned)).Error);

        byte[] excessiveHeader = EncryptContainer(Encoding.UTF8.GetBytes(TextSave));
        BinaryPrimitives.WriteUInt32LittleEndian(excessiveHeader.AsSpan(52, 4), (uint)SiiSaveContainerDecoder.MaximumInflatedBytes + 1);
        Assert.Equal(SiiSaveContainerError.LimitExceeded,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(excessiveHeader)).Error);

        Assert.Equal(SiiSaveContainerError.LengthMismatch,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(
                EncryptContainer(Encoding.UTF8.GetBytes(TextSave), announcedLengthDelta: -1))).Error);
        Assert.Equal(SiiSaveContainerError.LengthMismatch,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(
                EncryptContainer(Encoding.UTF8.GetBytes(TextSave), announcedLengthDelta: 1))).Error);
        Assert.Equal(SiiSaveContainerError.DecompressionFailed,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(
                EncryptCompressedData([0xde, 0xad, 0xbe, 0xef], announcedLength: 64))).Error);

        Assert.Equal(SiiSaveContainerError.LimitExceeded,
            Assert.Throws<SiiSaveContainerException>(() => SiiSaveContainerDecoder.Decode(
                new byte[SiiSaveContainerDecoder.MaximumFileBytes + 1])).Error);
    }

    private static byte[] EncryptContainer(byte[] payload, int announcedLengthDelta = 0)
    {
        using var compressedStream = new MemoryStream();
        using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(payload);
        return EncryptCompressedData(compressedStream.ToArray(), checked((uint)(payload.Length + announcedLengthDelta)));
    }

    private static byte[] EncodeThreeNk(byte[] payload, byte seed)
    {
        byte[] result = new byte[payload.Length + 6];
        "3nK\u0001"u8.CopyTo(result);
        result[4] = 0; // Unknown header byte; retained as an opaque value.
        result[5] = seed;
        for (int offset = 0; offset < payload.Length; offset++)
        {
            int index = (seed + offset) & 0xff;
            byte key = (byte)((((index << 2) ^ ~index) << 3) ^ index);
            result[offset + 6] = (byte)(payload[offset] ^ key);
        }
        return result;
    }

    private static byte[] EncryptCompressedData(byte[] compressed, uint announcedLength)
    {
        int paddedLength = checked((compressed.Length + 15) / 16 * 16);
        Array.Resize(ref compressed, paddedLength); // ScsC keeps AES block padding after the zlib stream.
        byte[] iv = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        byte[] ciphertext;
        using (Aes aes = Aes.Create())
        {
            aes.Key = ScsKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);
        }

        byte[] result = new byte[56 + ciphertext.Length];
        "ScsC"u8.CopyTo(result);
        iv.CopyTo(result, 36);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(52, 4), announcedLength);
        ciphertext.CopyTo(result, 56);
        return result;
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
}
