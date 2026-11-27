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
    /// <summary>Selects an explicit synthetic trailer in the same in-memory history.</summary>
    public LegacyVehiclePreview SelectTrailer(string blockId) => new(_session, LegacyVehicleKind.Trailer, blockId);
    public string PreviewText => _session.PreviewText;
    public IReadOnlyList<SiiFieldChange> Changes => _session.Changes;
    public bool IsDirty => _session.IsDirty;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();
}
