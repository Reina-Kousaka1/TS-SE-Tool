using TSSE.GameAdapters.Compatibility;

namespace TSSE.Desktop.Services;

public sealed class FoundationStatusService(GameAdapterRegistry registry) : IFoundationStatusService
{
    public FoundationStatus GetCurrent() => new(
        "Foundation only",
        registry.RegistrationCount,
        CanReadGameSaves: false,
        CanWriteGameSaves: false);
}
