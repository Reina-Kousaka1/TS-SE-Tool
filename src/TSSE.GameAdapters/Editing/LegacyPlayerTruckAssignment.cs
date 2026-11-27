using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>Explicit in-memory synthetic assignment; no game-semantic validation or file access.</summary>
public sealed class LegacyPlayerTruckAssignment
{
    private readonly SiiEditSession _session;
    private readonly string _playerId;
    private readonly string _arrayName, _fieldName, _targetType;

    public LegacyPlayerTruckAssignment(SiiSyntaxDocument document, string playerId)
        : this(new SiiEditSession(document), playerId) { }

    internal LegacyPlayerTruckAssignment(SiiEditSession session, string playerId)
        : this(session, playerId, "trucks", "assigned_truck", "vehicle") { }

    internal LegacyPlayerTruckAssignment(SiiEditSession session, string playerId, string arrayName, string fieldName, string targetType)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        _playerId = playerId;
        _arrayName = arrayName;
        _fieldName = fieldName;
        _targetType = targetType;
    }

    public string AssignedTruck => Validate().Assigned;
    public IReadOnlyList<string> EligibleTrucks => Validate().Trucks;
    public string PreviewText => _session.PreviewText;
    public bool Undo() => _session.Undo();
    public bool Redo() => _session.Redo();

    public void Assign(string truckId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(truckId);
        var snapshot = Validate();
        if (!snapshot.Trucks.Contains(truckId, StringComparer.Ordinal))
            throw new InvalidOperationException("The selected target is not uniquely listed by the player.");
        if (snapshot.Assigned != truckId)
            _session.Apply([new("player", _playerId, _fieldName, truckId)]);
    }

    private (string Assigned, IReadOnlyList<string> Trucks) Validate()
    {
        SiiSyntaxDocument document = SiiSyntaxDocument.Parse(_session.PreviewText);
        IReadOnlyList<string> trucks = LegacyReferenceArray.Read(document, "player", _playerId, _arrayName);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string truck in trucks)
        {
            if (truck is "nil" or "null" || !seen.Add(truck))
                throw new InvalidOperationException("Vehicle references are empty or duplicated.");
            SiiBlock[] targets = document.Blocks.Where(block => block.Id == truck).ToArray();
            if (targets.Length != 1 || targets[0].Type != _targetType)
                throw new InvalidOperationException("A vehicle reference is missing, ambiguous, or has the wrong type.");
        }
        SiiBlock player = document.Blocks.Single(block => block.Type == "player" && block.Id == _playerId);
        SiiField[] assigned = player.Fields.Where(item => item.Name == _fieldName).ToArray();
        if (assigned.Length != 1 || string.IsNullOrWhiteSpace(assigned[0].Value))
            throw new InvalidOperationException("The current assignment is missing or ambiguous.");
        string current = assigned[0].Value;
        if (current is not ("nil" or "null") && !seen.Contains(current))
            throw new InvalidOperationException("The current assignment is not uniquely listed.");
        return (current, trucks);
    }
}
