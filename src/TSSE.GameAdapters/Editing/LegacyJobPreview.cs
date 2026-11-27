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

    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    private SiiBlock CurrentJob()
    {
        if (_jobId is null) throw new InvalidOperationException("There is no active job.");
        return Block(SiiSyntaxDocument.Parse(_session.PreviewText), "player_job", _jobId);
    }

    private static int ReadUnits(SiiBlock job)
        => ReadNonnegativeInt(job, "units_count");

    private static int ReadNonnegativeInt(SiiBlock job, string field)
    {
        if (!int.TryParse(Value(job, field), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int count) || count < 0)
            throw new InvalidOperationException("The requested value is invalid or unsupported.");
        return count;
    }

    private static SiiBlock Block(SiiSyntaxDocument document, string type, string id)
    {
        SiiBlock[] matches = document.Blocks.Where(block => block.Type == type && block.Id == id).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("The requested block is missing or ambiguous.");
    }

    private static string Value(SiiBlock block, string name)
    {
        SiiField[] matches = block.Fields.Where(field => field.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : throw new InvalidOperationException("The requested field is missing or ambiguous.");
    }
}
