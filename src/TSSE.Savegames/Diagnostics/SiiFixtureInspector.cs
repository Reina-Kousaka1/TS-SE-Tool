using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Inspects caller-supplied SII bytes in memory. It performs no file access,
/// emits no field values, and makes no claim about game compatibility.
/// </summary>
public static class SiiFixtureInspector
{
    /// <summary>
    /// Inspects an explicitly supplied stream, reading at most the byte limit
    /// plus one detection byte. The caller retains ownership of the stream.
    /// No file discovery, writes, or game validation is performed. Known binary
    /// wrappers are passed through the bounded read-only container decoder.
    /// </summary>
    public static SiiFixtureReport Inspect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The stream must be readable.", nameof(stream));

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (buffer.Length <= SiiEncodedText.MaximumBytes)
        {
            int remaining = SiiEncodedText.MaximumBytes + 1 - (int)buffer.Length;
            int read = stream.Read(chunk, 0, Math.Min(chunk.Length, remaining));
            if (read == 0)
                return Inspect(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            buffer.Write(chunk, 0, read);
        }

        return new SiiFixtureReport(SiiFixtureStatus.TooLarge, (int)buffer.Length, null, null,
            null, null, null, null, null)
        {
            ByteLengthIsLowerBound = true,
            SignatureHint = DetectSignature(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
        };
    }

    public static SiiFixtureReport Inspect(ReadOnlySpan<byte> bytes)
    {
        SiiSignatureHint hint = DetectSignature(bytes);
        if (bytes.Length > SiiEncodedText.MaximumBytes)
            return new(SiiFixtureStatus.TooLarge, bytes.Length, null, null, null, null, null, null, null) { SignatureHint = hint };
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (hint is SiiSignatureHint.EncryptedPrefix or SiiSignatureHint.BinaryPrefix or SiiSignatureHint.ThreeNkPrefix)
        {
            uint? headerVersion = hint == SiiSignatureHint.BinaryPrefix && bytes.Length >= 8
                ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]) : null;
            BinarySiiHeaderHint headerHint = hint != SiiSignatureHint.BinaryPrefix ? BinarySiiHeaderHint.NotInspected :
                headerVersion is null ? BinarySiiHeaderHint.Incomplete :
                headerVersion is 1 or 2 or 3 ? BinarySiiHeaderHint.LegacyDocumentedVersion : BinarySiiHeaderHint.UnrecognizedVersion;
            try
            {
                using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(bytes);
                if (decoded.Format == SiiPayloadFormat.Binary)
                {
                    IReadOnlyList<ExperimentalBinaryBlock> blocks = decoded.BinaryBlocks!;
                    uint? version = blocks.Count == 0 ? null : blocks[0].BinaryVersion;
                    bool? textSyntaxValidated = null;
                    string? textConversionError = null;
                    ReadOnlyMemory<byte> payloadSnapshot = decoded.Payload;
                    try
                    {
                        byte[] converted = ExperimentalBinarySiiTextSerializer.Serialize(payloadSnapshot.Span);
                        CryptographicOperations.ZeroMemory(converted);
                        textSyntaxValidated = true;
                    }
                    catch (ExperimentalBinaryTextSerializationException exception)
                    {
                        textSyntaxValidated = false;
                        textConversionError = exception.Error;
                    }
                    finally
                    {
                        if (MemoryMarshal.TryGetArray(payloadSnapshot, out ArraySegment<byte> snapshot) && snapshot.Array is not null)
                            CryptographicOperations.ZeroMemory(snapshot.Array.AsSpan(snapshot.Offset, snapshot.Count));
                    }
                    return new(SiiFixtureStatus.DecodedBinaryStructureOnly, bytes.Length, hash, null, null,
                        blocks.Count, null, null, null)
                    {
                        SignatureHint = hint,
                        BinaryHeader = headerHint,
                        BinaryFormatVersion = version,
                        PayloadFormat = SiiPayloadFormat.Binary,
                        ContainerAuthenticationUnverified = decoded.ContainerAuthenticationUnverified,
                        ThreeNkDecoded = decoded.ThreeNkDecoded,
                        BinaryTextSyntaxValidated = textSyntaxValidated,
                        BinaryTextConversionError = textConversionError
                    };
                }

                SiiSyntaxDocument syntax = decoded.TextDocument!;
                return new(SiiFixtureStatus.DecodedContainerTextOnly, bytes.Length, hash, null, null,
                    syntax.Blocks.Count, syntax.OpaqueLines.Count, null, null)
                {
                    SignatureHint = hint,
                    BinaryHeader = headerHint,
                    PayloadFormat = SiiPayloadFormat.Text,
                    ContainerAuthenticationUnverified = decoded.ContainerAuthenticationUnverified,
                    ThreeNkDecoded = decoded.ThreeNkDecoded
                };
            }
            catch (SiiSaveContainerException exception)
            {
                return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                    null, null, null, null)
                {
                    SignatureHint = hint,
                    BinaryHeader = headerHint,
                    BinaryFormatVersion = headerVersion,
                    ContainerError = exception.Error,
                    ContainerAuthenticationUnverified = hint == SiiSignatureHint.EncryptedPrefix
                };
            }
            catch (ExperimentalBinarySiiException exception)
            {
                return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                    null, null, null, null)
                {
                    SignatureHint = hint,
                    BinaryHeader = headerHint,
                    BinaryFormatVersion = headerVersion,
                    BinaryError = exception.Error,
                    ContainerAuthenticationUnverified = hint == SiiSignatureHint.EncryptedPrefix
                };
            }
            catch (SiiSyntaxException exception)
            {
                return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                    null, null, exception.ErrorCode, exception.LineNumber)
                {
                    SignatureHint = hint,
                    BinaryHeader = headerHint,
                    BinaryFormatVersion = headerVersion,
                    ErrorColumn = exception.ColumnNumber,
                    ContainerAuthenticationUnverified = hint == SiiSignatureHint.EncryptedPrefix
                };
            }
            catch (InvalidDataException)
            {
                return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                    null, null, null, null)
                {
                    SignatureHint = hint,
                    BinaryHeader = headerHint,
                    BinaryFormatVersion = headerVersion,
                    ContainerAuthenticationUnverified = hint == SiiSignatureHint.EncryptedPrefix
                };
            }
        }
        try
        {
            SiiEncodedText text = SiiEncodedText.Parse(bytes);
            bool equal = bytes.SequenceEqual(text.ToBytes());
            int opaqueCount = text.Syntax.OpaqueLines.Count;
            SiiFixtureStatus status = !equal ? SiiFixtureStatus.ByteMismatch :
                opaqueCount > 0 ? SiiFixtureStatus.UnclassifiedSyntax : SiiFixtureStatus.ParsedSyntaxOnly;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int duplicateIds = 0, duplicateFields = 0;
            foreach (SiiBlock block in text.Syntax.Blocks)
            {
                if (block.Id is not null && !ids.Add(block.Id)) duplicateIds++;
                var fields = new HashSet<string>(StringComparer.Ordinal);
                foreach (SiiField field in block.Fields)
                    if (!fields.Add(field.Name)) duplicateFields++;
            }
            return new(status, bytes.Length, hash, text.EncodingKind, equal,
                text.Syntax.Blocks.Count, opaqueCount, null, null)
                { SignatureHint = hint, DuplicateBlockIdOccurrences = duplicateIds, DuplicateFieldOccurrences = duplicateFields };
        }
        catch (SiiSyntaxException ex)
        {
            return new(SiiFixtureStatus.InvalidSyntax, bytes.Length, hash, null, null,
                null, null, ex.ErrorCode, ex.LineNumber) { SignatureHint = hint, ErrorColumn = ex.ColumnNumber };
        }
        catch (InvalidDataException)
        {
            return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                null, null, null, null) { SignatureHint = hint };
        }
    }

    private static SiiSignatureHint DetectSignature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("ScsC"u8)) return SiiSignatureHint.EncryptedPrefix;
        if (bytes.StartsWith("BSII"u8)) return SiiSignatureHint.BinaryPrefix;
        if (bytes.StartsWith("3nK\u0001"u8)) return SiiSignatureHint.ThreeNkPrefix;
        if (bytes.StartsWith("SiiN"u8)) return SiiSignatureHint.TextPrefix;
        return SiiSignatureHint.Unknown;
    }
}

/// <summary>A four-byte hint only, never proof by itself that a container is valid or decodable.</summary>
public enum SiiSignatureHint { Unknown, TextPrefix, EncryptedPrefix, BinaryPrefix, ThreeNkPrefix }

/// <summary>Historical BSII version classification; the hint alone never proves decoding or compatibility.</summary>
public enum BinarySiiHeaderHint { NotInspected, Incomplete, LegacyDocumentedVersion, UnrecognizedVersion }

public enum SiiFixtureStatus
{
    ParsedSyntaxOnly,
    DecodedContainerTextOnly,
    DecodedBinaryStructureOnly,
    UnclassifiedSyntax,
    ByteMismatch,
    InvalidSyntax,
    UnsupportedTextOrContainer,
    TooLarge
}

/// <summary>No status in this report establishes game compatibility or permission to write.</summary>
public sealed record SiiFixtureReport(
    SiiFixtureStatus Status,
    int ByteLength,
    string? Sha256,
    SiiTextEncoding? Encoding,
    bool? ByteRoundTripEqual,
    int? BlockCount,
    int? OpaqueLineCount,
    SiiSyntaxErrorCode? SyntaxError,
    int? ErrorLine)
{
    /// <summary>True if a bounded stream was stopped before its full length was read.</summary>
    public bool ByteLengthIsLowerBound { get; init; }
    public SiiSignatureHint SignatureHint { get; init; }
    public BinarySiiHeaderHint BinaryHeader { get; init; }
    /// <summary>Raw BSII payload version, not the game or save version.</summary>
    public uint? BinaryFormatVersion { get; init; }
    /// <summary>One-based parser column when available; otherwise unknown.</summary>
    public int? ErrorColumn { get; init; }
    /// <summary>Repeated non-null IDs beyond their first occurrence; null when syntax was not parsed.</summary>
    public int? DuplicateBlockIdOccurrences { get; init; }
    /// <summary>Repeated field names beyond their first occurrence in each block; null when syntax was not parsed.</summary>
    public int? DuplicateFieldOccurrences { get; init; }
    /// <summary>Whether all syntax lines were classified; not a semantic validity guarantee.</summary>
    public bool? DuplicateInspectionComplete => OpaqueLineCount is int count ? count == 0 : null;
    /// <summary>Decoded payload encoding; null for plain syntax and undecoded inputs.</summary>
    public SiiPayloadFormat? PayloadFormat { get; init; }
    /// <summary>True when a ScsC HMAC was not checked.</summary>
    public bool ContainerAuthenticationUnverified { get; init; }
    public bool ThreeNkDecoded { get; init; }
    public SiiSaveContainerError? ContainerError { get; init; }
    public ExperimentalBinarySiiError? BinaryError { get; init; }
    /// <summary>True only when experimental conversion produced text that passed syntax reparse; never a semantic compatibility claim.</summary>
    public bool? BinaryTextSyntaxValidated { get; init; }
    public string? BinaryTextConversionError { get; init; }
}
