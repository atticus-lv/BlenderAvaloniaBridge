using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge;

public static class BlenderBusinessProtocolVersions
{
    public const int ProtocolVersion = 1;
    public const int SchemaVersion = 1;
}

public sealed class BusinessError
{
    public BusinessError()
    {
    }

    public BusinessError(string code, string message, JsonElement? details = null)
    {
        Code = code;
        Message = message;
        Details = details?.Clone();
    }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public JsonElement? Details { get; set; }
}

public sealed class BusinessRequest
{
    private static readonly JsonElement EmptyPayload = JsonDocument.Parse("{}").RootElement.Clone();

    public BusinessRequest()
    {
        Payload = EmptyPayload.Clone();
    }

    public BusinessRequest(
        string name,
        JsonElement payload,
        int protocolVersion = BlenderBusinessProtocolVersions.ProtocolVersion,
        int schemaVersion = BlenderBusinessProtocolVersions.SchemaVersion,
        long messageId = 0)
    {
        Name = name;
        Payload = payload.Clone();
        ProtocolVersion = protocolVersion;
        SchemaVersion = schemaVersion;
        MessageId = messageId;
    }

    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = BlenderBusinessProtocolVersions.ProtocolVersion;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = BlenderBusinessProtocolVersions.SchemaVersion;

    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    public static BusinessResponse Response(
        long replyTo,
        JsonElement? payload = null,
        bool ok = true,
        BusinessError? error = null,
        byte[]? rawPayload = null,
        int protocolVersion = BlenderBusinessProtocolVersions.ProtocolVersion,
        int schemaVersion = BlenderBusinessProtocolVersions.SchemaVersion,
        long messageId = 0)
    {
        return new BusinessResponse
        {
            ProtocolVersion = protocolVersion,
            SchemaVersion = schemaVersion,
            MessageId = messageId,
            ReplyTo = replyTo,
            Ok = ok,
            Payload = payload?.Clone(),
            Error = error,
            RawPayload = rawPayload ?? Array.Empty<byte>(),
        };
    }
}

public sealed class BusinessResponse
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = BlenderBusinessProtocolVersions.ProtocolVersion;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = BlenderBusinessProtocolVersions.SchemaVersion;

    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("reply_to")]
    public long ReplyTo { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    [JsonPropertyName("error")]
    public BusinessError? Error { get; set; }

    [JsonIgnore]
    public byte[] RawPayload { get; set; } = Array.Empty<byte>();
}

public sealed class BusinessEvent
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = BlenderBusinessProtocolVersions.ProtocolVersion;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = BlenderBusinessProtocolVersions.SchemaVersion;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}

public interface IBusinessEndpoint
{
    ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default);
}

public interface IBusinessEndpointSink
{
    void AttachBusinessEndpoint(IBusinessEndpoint? businessEndpoint);
}
