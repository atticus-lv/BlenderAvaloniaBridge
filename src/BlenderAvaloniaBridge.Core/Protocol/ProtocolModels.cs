using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge.Protocol;

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

    [JsonPropertyName("window_mode")]
    public string? WindowMode { get; set; }

    [JsonPropertyName("supports_business")]
    public bool? SupportsBusiness { get; set; }

    [JsonPropertyName("supports_frames")]
    public bool? SupportsFrames { get; set; }

    [JsonPropertyName("supports_input")]
    public bool? SupportsInput { get; set; }

    [JsonPropertyName("protocolVersion")]
    public int? ProtocolVersion { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; set; }

    [JsonPropertyName("message_id")]
    public long? MessageId { get; set; }

    [JsonPropertyName("reply_to")]
    public long? ReplyTo { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    [JsonPropertyName("error")]
    public BusinessError? Error { get; set; }

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

    [JsonPropertyName("shm_name")]
    public string? SharedMemoryName { get; set; }

    [JsonPropertyName("frame_size")]
    public int? FrameSize { get; set; }

    [JsonPropertyName("slot_count")]
    public int? SlotCount { get; set; }

    [JsonPropertyName("slot")]
    public int? Slot { get; set; }

    [JsonPropertyName("captured_at_unix_ms")]
    public long? CapturedAtUnixMs { get; set; }

    [JsonPropertyName("sent_at_unix_ms")]
    public long? SentAtUnixMs { get; set; }

    [JsonPropertyName("input_applied_at_unix_ms")]
    public long? InputAppliedAtUnixMs { get; set; }

    [JsonPropertyName("capture_started_at_unix_ms")]
    public long? CaptureStartedAtUnixMs { get; set; }

    [JsonPropertyName("ui_apply_ms")]
    public double? UiApplyMs { get; set; }

    [JsonPropertyName("capture_frame_ms")]
    public double? CaptureFrameMs { get; set; }

    [JsonPropertyName("copy_bgra_ms")]
    public double? CopyBgraMs { get; set; }

    [JsonPropertyName("linear_convert_ms")]
    public double? LinearConvertMs { get; set; }

    [JsonPropertyName("shared_write_ms")]
    public double? SharedWriteMs { get; set; }

    [JsonPropertyName("frame_send_ms")]
    public double? FrameSendMs { get; set; }

    [JsonPropertyName("ok")]
    public bool? Ok { get; set; }
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
