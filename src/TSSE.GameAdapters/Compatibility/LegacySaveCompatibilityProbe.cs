using System.Globalization;
using TSSE.Savegames.Syntax;

namespace TSSE.GameAdapters.Compatibility;

/// <summary>
/// In-memory diagnostic only. A version field and registry entry are not a
/// validated save or permission to access or modify game files.
/// </summary>
public static class LegacySaveCompatibilityProbe
{
    public static LegacySaveCompatibilityResult Inspect(SiiSyntaxDocument document, string gameId, GameAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(registry);
        LegacySaveVersionResult version = LegacySaveVersionInspector.Inspect(document);
        if (version.Status != LegacySaveVersionStatus.LegacyFieldFound || version.Version is null)
            return new(version, null);
        string key = version.Version.Value.ToString(CultureInfo.InvariantCulture);
        return new(version, registry.Assess(gameId, key));
    }
}

/// <summary>Diagnostic evidence only; neither property authorizes game-save access.</summary>
public sealed record LegacySaveCompatibilityResult(LegacySaveVersionResult Version, CompatibilityAssessment? RegistryAssessment)
{
    public bool CanReadGameSave => false;
    public bool CanWriteGameSave => false;
}
