using System.Globalization;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Read-only model for Coaches job, stage, and passenger references observed in
/// a supplied SII document. Raw blocks are retained; no field semantics are inferred.
/// </summary>
public sealed class LegacyCoachJobInspector
{
    public LegacyCoachJobInspector(SiiSyntaxDocument document, string? playerId)
    {
        ArgumentNullException.ThrowIfNull(document);

        LegacyCoachUnitReference? currentJob = null;
        if (playerId is not null)
        {
            SiiBlock player = FindUniqueBlock(document, "player", playerId);
            string? currentJobId = OptionalValue(player, "current_bus_job");
            if (!IsNullReference(currentJobId))
                currentJob = ResolveReference(document, currentJobId!);
        }

        SiiBlock[] jobs = document.Blocks.Where(block => block.Type == "player_bus_job").ToArray();
        if (jobs.Where(block => block.Id is not null).GroupBy(block => block.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Observed Coaches job block identifiers are ambiguous.");

        Snapshot = new LegacyCoachJobInspection(
            currentJob,
            Array.AsReadOnly(jobs.Select(job => InspectJob(document, job)).ToArray()));
    }

    public LegacyCoachJobInspection Snapshot { get; }

    private static LegacyCoachJobSnapshot InspectJob(SiiSyntaxDocument document, SiiBlock job)
    {
        IReadOnlyList<LegacyCoachJobStageSnapshot>? stages = ReadStages(document, job);
        IReadOnlyList<LegacyCoachPassengerSnapshot>? passengers = ReadPassengers(document, job);
        return new LegacyCoachJobSnapshot(job, stages, passengers);
    }

    private static IReadOnlyList<LegacyCoachJobStageSnapshot>? ReadStages(SiiSyntaxDocument document, SiiBlock job)
    {
        IReadOnlyList<string>? ids = ReadArray(job, "stages");
        return ids is null
            ? null
            : Array.AsReadOnly(ids.Select(id => CreateStageSnapshot(document, id)).ToArray());
    }

    private static IReadOnlyList<LegacyCoachPassengerSnapshot>? ReadPassengers(SiiSyntaxDocument document, SiiBlock job)
    {
        IReadOnlyList<string>? ids = ReadArray(job, "passengers");
        return ids is null
            ? null
            : Array.AsReadOnly(ids.Select(id => CreatePassengerSnapshot(document, id)).ToArray());
    }

    private static LegacyCoachJobStageSnapshot CreateStageSnapshot(SiiSyntaxDocument document, string id)
    {
        LegacyCoachUnitReference reference = ResolveReference(document, id);
        return new LegacyCoachJobStageSnapshot(reference, FindResolvedBlock(document, reference, "player_bus_job_stage"));
    }

    private static LegacyCoachPassengerSnapshot CreatePassengerSnapshot(SiiSyntaxDocument document, string id)
    {
        LegacyCoachUnitReference reference = ResolveReference(document, id);
        return new LegacyCoachPassengerSnapshot(reference, FindResolvedBlock(document, reference, "player_bus_passenger"));
    }

    private static SiiBlock? FindResolvedBlock(SiiSyntaxDocument document, LegacyCoachUnitReference reference, string expectedType)
    {
        if (reference.State != LegacyCoachReferenceState.Resolved || reference.ResolvedType != expectedType)
            return null;
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == reference.Id && block.Type == expectedType).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static IReadOnlyList<string>? ReadArray(SiiBlock block, string name)
    {
        string prefix = name + "[";
        SiiField[] fields = block.Fields.Where(field => field.Name == name || field.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (fields.Length == 0) return null;

        SiiField[] countFields = fields.Where(field => field.Name == name).ToArray();
        if (countFields.Length != 1 ||
            !int.TryParse(countFields[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count < 0 || count > fields.Length - 1)
            throw new InvalidOperationException($"The observed Coaches '{name}' array is missing or malformed.");

        var values = new string[count];
        var seen = new bool[count];
        foreach (SiiField field in fields)
        {
            if (field.Name == name) continue;
            if (!IsArrayElement(field.Name, name, out int index) || index >= count || seen[index])
                throw new InvalidOperationException($"The observed Coaches '{name}' array has invalid or duplicate indices.");
            values[index] = field.Value;
            seen[index] = true;
        }
        if (seen.Any(value => !value) || values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"The observed Coaches '{name}' array is incomplete.");
        return Array.AsReadOnly(values);
    }

    private static bool IsArrayElement(string fieldName, string name, out int index)
    {
        index = -1;
        string prefix = name + "[";
        return fieldName.StartsWith(prefix, StringComparison.Ordinal) && fieldName.EndsWith(']') &&
            int.TryParse(fieldName.AsSpan(prefix.Length, fieldName.Length - prefix.Length - 1),
                NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static LegacyCoachUnitReference ResolveReference(SiiSyntaxDocument document, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == id).ToArray();
        return matches.Length switch
        {
            0 => new LegacyCoachUnitReference(id, LegacyCoachReferenceState.Unresolved, null),
            1 => new LegacyCoachUnitReference(id, LegacyCoachReferenceState.Resolved, matches[0].Type),
            _ => new LegacyCoachUnitReference(id, LegacyCoachReferenceState.Ambiguous, null)
        };
    }

    private static SiiBlock FindUniqueBlock(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == id).ToArray();
        return matches.Length == 1 && matches[0].Type == type
            ? matches[0]
            : throw new InvalidOperationException($"The selected {type} block is missing or ambiguous.");
    }

    private static string? OptionalValue(SiiBlock block, string name)
    {
        SiiField[] fields = block.Fields.Where(field => field.Name == name).ToArray();
        return fields.Length switch
        {
            0 => null,
            1 => fields[0].Value,
            _ => throw new InvalidOperationException($"The observed '{name}' field is duplicated.")
        };
    }

    private static bool IsNullReference(string? value) => string.IsNullOrWhiteSpace(value) ||
        value.Equals("nil", StringComparison.OrdinalIgnoreCase) || value.Equals("null", StringComparison.OrdinalIgnoreCase);
}

public sealed record LegacyCoachJobInspection(
    LegacyCoachUnitReference? CurrentJob,
    IReadOnlyList<LegacyCoachJobSnapshot> Jobs);

/// <summary>The original SII block preserves observed and unknown fields unchanged.</summary>
public sealed record LegacyCoachJobSnapshot(
    SiiBlock Source,
    IReadOnlyList<LegacyCoachJobStageSnapshot>? Stages,
    IReadOnlyList<LegacyCoachPassengerSnapshot>? Passengers);

/// <summary>The observed stage block is preserved without assigning field semantics.</summary>
public sealed record LegacyCoachJobStageSnapshot(LegacyCoachUnitReference Reference, SiiBlock? Source);

/// <summary>The observed passenger block is preserved without assigning field semantics.</summary>
public sealed record LegacyCoachPassengerSnapshot(LegacyCoachUnitReference Reference, SiiBlock? Source);
