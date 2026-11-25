namespace TSSE.Desktop.Services;

public sealed record FoundationStatus(
    string EngineState,
    int VerifiedAdapterCount,
    bool CanReadGameSaves,
    bool CanWriteGameSaves);
