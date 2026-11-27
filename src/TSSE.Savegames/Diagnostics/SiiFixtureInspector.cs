using System.Security.Cryptography;
using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Inspects caller-supplied plain-text bytes in memory. It performs no file
/// access and makes no claim about game versions, containers or game validity.
/// </summary>
public static class SiiFixtureInspector
{
    public static SiiFixtureReport Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > SiiEncodedText.MaximumBytes)
            return new(SiiFixtureStatus.TooLarge, bytes.Length, null, null, null, null, null, null, null);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        try
        {
            SiiEncodedText text = SiiEncodedText.Parse(bytes);
            bool equal = bytes.SequenceEqual(text.ToBytes());
            int opaqueCount = text.Syntax.OpaqueLines.Count;
            SiiFixtureStatus status = !equal ? SiiFixtureStatus.ByteMismatch :
                opaqueCount > 0 ? SiiFixtureStatus.UnclassifiedSyntax : SiiFixtureStatus.ParsedSyntaxOnly;
            return new(status, bytes.Length, hash, text.EncodingKind, equal,
                text.Syntax.Blocks.Count, opaqueCount, null, null);
        }
        catch (SiiSyntaxException ex)
        {
            return new(SiiFixtureStatus.InvalidSyntax, bytes.Length, hash, null, null,
                null, null, ex.ErrorCode, ex.LineNumber);
        }
        catch (InvalidDataException)
        {
            return new(SiiFixtureStatus.UnsupportedTextOrContainer, bytes.Length, hash, null, null,
                null, null, null, null);
        }
    }
}

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
    int? ErrorLine);
