using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge.Bridge;

public sealed class ProtocolEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("seq")]
    public int Seq { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("pixel_format")]
    public string? PixelFormat { get; set; }

    [JsonPropertyName("stride")]
    public int? Stride { get; set; }

    [JsonPropertyName("payload_length")]
    public int PayloadLength { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("x")]
    public double? X { get; set; }

    [JsonPropertyName("y")]
    public double? Y { get; set; }

    [JsonPropertyName("delta_x")]
    public double? DeltaX { get; set; }

    [JsonPropertyName("delta_y")]
    public double? DeltaY { get; set; }

    [JsonPropertyName("button")]
    public string? Button { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("focus")]
    public bool? Focus { get; set; }

    [JsonPropertyName("modifiers")]
    public List<string>? Modifiers { get; set; }
}

public sealed record ProtocolPacket(ProtocolEnvelope Header, byte[] Payload)
{
    public static ProtocolPacket CreateControl(ProtocolEnvelope header)
    {
        header.PayloadLength = 0;
        return new ProtocolPacket(header, Array.Empty<byte>());
    }

    public static ProtocolPacket CreateFrame(ProtocolEnvelope header, byte[] payload)
    {
        header.PayloadLength = payload.Length;
        return new ProtocolPacket(header, payload);
    }
}
