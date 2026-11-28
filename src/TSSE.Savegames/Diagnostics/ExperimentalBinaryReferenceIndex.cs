namespace TSSE.Savegames.Diagnostics;

/// <summary>
/// In-memory raw-identity diagnostics for experimental binary blocks. It
/// does not establish game semantics, ownership, access or write permission.
/// </summary>
public sealed class ExperimentalBinaryReferenceIndex
{
    private const int MaximumBlocks = ExperimentalBinarySiiReader.MaximumBlocks;
    private readonly Dictionary<string, List<Entry>> _entries = new(StringComparer.Ordinal);

    public ExperimentalBinaryReferenceIndex(IReadOnlyList<ExperimentalBinaryBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Count > MaximumBlocks) throw new ArgumentException("The block limit was exceeded.", nameof(blocks));
        for (int i = 0; i < blocks.Count; i++)
        {
            ExperimentalBinaryBlockIdentifier id = blocks[i]?.Identifier
                ?? throw new ArgumentException("A block identifier is missing.", nameof(blocks));
            if (id.RawParts is null || id.Marker == 0 || id.RawParts.Count != (id.Marker == 0xff ? 1 : id.Marker))
                throw new ArgumentException("A block identifier is unsupported.", nameof(blocks));
            ulong[] parts = id.RawParts.ToArray();
            if (!TryKey(id.Marker, parts, out string? key) || key is null)
                throw new ArgumentException("A block identifier is unsupported.", nameof(blocks));
            if (!_entries.TryGetValue(key, out List<Entry>? targets))
                _entries.Add(key, targets = []);
            targets.Add(new Entry(i, id.Marker, parts));
        }
    }

    public ExperimentalReferenceMatch Inspect(ExperimentalBinaryReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.TypeCode is not (0x39 or 0x3a or 0x3b or 0x3c or 0x3d) || reference.RawParts is null)
            return new(ExperimentalReferenceMatchKind.Unsupported, null);
        if (reference.RawParts.Count != (reference.Marker == 0xff ? 1 : reference.Marker))
            return new(ExperimentalReferenceMatchKind.Unsupported, null);
        ulong[] parts = reference.RawParts.ToArray();
        if (reference.IsNullMarker)
            return parts.Length == 0 ? new(ExperimentalReferenceMatchKind.NullMarker, null) : new(ExperimentalReferenceMatchKind.Unsupported, null);
        if (!TryKey(reference.Marker, parts, out string? key) || key is null)
            return new(ExperimentalReferenceMatchKind.Unsupported, null);
        if (!_entries.TryGetValue(key, out List<Entry>? targets)) return new(ExperimentalReferenceMatchKind.Missing, null);
        if (targets.Count != 1) return new(ExperimentalReferenceMatchKind.Ambiguous, null);
        Entry target = targets[0];
        if (target.Marker != reference.Marker || !target.Parts.AsSpan().SequenceEqual(parts))
            return new(ExperimentalReferenceMatchKind.LegacyTextOnly, null);
        return new(ExperimentalReferenceMatchKind.ExactRawIdentity, target.Index);
    }

    private static bool TryKey(byte marker, IReadOnlyList<ulong> parts, out string? key)
    {
        key = null;
        if (marker == 0xff && parts.Count == 1)
        {
            key = ExperimentalBinarySiiReader.NamelessText(parts[0]);
            return true;
        }
        if (marker is 0 or 0xff || parts.Count != marker) return false;
        return new ExperimentalBinaryReference(0, marker, parts).TryGetLegacyText(out key);
    }

    private sealed record Entry(int Index, byte Marker, ulong[] Parts);
}

public enum ExperimentalReferenceMatchKind { ExactRawIdentity, NullMarker, Missing, LegacyTextOnly, Ambiguous, Unsupported }
/// <summary>Only exact raw identity has an index; no result establishes game compatibility.</summary>
public sealed record ExperimentalReferenceMatch(ExperimentalReferenceMatchKind Kind, int? TargetIndex);
