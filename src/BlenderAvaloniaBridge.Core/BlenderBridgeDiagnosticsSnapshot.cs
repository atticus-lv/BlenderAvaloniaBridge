using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge;

public sealed class BlenderBridgeDiagnosticsSnapshot
{
    [JsonPropertyName("uptime_s")]
    public double? UptimeSeconds { get; set; }

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("frame_cadence_ms")]
    public double? FrameCadenceMs { get; set; }

    [JsonPropertyName("last_frame_seq")]
    public int LastFrameSeq { get; set; }

    [JsonPropertyName("last_input_type")]
    public string LastInputType { get; set; } = string.Empty;

    [JsonPropertyName("input_to_next_frame_ms")]
    public double? InputToNextFrameMs { get; set; }

    [JsonPropertyName("input_to_apply_ms")]
    public double? InputToApplyMs { get; set; }

    [JsonPropertyName("capture_to_blender_recv_ms")]
    public double? CaptureToBlenderReceiveMs { get; set; }

    [JsonPropertyName("capture_frame_ms")]
    public double? CaptureFrameMs { get; set; }

    [JsonPropertyName("convert_ms")]
    public double? ConvertMs { get; set; }

    [JsonPropertyName("gpu_upload_ms")]
    public double? GpuUploadMs { get; set; }

    [JsonPropertyName("overlay_draw_ms")]
    public double? OverlayDrawMs { get; set; }

    [JsonPropertyName("pointer_move_drop_pct")]
    public double? PointerMoveDropPct { get; set; }

    public string ToJson(bool indented = false)
    {
        var typeInfo = indented
            ? BlenderBridgeIndentedJsonContext.Default.BlenderBridgeDiagnosticsSnapshot
            : BlenderBridgeJsonContext.Default.BlenderBridgeDiagnosticsSnapshot;
        return JsonSerializer.Serialize(this, typeInfo);
    }
}
