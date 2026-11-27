using System.Globalization;
using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Experimental, in-memory job editing for synthetic legacy-shaped data.</summary>
public sealed class LegacyJobPreview
{
    private readonly SiiEditSession _session;
    private readonly string? _jobId;

    public LegacyJobPreview(SiiSyntaxDocument document, string playerId)
        : this(new SiiEditSession(document), playerId)
    {
    }

    internal LegacyJobPreview(SiiEditSession session, string playerId)
    {
        ArgumentNullException.ThrowIfNull(session);
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(session.PreviewText);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax cannot be safely edited.");
        string reference = Value(Block(document, "player", playerId), "current_job");
        _jobId = reference is "null" or "nil" ? null : reference;
        if (_jobId is not null)
        {
            if (string.IsNullOrWhiteSpace(_jobId))
                throw new InvalidOperationException("The job reference is empty.");
            SiiBlock job = Block(document, "player_job", _jobId);
            _ = Value(job, "cargo");
            _ = ReadUnits(job);
        }
        _session = session;
    }

    public bool HasActiveJob => _jobId is not null;
    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public string CargoReference => Value(CurrentJob(), "cargo");
    public int UnitsCount => ReadUnits(CurrentJob());
    public int PlannedDistanceKm => ReadNonnegativeInt(CurrentJob(), "planned_distance_km");
    /// <summary>Legacy-shaped synthetic integer only; no cargo model semantics are validated.</summary>
    public int CargoModelIndex => ReadNonnegativeInt(CurrentJob(), "cargo_model_index");
    /// <summary>Historical synthetic urgency value; current game meaning is unverified.</summary>
    public int Urgency
    {
        get
        {
            int value = ReadNonnegativeInt(CurrentJob(), "urgency");
            return value <= 2 ? value : throw new InvalidOperationException("The urgency is unsupported.");
        }
    }
    public string SourceCompany => RouteValue(CurrentJob(), "source_company");
    public string TargetCompany => RouteValue(CurrentJob(), "target_company");

    /// <summary>Lists distinct raw cargo references observed in synthetic legacy-shaped job blocks.</summary>
    public IReadOnlyList<string> ObservedCargoReferences()
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
        if (document.OpaqueLines.Count != 0) throw new InvalidOperationException("Unclassified syntax cannot be inspected.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (SiiBlock block in document.Blocks.Where(item => item.Type is "player_job" or "job_offer_data" or "job_info"))
        {
            string raw = Value(block, "cargo");
            if (!raw.StartsWith("cargo.", StringComparison.Ordinal) || raw.Length <= 6 ||
                !raw.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.'))
                throw new InvalidOperationException("A cargo reference is unsupported.");
            if (seen.Add(raw)) result.Add(raw);
        }
        return result.AsReadOnly();
    }

    /// <summary>Changes only the synthetic reference, without validating cargo, trailer or job semantics.</summary>
    public void SelectObservedCargo(string cargo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cargo);
        string current = CargoReference;
        if (!ObservedCargoReferences().Contains(cargo, StringComparer.Ordinal))
            throw new InvalidOperationException("The cargo reference was not observed in the document.");
        if (current != cargo) _session.Apply([new("player_job", _jobId, "cargo", cargo)]);
    }

    /// <summary>Swaps two existing synthetic raw references without inventing route semantics.</summary>
    public void SwapRoute()
    {
        SiiBlock job = CurrentJob();
        string source = RouteValue(job, "source_company");
        string target = RouteValue(job, "target_company");
        if (source == target) return;
        _session.Apply([
            new("player_job", _jobId, "source_company", target),
            new("player_job", _jobId, "target_company", source)
        ]);
    }

    public void SetUnitsCount(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (UnitsCount == count) return;
        _session.Apply([new("player_job", _jobId, "units_count", count.ToString(CultureInfo.InvariantCulture))]);
    }

    public void SetPlannedDistanceKm(int distance)
    {
        if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
        if (PlannedDistanceKm == distance) return;
        _session.Apply([new("player_job", _jobId, "planned_distance_km", distance.ToString(CultureInfo.InvariantCulture))]);
    }

    public void SetCargoModelIndex(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (CargoModelIndex == index) return;
        _session.Apply([new("player_job", _jobId, "cargo_model_index", index.ToString(CultureInfo.InvariantCulture))]);
    }

    public void SetUrgency(int urgency)
    {
        if (urgency is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(urgency));
        if (Urgency == urgency) return;
        _session.Apply([new("player_job", _jobId, "urgency", urgency.ToString(CultureInfo.InvariantCulture))]);
    }

    /// <summary>Applies three syntactic values together; game-level compatibility is not validated.</summary>
    public void SetCargoDetails(string cargo, int count, int modelIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cargo);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (modelIndex < 0) throw new ArgumentOutOfRangeException(nameof(modelIndex));
        string currentCargo = CargoReference;
        int currentCount = UnitsCount;
        int currentModel = CargoModelIndex;
        if (!ObservedCargoReferences().Contains(cargo, StringComparer.Ordinal))
            throw new InvalidOperationException("The cargo reference was not observed in the document.");
        var edits = new List<SiiFieldEdit>();
        if (cargo != currentCargo) edits.Add(new("player_job", _jobId, "cargo", cargo));
        if (count != currentCount) edits.Add(new("player_job", _jobId, "units_count", count.ToString(CultureInfo.InvariantCulture)));
        if (modelIndex != currentModel) edits.Add(new("player_job", _jobId, "cargo_model_index", modelIndex.ToString(CultureInfo.InvariantCulture)));
        _session.Apply(edits);
    }

    /// <summary>Updates both validated synthetic job values in a single undoable transaction.</summary>
    public void SetUnitsAndPlannedDistance(int count, int distance)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
        int currentCount = UnitsCount;
        int currentDistance = PlannedDistanceKm;
        var edits = new List<SiiFieldEdit>();
        if (count != currentCount)
            edits.Add(new("player_job", _jobId, "units_count", count.ToString(CultureInfo.InvariantCulture)));
        if (distance != currentDistance)
            edits.Add(new("player_job", _jobId, "planned_distance_km", distance.ToString(CultureInfo.InvariantCulture)));
        _session.Apply(edits);
    }

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private SiiBlock CurrentJob()
    {
        if (_jobId is null) throw new InvalidOperationException("There is no active job.");
        return Block(SiiSyntaxDocument.Parse(_session.PreviewText), "player_job", _jobId);
    }

    private static int ReadUnits(SiiBlock job)
        => ReadNonnegativeInt(job, "units_count");

    private static string RouteValue(SiiBlock job, string name)
    {
        string value = Value(job, name);
        if (string.IsNullOrWhiteSpace(value) || value is "nil" or "null")
            throw new InvalidOperationException("The route reference is empty or unsupported.");
        return value;
    }

    private static int ReadNonnegativeInt(SiiBlock job, string field)
    {
        if (!int.TryParse(Value(job, field), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int count) || count < 0)
            throw new InvalidOperationException("The requested value is invalid or unsupported.");
        return count;
    }

    private static SiiBlock Block(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Id == id).ToArray();
        return matches.Length == 1 && matches[0].Type == type
            ? matches[0] : throw new InvalidOperationException("The requested block is missing or ambiguous.");
    }

    private static string Value(SiiBlock block, string name)
    {
        SiiField[] matches = block.Fields.Where(field => field.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : throw new InvalidOperationException("The requested field is missing or ambiguous.");
    }
}
