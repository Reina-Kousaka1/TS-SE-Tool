namespace TSSE.GameAdapters.Compatibility;

/// <summary>
/// Exact-match support registry. It starts empty so unverified game/save versions remain unsupported.
/// </summary>
public sealed class GameAdapterRegistry
{
    private readonly Dictionary<(string GameId, string SaveVersion), GameAdapterRegistration> _registrations = new();

    public int RegistrationCount => _registrations.Count;

    public void RegisterVerified(GameAdapterRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();
        var key = (Normalize(registration.GameId), Normalize(registration.SaveVersion));
        if (!_registrations.TryAdd(key, registration))
            throw new InvalidOperationException("A verified adapter is already registered for this exact version.");
    }

    public CompatibilityAssessment Assess(string gameId, string saveVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveVersion);

        if (!_registrations.TryGetValue((Normalize(gameId), Normalize(saveVersion)), out GameAdapterRegistration? registration))
            return new CompatibilityAssessment(gameId, saveVersion, CompatibilityState.UnsupportedUnknown, "");

        CompatibilityState state = registration.CanWrite
            ? CompatibilityState.ReadWriteVerified
            : registration.CanRead
                ? CompatibilityState.ReadOnlyVerified
                : CompatibilityState.UnsupportedUnknown;

        return new CompatibilityAssessment(
            registration.GameId,
            registration.SaveVersion,
            state,
            registration.EvidenceReference);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
