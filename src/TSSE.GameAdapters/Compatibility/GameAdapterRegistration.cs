namespace TSSE.GameAdapters.Compatibility;

public sealed record GameAdapterRegistration(
    string GameId,
    string SaveVersion,
    bool CanRead,
    bool CanWrite,
    string EvidenceReference)
{
    public GameAdapterRegistration Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(GameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SaveVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(EvidenceReference);
        if (CanWrite && !CanRead)
            throw new ArgumentException("Write support requires verified read support.");
        if (!CanRead)
            throw new ArgumentException("A verified adapter must provide read support.", nameof(CanRead));
        return this;
    }
}
