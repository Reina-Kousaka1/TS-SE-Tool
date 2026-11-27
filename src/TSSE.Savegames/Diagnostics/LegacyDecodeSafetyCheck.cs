using TSSE.Savegames.Syntax;

namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// Checks caller-supplied results from the legacy two-call decoder contract.
/// It does not load a native library, decode a container, or establish game compatibility.
/// </summary>
public static class LegacyDecodeSafetyCheck
{
    /// <summary>
    /// Checks a reported size before allocation. A true result does not authorize decoding or file access.
    /// </summary>
    public static bool TryGetBoundedOutputSize(int sizeQueryResult, uint announcedBytes, out int boundedBytes)
    {
        boundedBytes = 0;
        if (sizeQueryResult != 0 || announcedBytes == 0 || announcedBytes > SiiEncodedText.MaximumBytes)
            return false;
        boundedBytes = (int)announcedBytes;
        return true;
    }

    public static LegacyDecodeCheckResult Inspect(
        int sizeQueryResult, int decodeResult, uint announcedBytes, uint producedBytes, ReadOnlySpan<byte> output)
    {
        if (sizeQueryResult != 0) return new(LegacyDecodeCheckStatus.SizeQueryFailed, null);
        if (!TryGetBoundedOutputSize(sizeQueryResult, announcedBytes, out _))
            return new(LegacyDecodeCheckStatus.InvalidSize, null);
        if (decodeResult != 0) return new(LegacyDecodeCheckStatus.DecodeFailed, null);
        if (producedBytes != announcedBytes || output.Length != announcedBytes)
            return new(LegacyDecodeCheckStatus.SizeMismatch, null);

        SiiFixtureReport report = SiiFixtureInspector.Inspect(output);
        if (report.Status != SiiFixtureStatus.ParsedSyntaxOnly || report.ByteRoundTripEqual != true ||
            report.DuplicateBlockIdOccurrences != 0 || report.DuplicateFieldOccurrences != 0)
            return new(LegacyDecodeCheckStatus.OutputRejected, report);
        return new(LegacyDecodeCheckStatus.SyntaxOnly, report);
    }
}

public enum LegacyDecodeCheckStatus { SizeQueryFailed, InvalidSize, DecodeFailed, SizeMismatch, OutputRejected, SyntaxOnly }

/// <summary>Syntax evidence only; no state here enables reading or writing a game save.</summary>
public sealed record LegacyDecodeCheckResult(LegacyDecodeCheckStatus Status, SiiFixtureReport? Report)
{
    public bool GameCompatibilityEstablished => false;
    public bool SaveWritingEnabled => false;
}
