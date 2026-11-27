using TSSE.Savegames.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Editing;

/// <summary>In-memory synthetic trailer reference assignment; related game state is unvalidated.</summary>
public sealed class LegacyPlayerTrailerAssignment
{
    private readonly LegacyPlayerTruckAssignment _assignment;
    public LegacyPlayerTrailerAssignment(SiiSyntaxDocument document, string playerId)
        : this(new SiiEditSession(document), playerId) { }
    internal LegacyPlayerTrailerAssignment(SiiEditSession session, string playerId) =>
        _assignment = new(session, playerId, "trailers", "assigned_trailer", "trailer");
    public string AssignedTrailer => _assignment.AssignedTruck;
    public IReadOnlyList<string> EligibleTrailers => _assignment.EligibleTrucks;
    public string PreviewText => _assignment.PreviewText;
    public bool Undo() => _assignment.Undo();
    public bool Redo() => _assignment.Redo();
    public void Assign(string trailerId) => _assignment.Assign(trailerId);
}
