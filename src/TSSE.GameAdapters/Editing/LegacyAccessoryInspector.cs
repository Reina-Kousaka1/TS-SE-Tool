using System.Globalization;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Inspects an explicitly selected legacy-shaped accessories array in synthetic
/// SII text. It does not infer a vehicle category or validate game semantics.
/// </summary>
public static class LegacyAccessoryInspector
{
    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        "vehicle_accessory", "vehicle_addon_accessory", "vehicle_drv_plate_accessory",
        "vehicle_wheel_accessory", "vehicle_paint_job_accessory",
        "vehicle_sound_accessory", "vehicle_cargo_accessory"
    };

    public static IReadOnlyList<AccessoryReference> InspectDeclaredArray(SiiSyntaxDocument document, string blockType, string blockId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax prevents safe inspection.");
        SiiBlock[] blocks = document.Blocks.Where(block => block.Type == blockType && block.Id == blockId).ToArray();
        if (blocks.Length != 1)
            throw new InvalidOperationException("The selected block is missing or ambiguous.");
        SiiField[] counts = blocks[0].Fields.Where(item => item.Name == "accessories").ToArray();
        if (counts.Length != 1 ||
            !int.TryParse(counts[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count is < 0 or > 1024)
            throw new InvalidOperationException("The accessories count is missing, ambiguous, or unsupported.");
        SiiField[] entries = blocks[0].Fields.Where(item => item.Name.StartsWith("accessories[", StringComparison.Ordinal)).ToArray();
        if (entries.Length != count)
            throw new InvalidOperationException("The accessories array is incomplete or ambiguous.");
        var byId = document.Blocks.Where(block => block.Id is not null)
            .GroupBy(block => block.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var result = new List<AccessoryReference>(count);
        for (int i = 0; i < count; i++)
        {
            string name = "accessories[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            SiiField[] matching = entries.Where(item => item.Name == name).ToArray();
            if (matching.Length != 1 || string.IsNullOrWhiteSpace(matching[0].Value))
                throw new InvalidOperationException("An accessory index is missing, ambiguous, or empty.");
            string reference = matching[0].Value;
            if (reference is "nil" or "null")
            {
                result.Add(new(i, reference, AccessoryReferenceState.Empty, null));
                continue;
            }
            if (!byId.TryGetValue(reference, out SiiBlock[]? targets))
            {
                result.Add(new(i, reference, AccessoryReferenceState.Missing, null));
                continue;
            }
            if (targets.Length != 1)
            {
                result.Add(new(i, reference, AccessoryReferenceState.Ambiguous, null));
                continue;
            }
            string type = targets[0].Type;
            result.Add(new(i, reference,
                KnownTypes.Contains(type) ? AccessoryReferenceState.ResolvedKnownType : AccessoryReferenceState.ResolvedUnknownType,
                type));
        }
        return result.AsReadOnly();
    }
}

public enum AccessoryReferenceState { Empty, Missing, Ambiguous, ResolvedKnownType, ResolvedUnknownType }
public sealed record AccessoryReference(int Index, string RawReference, AccessoryReferenceState State, string? TargetBlockType);
