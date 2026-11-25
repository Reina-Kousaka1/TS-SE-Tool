using System.Text.Json;
using TSSE.GameAdapters.Compatibility;

namespace TSSE.Bridge.Protocol;

public static class BridgeProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumRequestCharacters = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string HandleLine(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine) || jsonLine.Length > MaximumRequestCharacters)
            return Error(string.Empty, "request_size_invalid");

        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(jsonLine, JsonOptions);
        }
        catch (JsonException)
        {
            return Error(string.Empty, "invalid_json");
        }

        if (request is null || !IsValidRequestId(request.RequestId))
            return Error(string.Empty, "request_invalid");
        if (request.ProtocolVersion != CurrentVersion)
            return Error(request.RequestId, "protocol_version_unsupported");
        if (!string.Equals(request.Method, "engine.status", StringComparison.Ordinal))
            return Error(request.RequestId, "method_not_supported");

        var adapters = new GameAdapterRegistry();
        var status = new EngineStatus(
            ProcessState: "foundation-only",
            SaveSyntaxCapability: "lossless-syntax-foundation",
            VerifiedGameAdapterCount: adapters.RegistrationCount,
            SaveWriteEnabled: false);

        return Serialize(new BridgeResponse(CurrentVersion, request.RequestId, "ok", null, status));
    }

    public static string Error(string requestId, string errorCode) =>
        Serialize(new BridgeResponse(CurrentVersion, requestId, "error", errorCode, null));

    private static bool IsValidRequestId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string Serialize(BridgeResponse response) => JsonSerializer.Serialize(response, JsonOptions);
}
