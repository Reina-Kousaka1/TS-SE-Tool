using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Bounded read-only decoder for plain SII, 3nK-encoded SII and ScsC-wrapped SII payloads.
/// It never opens files, writes data, or authenticates the ScsC HMAC.
/// </summary>
public static class SiiSaveContainerDecoder
{
    public const int MaximumFileBytes = 32 * 1024 * 1024;
    public const int MaximumInflatedBytes = 32 * 1024 * 1024;
    private const int IvOffset = 36;
    private const int InflatedLengthOffset = 52;
    private const int PayloadOffset = 56;
    private const int AesBlockBytes = 16;

    // The shared ETS2/ATS ScsC key published by community format references.
    // This key is not a user credential or a trust secret; the header HMAC is
    // still not checked and the result remains explicitly unauthenticated.
    private static readonly byte[] SharedScsKey =
    [
        0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
        0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
        0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
        0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
    ];

    /// <summary>Decodes exactly one in-memory SII file or save container.</summary>
    public static SiiDecodedSave Decode(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaximumFileBytes)
            throw new SiiSaveContainerException(SiiSaveContainerError.LimitExceeded);

        bool wrapped = input.StartsWith("ScsC"u8);
        byte[] payload = wrapped ? DecryptAndInflate(input) : input.ToArray();
        bool threeNk = false;
        try
        {
            if (payload.AsSpan().StartsWith("3nK\u0001"u8))
            {
                byte[] decoded = DecodeThreeNk(payload);
                if (wrapped) CryptographicOperations.ZeroMemory(payload);
                payload = decoded;
                threeNk = true;
            }

            if (payload.AsSpan().StartsWith("BSII"u8))
            {
                IReadOnlyList<ExperimentalBinaryBlock> blocks = ExperimentalBinarySiiReader.Read(payload);
                return new SiiDecodedSave(payload, SiiPayloadFormat.Binary, wrapped, threeNk, null, blocks);
            }

            if (HasTextSiiHeader(payload))
            {
                SiiEncodedText text = SiiEncodedText.Parse(payload);
                return new SiiDecodedSave(payload, SiiPayloadFormat.Text, wrapped, threeNk, text.Syntax, null);
            }

            throw new SiiSaveContainerException(SiiSaveContainerError.UnsupportedPayload);
        }
        finally
        {
            if (wrapped) CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static byte[] DecodeThreeNk(ReadOnlySpan<byte> input)
    {
        // Format notes describe a six-byte header: 3nK\x01, one unknown byte,
        // then the byte-table seed. The payload is XORed with a 256-byte key
        // rotated by that seed. Keep this bounded to the input length and only
        // accept the decoded SII forms handled below.
        if (input.Length <= 6)
            throw new SiiSaveContainerException(SiiSaveContainerError.Truncated);

        byte[] result = input[6..].ToArray();
        int seed = input[5];
        for (int offset = 0; offset < result.Length; offset++)
        {
            int index = (seed + offset) & 0xff;
            byte key = (byte)((((index << 2) ^ ~index) << 3) ^ index);
            result[offset] ^= key;
        }

        return result;
    }

    private static byte[] DecryptAndInflate(ReadOnlySpan<byte> file)
    {
        if (file.Length < PayloadOffset + AesBlockBytes)
            throw new SiiSaveContainerException(SiiSaveContainerError.Truncated);

        uint announcedLength = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(InflatedLengthOffset, sizeof(uint)));
        if (announcedLength is 0 or > MaximumInflatedBytes)
            throw new SiiSaveContainerException(SiiSaveContainerError.LimitExceeded);

        ReadOnlySpan<byte> ciphertext = file[PayloadOffset..];
        if (ciphertext.IsEmpty || ciphertext.Length % AesBlockBytes != 0)
            throw new SiiSaveContainerException(SiiSaveContainerError.InvalidCiphertextLength);

        byte[] decrypted;
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = SharedScsKey;
            aes.IV = file.Slice(IvOffset, AesBlockBytes).ToArray();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            decrypted = decryptor.TransformFinalBlock(ciphertext.ToArray(), 0, ciphertext.Length);
        }
        catch (CryptographicException exception)
        {
            throw new SiiSaveContainerException(SiiSaveContainerError.DecryptionFailed, exception);
        }

        try
        {
            using var compressed = new MemoryStream(decrypted, writable: false);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            using var output = new MemoryStream(checked((int)announcedLength));
            byte[] buffer = new byte[64 * 1024];
            while (true)
            {
                int count = zlib.Read(buffer, 0, buffer.Length);
                if (count == 0) break;
                if (output.Length + count > announcedLength)
                    throw new SiiSaveContainerException(SiiSaveContainerError.LengthMismatch);
                output.Write(buffer, 0, count);
            }

            if (output.Length != announcedLength)
                throw new SiiSaveContainerException(SiiSaveContainerError.LengthMismatch);
            return output.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw new SiiSaveContainerException(SiiSaveContainerError.DecompressionFailed, exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decrypted);
        }
    }

    private static bool HasTextSiiHeader(ReadOnlySpan<byte> payload)
    {
        if (payload.StartsWith("SiiN"u8)) return true;
        if (payload.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return payload[3..].StartsWith("SiiN"u8);
        if (payload.StartsWith(new byte[] { 0xff, 0xfe }))
            return payload.Length >= 10 && payload.Slice(2, 8).SequenceEqual(new byte[] { 0x53, 0x00, 0x69, 0x00, 0x69, 0x00, 0x4e, 0x00 });
        if (payload.StartsWith(new byte[] { 0xfe, 0xff }))
            return payload.Length >= 10 && payload.Slice(2, 8).SequenceEqual(new byte[] { 0x00, 0x53, 0x00, 0x69, 0x00, 0x69, 0x00, 0x4e });
        return false;
    }
}

public sealed class SiiDecodedSave
{
    private readonly byte[] _payload;

    internal SiiDecodedSave(
        byte[] payload,
        SiiPayloadFormat format,
        bool containerAuthenticationUnverified,
        bool threeNkDecoded,
        SiiSyntaxDocument? textDocument,
        IReadOnlyList<ExperimentalBinaryBlock>? binaryBlocks)
    {
        _payload = (byte[])payload.Clone();
        Format = format;
        ContainerAuthenticationUnverified = containerAuthenticationUnverified;
        ThreeNkDecoded = threeNkDecoded;
        TextDocument = textDocument;
        BinaryBlocks = binaryBlocks;
    }

    public SiiPayloadFormat Format { get; }
    /// <summary>True for ScsC; its HMAC is not verified by this read-only decoder.</summary>
    public bool ContainerAuthenticationUnverified { get; }
    /// <summary>True when the six-byte 3nK wrapper was removed before SII parsing.</summary>
    public bool ThreeNkDecoded { get; }
    public ReadOnlyMemory<byte> Payload => _payload;
    public SiiSyntaxDocument? TextDocument { get; }
    public IReadOnlyList<ExperimentalBinaryBlock>? BinaryBlocks { get; }
}

public enum SiiPayloadFormat { Text, Binary }

public enum SiiSaveContainerError
{
    Truncated,
    LimitExceeded,
    InvalidCiphertextLength,
    DecryptionFailed,
    DecompressionFailed,
    LengthMismatch,
    UnsupportedPayload
}

public sealed class SiiSaveContainerException : FormatException
{
    public SiiSaveContainerException(SiiSaveContainerError error, Exception? innerException = null)
        : base($"SII save container decoding failed ({error}).", innerException) => Error = error;

    public SiiSaveContainerError Error { get; }
}
