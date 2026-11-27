using System.Security.Cryptography;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Inspects caller-supplied plain-text bytes in memory. It performs no file
/// access and makes no claim about game versions, containers or game validity.
/// </summary>
public static class SiiFixtureInspector
{
    /// <summary>
    /// Inspects an explicitly supplied stream, reading at most the byte limit
    /// plus one detection byte. The caller retains ownership of the stream.
    /// No file discovery, writes, decompression, or game validation is performed.
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
            return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                null, null, null, null) { SignatureHint = hint };
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

/// <summary>A four-byte hint only, never proof that a container is valid or decodable.</summary>
public enum SiiSignatureHint { Unknown, TextPrefix, EncryptedPrefix, BinaryPrefix, ThreeNkPrefix }

public enum SiiFixtureStatus
{
    ParsedSyntaxOnly,
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
    /// <summary>One-based parser column when available; otherwise unknown.</summary>
    public int? ErrorColumn { get; init; }
    /// <summary>Repeated non-null IDs beyond their first occurrence; null when syntax was not parsed.</summary>
    public int? DuplicateBlockIdOccurrences { get; init; }
    /// <summary>Repeated field names beyond their first occurrence in each block; null when syntax was not parsed.</summary>
    public int? DuplicateFieldOccurrences { get; init; }
    /// <summary>Whether all syntax lines were classified; not a semantic validity guarantee.</summary>
    public bool? DuplicateInspectionComplete => OpaqueLineCount is int count ? count == 0 : null;
}
