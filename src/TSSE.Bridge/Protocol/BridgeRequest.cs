namespace TSSE.Bridge.Protocol;

public sealed record BridgeRequest(int ProtocolVersion, string RequestId, string Method);
