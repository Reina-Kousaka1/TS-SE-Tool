namespace TSSE.GameAdapters.Compatibility;

public sealed record CompatibilityAssessment(
    string GameId,
    string SaveVersion,
    CompatibilityState State,
    string EvidenceReference)
{
    public bool CanRead => State is CompatibilityState.ReadOnlyVerified or CompatibilityState.ReadWriteVerified;
    public bool CanWrite => State == CompatibilityState.ReadWriteVerified;
}
