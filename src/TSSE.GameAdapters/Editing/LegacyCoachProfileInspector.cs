using System.Globalization;
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

        return new LegacyCoachEconomySnapshot(
            counters,
            stopIds,
            jobLogId,
            jobLogVersion,
            jobLogEntryCount,
            stops);
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
}

public sealed record LegacyCoachEconomySnapshot(
    IReadOnlyDictionary<string, int?> Counters,
    IReadOnlyList<string> StopIds,
    string? JobLogId,
    int? JobLogVersion,
    int? JobLogEntryCount,
    IReadOnlyList<LegacyCoachStopSnapshot> Stops);

public sealed record LegacyCoachStopSnapshot(string Id, bool Discovered, int OfferedLineCount);
