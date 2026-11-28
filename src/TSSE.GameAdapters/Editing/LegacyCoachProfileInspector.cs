using System.Globalization;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// Read-only inspection of the Coaches-shaped fields understood by the historical TS-SE Tool.
/// This schema is not evidence of compatibility with current ETS2 Coaches DLC saves.
/// </summary>
public sealed class LegacyCoachProfileInspector
{
    private static readonly string[] Counters =
    [
        "bus_experience_points", "bus_total_distance", "bus_finished_job_count",
        "bus_cancelled_job_count", "bus_total_passengers", "bus_total_stops",
        "bus_game_time", "bus_playing_time"
    ];

    private readonly SiiSyntaxDocument _document;
    private readonly SiiBlock _economy;

    public LegacyCoachProfileInspector(SiiSyntaxDocument document, string economyId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(economyId);
        _document = document;
        _economy = FindUniqueBlock(document, "economy", economyId);
        Snapshot = ReadSnapshot();
    }

    public LegacyCoachEconomySnapshot Snapshot { get; }

    /// <summary>Reads only the historical Int32 Coaches counters from one explicitly decoded BSII economy block.</summary>
    public static IReadOnlyDictionary<string, int?> InspectBinaryCounters(IReadOnlyList<ExperimentalBinaryBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ExperimentalBinaryBlock[] matches = blocks.Where(block => block.Type == "economy").ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("The binary economy block is missing or ambiguous.");
        ExperimentalBinaryBlock economy = matches[0];
        var counters = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (string name in Counters)
        {
            if (!economy.RawFields.ContainsKey(name))
            {
                counters.Add(name, null);
                continue;
            }
            if (!economy.Int32Fields.TryGetValue(name, out int value))
                throw new InvalidOperationException($"The historical binary Coaches counter '{name}' has an unsupported representation.");
            counters.Add(name, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, int?>(counters);
    }

    /// <summary>Reports only raw BSII type codes for present candidate skill fields; it assigns no meaning to their values.</summary>
    public static IReadOnlyDictionary<string, uint?> InspectBinaryCandidateSkillTypeCodes(IReadOnlyList<ExperimentalBinaryBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ExperimentalBinaryBlock[] matches = blocks.Where(block => block.Type == "economy").ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("The binary economy block is missing or ambiguous.");
        ExperimentalBinaryBlock economy = matches[0];
        var types = new Dictionary<string, uint?>(StringComparer.Ordinal);
        foreach (string name in LegacyCoachSkillCatalog.FieldNames)
        {
            if (!economy.RawFields.TryGetValue(name, out ExperimentalBinaryRawField raw))
            {
                types.Add(name, null);
                continue;
            }
            ExperimentalBinaryFieldDefinition[] definitions = economy.FieldDefinitions.Where(field => field.Name == name).ToArray();
            if (definitions.Length != 1 || definitions[0].TypeCode != raw.TypeCode)
                throw new InvalidOperationException($"The binary candidate Coaches skill field '{name}' has ambiguous type metadata.");
            types.Add(name, raw.TypeCode);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, uint?>(types);
    }

    private LegacyCoachEconomySnapshot ReadSnapshot()
    {
        Dictionary<string, int?> counters = Counters.ToDictionary(
            name => name,
            name => OptionalInt32(_economy, name),
            StringComparer.Ordinal);

        string? jobLogId = OptionalValue(_economy, "bus_job_log");
        if (jobLogId == "nil") jobLogId = null;
        int? jobLogVersion = null;
        int? jobLogEntryCount = null;
        if (jobLogId is not null)
        {
            SiiBlock log = FindUniqueBlock(_document, "bus_job_log", jobLogId);
            jobLogVersion = RequiredInt32(log, "version");
            jobLogEntryCount = ReadArray(log, "entries").Count;
        }

        IReadOnlyList<string> stopIds = ReadArray(_economy, "bus_stops");
        var stops = new List<LegacyCoachStopSnapshot>(stopIds.Count);
        foreach (string id in stopIds)
        {
            SiiBlock stop = FindUniqueBlock(_document, "bus_stop", id);
            bool discovered = RequiredValue(stop, "discovered") switch
            {
                "true" => true,
                "false" => false,
                _ => throw new InvalidOperationException("A legacy bus_stop discovered value is invalid.")
            };
            stops.Add(new LegacyCoachStopSnapshot(id, discovered, ReadArray(stop, "lines_offer").Count));
        }

        LegacyCoachPlayerSnapshot? player = null;
        string? playerId = OptionalValue(_economy, "player");
        if (!IsNullReference(playerId))
        {
            SiiBlock referencedPlayer = FindUniqueBlock(_document, "player", playerId!);
            IReadOnlyList<string> busIds = ReadArray(referencedPlayer, "buses");
            var buses = busIds.Select(id => ResolveReference(_document, id)).ToArray();
            string? currentJobId = OptionalValue(referencedPlayer, "current_bus_job");
            LegacyCoachUnitReference? currentJob = IsNullReference(currentJobId)
                ? null : ResolveReference(_document, currentJobId!);
            player = new LegacyCoachPlayerSnapshot(
                playerId!,
                OptionalValue(referencedPlayer, "my_vehicles_mode"),
                OptionalValue(referencedPlayer, "assigned_vehicles_mode"),
                busIds,
                buses,
                currentJob);
        }

        return new LegacyCoachEconomySnapshot(
            counters,
            ReadCoachSkills(_economy),
            stopIds,
            jobLogId,
            jobLogVersion,
            jobLogEntryCount,
            stops,
            player);
    }

    private static IReadOnlyDictionary<string, int?> ReadCoachSkills(SiiBlock economy)
    {
        var skills = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (string name in LegacyCoachSkillCatalog.FieldNames)
        {
            int? value = OptionalInt32(economy, name);
            if (value is int rank && rank is < 0 or > LegacyCoachSkillCatalog.MaximumRank)
                throw new InvalidOperationException($"The candidate Coaches skill '{name}' is outside its supported synthetic 0–3 range.");
            skills.Add(name, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, int?>(skills);
    }

    private static IReadOnlyList<string> ReadArray(SiiBlock block, string name)
    {
        string prefix = name + "[";
        SiiField[] fields = block.Fields.Where(field => field.Name == name || field.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (fields.Length == 0) return Array.Empty<string>();
        SiiField[] counts = fields.Where(field => field.Name == name).ToArray();
        if (counts.Length != 1 || !int.TryParse(counts[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0 || count > fields.Length - 1)
            throw new InvalidOperationException($"The legacy '{name}' array count is missing or invalid.");

        var values = new string[count];
        var seen = new bool[count];
        foreach (SiiField field in fields)
        {
            if (field.Name == name) continue;
            if (!IsArrayElement(field.Name, name, out int index) || index >= count || seen[index])
                throw new InvalidOperationException($"The legacy '{name}' array indices are invalid or duplicated.");
            values[index] = field.Value;
            seen[index] = true;
        }
        if (seen.Any(present => !present) || values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"The legacy '{name}' array is incomplete.");
        return Array.AsReadOnly(values);
    }

    private static bool IsArrayElement(string fieldName, string name, out int index)
    {
        index = -1;
        string prefix = name + "[";
        return fieldName.StartsWith(prefix, StringComparison.Ordinal) && fieldName.EndsWith(']') &&
               int.TryParse(fieldName.AsSpan(prefix.Length, fieldName.Length - prefix.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static int? OptionalInt32(SiiBlock block, string name)
    {
        string? value = OptionalValue(block, name);
        if (value is null) return null;
        return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed)
            ? parsed : throw new InvalidOperationException($"The legacy '{name}' value is not a supported Int32.");
    }

    private static int RequiredInt32(SiiBlock block, string name) => OptionalInt32(block, name)
        ?? throw new InvalidOperationException($"The legacy '{name}' value is missing.");

    private static string RequiredValue(SiiBlock block, string name) => OptionalValue(block, name)
        ?? throw new InvalidOperationException($"The legacy '{name}' field is missing.");

    private static string? OptionalValue(SiiBlock block, string name)
    {
        SiiField[] fields = block.Fields.Where(field => field.Name == name).ToArray();
        return fields.Length switch
        {
            0 => null,
            1 => fields[0].Value,
            _ => throw new InvalidOperationException($"The legacy '{name}' field is duplicated.")
        };
    }

    private static SiiBlock FindUniqueBlock(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == id).ToArray();
        return matches.Length == 1 && matches[0].Type == type
            ? matches[0]
            : throw new InvalidOperationException($"The legacy {type} reference is missing, ambiguous, or has the wrong type.");
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

    private static bool IsNullReference(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("nil", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("null", StringComparison.OrdinalIgnoreCase);
}

public sealed record LegacyCoachEconomySnapshot(
    IReadOnlyDictionary<string, int?> Counters,
    IReadOnlyDictionary<string, int?> CoachSkills,
    IReadOnlyList<string> StopIds,
    string? JobLogId,
    int? JobLogVersion,
    int? JobLogEntryCount,
    IReadOnlyList<LegacyCoachStopSnapshot> Stops,
    LegacyCoachPlayerSnapshot? Player);

public sealed record LegacyCoachStopSnapshot(string Id, bool Discovered, int OfferedLineCount);

public sealed record LegacyCoachPlayerSnapshot(
    string Id,
    string? MyVehiclesMode,
    string? AssignedVehiclesMode,
    IReadOnlyList<string> BusIds,
    IReadOnlyList<LegacyCoachUnitReference> Buses,
    LegacyCoachUnitReference? CurrentBusJob);

public sealed record LegacyCoachUnitReference(string Id, LegacyCoachReferenceState State, string? ResolvedType);

public enum LegacyCoachReferenceState { Resolved, Unresolved, Ambiguous }

/// <summary>Candidate 1.62 Coaches skill fields reported by an independent implementation; not an official schema.</summary>
public static class LegacyCoachSkillCatalog
{
    private static readonly string[] Names =
    ["long_duty", "tight_schedule", "satisfaction_decrease", "low_acceleration", "satisfaction_increase", "ecodriving"];
    private static readonly IReadOnlyList<string> ReadOnlyNames = Array.AsReadOnly(Names);

    public static IReadOnlyList<string> FieldNames => ReadOnlyNames;
    public const byte MaximumRank = 3;
}
