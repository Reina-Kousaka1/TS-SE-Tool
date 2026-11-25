namespace TSSE.Bridge.Protocol;

public sealed record BridgeResponse(
    int ProtocolVersion,
    string RequestId,
    string Status,
    string? ErrorCode,
    EngineStatus? Engine);

public sealed record EngineStatus(
    string ProcessState,
    string SaveSyntaxCapability,
    int VerifiedGameAdapterCount,
    bool SaveWriteEnabled);
