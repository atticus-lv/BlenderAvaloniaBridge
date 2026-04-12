using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderBridgeDiagnosticsSnapshotTests
{
    [Fact]
    public void ToJson_UsesExpectedSnakeCaseFieldNames()
    {
        var snapshot = new BlenderBridgeDiagnosticsSnapshot
        {
            UptimeSeconds = 12.3,
            Fps = 27.6,
            FrameCadenceMs = 36.2,
            LastFrameSeq = 686,
            LastInputType = "pointer_move",
            InputToNextFrameMs = 7.6,
            InputToApplyMs = 0.0,
            CaptureToBlenderReceiveMs = 2.1,
            CaptureFrameMs = 1.99,
            ConvertMs = 2.10,
            GpuUploadMs = 1.59,
            OverlayDrawMs = 1.74,
            PointerMoveDropPct = 64.0,
        };

        var json = snapshot.ToJson(indented: true);

        Assert.Contains("\"uptime_s\"", json);
        Assert.Contains("\"fps\"", json);
        Assert.Contains("\"frame_cadence_ms\"", json);
        Assert.Contains("\"last_frame_seq\"", json);
        Assert.Contains("\"pointer_move_drop_pct\"", json);
    }
}
