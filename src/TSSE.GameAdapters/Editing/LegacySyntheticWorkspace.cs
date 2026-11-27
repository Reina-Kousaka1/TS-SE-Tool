using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>
/// A single in-memory history for explicitly selected synthetic profile,
/// truck, trailer and job blocks. No file access or game compatibility implied.
/// </summary>
public sealed class LegacySyntheticWorkspace
{
    private readonly SiiEditSession _session;

    public LegacySyntheticWorkspace(SiiSyntaxDocument document, string economyId, string playerId, string truckId, string trailerId)
    {
        ArgumentNullException.ThrowIfNull(document);
        _session = new SiiEditSession(document);
        Profile = new LegacyProfilePreview(_session, economyId);
        Truck = new LegacyVehiclePreview(_session, LegacyVehicleKind.Truck, truckId);
        Trailer = new LegacyVehiclePreview(_session, LegacyVehicleKind.Trailer, trailerId);
        Job = new LegacyJobPreview(_session, playerId);
    }

    public LegacyProfilePreview Profile { get; }
    public LegacyVehiclePreview Truck { get; }
    public LegacyVehiclePreview Trailer { get; }
    public LegacyJobPreview Job { get; }
    /// <summary>
    /// Lists explicit trailer block identifiers in source order, without
    /// interpreting ownership, links, or game-version compatibility.
    /// </summary>
    public IReadOnlyList<string> TrailerIds() => VehicleIds("trailer");
    public IReadOnlyList<string> TruckIds() => VehicleIds("vehicle");

    private IReadOnlyList<string> VehicleIds(string type)
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
        if (document.OpaqueLines.Count != 0)
            throw new InvalidOperationException("Unclassified syntax prevents safe trailer selection.");
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (SiiBlock block in document.Blocks.Where(item => item.Type == type))
        {
            if (string.IsNullOrWhiteSpace(block.Id) || !seen.Add(block.Id))
                throw new InvalidOperationException("A vehicle identifier is missing or ambiguous.");
            ids.Add(block.Id);
        }
        return ids.AsReadOnly();
    }
    /// <summary>Selects an explicit synthetic trailer in the same in-memory history.</summary>
    public LegacyVehiclePreview SelectTrailer(string blockId) => new(_session, LegacyVehicleKind.Trailer, blockId);
    /// <summary>Selects an explicit synthetic truck in the same in-memory history.</summary>
    public LegacyVehiclePreview SelectTruck(string blockId) => new(_session, LegacyVehicleKind.Truck, blockId);
    public LegacyPlayerTruckAssignment SelectPlayerTruckAssignment(string playerId) => new(_session, playerId);
    public LegacyPlayerTrailerAssignment SelectPlayerTrailerAssignment(string playerId) => new(_session, playerId);
    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();
}
