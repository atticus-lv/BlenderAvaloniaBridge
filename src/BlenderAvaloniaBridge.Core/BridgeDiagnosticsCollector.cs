using System.Collections.Generic;
using BlenderAvaloniaBridge.Runtime;

namespace BlenderAvaloniaBridge;

internal sealed class BridgeDiagnosticsCollector
{
    private readonly Queue<double> _frameIntervalsMs = new();
    private readonly Queue<double> _captureFrameMs = new();
    private readonly Queue<double> _convertMs = new();
    private readonly Queue<double> _inputApplyMs = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly object _sync = new();

    private DateTimeOffset? _lastFrameAt;
    private DateTimeOffset? _lastInputAt;
    private string _lastInputType = string.Empty;
    private int _lastFrameSeq;

    public void RecordInput(string? inputType, double? inputApplyMs)
    {
        lock (_sync)
        {
            _lastInputAt = DateTimeOffset.UtcNow;
            _lastInputType = inputType ?? string.Empty;
            if (inputApplyMs.HasValue)
            {
                AppendSample(_inputApplyMs, inputApplyMs.Value);
            }
        }
    }

    public void RecordFrame(FrameCaptureResult frameResult, double? convertMs)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_lastFrameAt.HasValue)
            {
                AppendSample(_frameIntervalsMs, (now - _lastFrameAt.Value).TotalMilliseconds);
            }

            _lastFrameAt = now;
            _lastFrameSeq = frameResult.FramePacket.Header.Seq;
            AppendSample(_captureFrameMs, frameResult.Metrics.CaptureFrameMs);
            if (convertMs.HasValue)
            {
                AppendSample(_convertMs, convertMs.Value);
            }
        }
    }

    public BlenderBridgeDiagnosticsSnapshot CreateSnapshot()
    {
        lock (_sync)
        {
            var cadence = Average(_frameIntervalsMs);
            return new BlenderBridgeDiagnosticsSnapshot
            {
                UptimeSeconds = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds,
                Fps = cadence.HasValue && cadence.Value > 0 ? 1000.0 / cadence.Value : null,
                FrameCadenceMs = cadence,
                LastFrameSeq = _lastFrameSeq,
                LastInputType = string.IsNullOrWhiteSpace(_lastInputType) ? "-" : _lastInputType,
                InputToNextFrameMs = _lastInputAt.HasValue && _lastFrameAt.HasValue
                    ? Math.Max(0.0, (_lastFrameAt.Value - _lastInputAt.Value).TotalMilliseconds)
                    : null,
                InputToApplyMs = Average(_inputApplyMs),
                CaptureToBlenderReceiveMs = null,
                CaptureFrameMs = Average(_captureFrameMs),
                ConvertMs = Average(_convertMs),
                GpuUploadMs = null,
                OverlayDrawMs = null,
                PointerMoveDropPct = 0.0,
            };
        }
    }

    private static void AppendSample(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > 60)
        {
            queue.Dequeue();
        }
    }

    private static double? Average(IEnumerable<double> values)
    {
        double total = 0.0;
        var count = 0;
        foreach (var value in values)
        {
            total += value;
            count++;
        }

        return count > 0 ? total / count : null;
    }
}
