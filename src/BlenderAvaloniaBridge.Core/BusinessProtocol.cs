using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge;

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

    public BusinessRequest(string name, JsonElement payload, int businessVersion = 1, long messageId = 0)
    {
        Name = name;
        Payload = payload.Clone();
        BusinessVersion = businessVersion;
        MessageId = messageId;
    }

    [JsonPropertyName("business_version")]
    public int BusinessVersion { get; set; } = 1;

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
        int businessVersion = 1,
        long messageId = 0)
    {
        return new BusinessResponse
        {
            BusinessVersion = businessVersion,
            MessageId = messageId,
            ReplyTo = replyTo,
            Ok = ok,
            Payload = payload?.Clone(),
            Error = error,
        };
    }
}

public sealed class BusinessResponse
{
    [JsonPropertyName("business_version")]
    public int BusinessVersion { get; set; } = 1;

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
}

public interface IBusinessEndpoint
{
    ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default);
}

public interface IBusinessEndpointSink
{
    void AttachBusinessEndpoint(IBusinessEndpoint? businessEndpoint);
}
