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
        IReadOnlyList<string> entries = LegacyReferenceArray.Read(document, blockType, blockId, "accessories");
        var byId = document.Blocks.Where(block => block.Id is not null)
            .GroupBy(block => block.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var result = new List<AccessoryReference>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            string reference = entries[i];
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
            if (!KnownTypes.Contains(type))
            {
                result.Add(new(i, reference, AccessoryReferenceState.ResolvedUnknownType, type));
                continue;
            }
            SiiField[] paths = targets[0].Fields.Where(field => field.Name == "data_path").ToArray();
            var pathState = paths.Length switch
            {
                0 => AccessoryDataPathState.Missing,
                1 when !string.IsNullOrWhiteSpace(paths[0].Value) => AccessoryDataPathState.Present,
                1 => AccessoryDataPathState.Empty,
                _ => AccessoryDataPathState.Ambiguous
            };
            result.Add(new(i, reference, AccessoryReferenceState.ResolvedKnownType, type)
            {
                DataPathState = pathState,
                RawDataPath = pathState == AccessoryDataPathState.Present ? paths[0].Value : null
            });
        }
        return result.AsReadOnly();
    }
}

public enum AccessoryReferenceState { Empty, Missing, Ambiguous, ResolvedKnownType, ResolvedUnknownType }
public enum AccessoryDataPathState { NotInspected, Present, Missing, Empty, Ambiguous }
/// <summary>Raw data path is syntax only, never a filesystem path or verified asset.</summary>
public sealed record AccessoryReference(int Index, string RawReference, AccessoryReferenceState State, string? TargetBlockType)
{
    public AccessoryDataPathState DataPathState { get; init; }
    public string? RawDataPath { get; init; }
}
