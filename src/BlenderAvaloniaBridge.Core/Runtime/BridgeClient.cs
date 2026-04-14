using System.Runtime.Versioning;
using System.Diagnostics;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Transport;

namespace BlenderAvaloniaBridge.Runtime;

[SupportedOSPlatform("windows")]
internal sealed class BridgeClient
{
    private readonly BlenderBridgeOptions _options;
    private readonly LengthPrefixedConnection _connection;
    private readonly BridgeDiagnosticsCollector? _diagnostics;
    private readonly IBridgeUiSession _uiSession;
    private readonly FrameDispatchScheduler _frameScheduler;
    private readonly RemoteBusinessEndpoint _businessEndpoint;
    private readonly SemaphoreSlim _frameSignal = new(0, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SharedMemoryFrameWriter? _sharedMemoryWriter;
    private int _sharedFrameSize;
    private int _sharedSlotCount;
    private int _sequence;
    private double? _lastUiApplyMs;
    private long? _lastInputAppliedAtUnixMs;

    internal BridgeClient(
        LengthPrefixedConnection connection,
        IBridgeUiSession uiSession,
        BlenderBridgeOptions options,
        BridgeDiagnosticsCollector? diagnostics = null)
    {
        _connection = connection;
        _uiSession = uiSession;
        _options = options.Clone();
        _diagnostics = diagnostics;
        _frameScheduler = new FrameDispatchScheduler(_options.ActiveFrameInterval, _options.IdleHeartbeatInterval);
        _businessEndpoint = new RemoteBusinessEndpoint(WriteBusinessEnvelopeAsync);
        if (_uiSession.SupportsFrames)
        {
            _uiSession.FrameRequested += OnUiFrameRequested;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var packet = await _connection.ReadAsync(cancellationToken)
                ?? throw new InvalidOperationException("Bridge closed before init packet was received.");

            if (!string.Equals(packet.Header.Type, "init", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected init packet, got {packet.Header.Type}.");
            }

            await _uiSession.InitializeAsync();
            await _uiSession.DeliverBridgeMessageAsync(packet.Header);

            var canStreamFrames = _uiSession.SupportsFrames && _options.SupportsFrames;
            ConfigureSharedMemory(packet.Header, canStreamFrames);

            await WritePacketAsync(_uiSession.CreateInitAck(NextSequence()), cancellationToken);
            if (_options.SupportsBusiness)
            {
                await _uiSession.AttachBusinessEndpointAsync(_businessEndpoint);
            }
            if (canStreamFrames)
            {
                await WriteFrameAsync(await _uiSession.CaptureFrameAsync(NextSequence()), cancellationToken);
                _frameScheduler.MarkFrameSent(DateTimeOffset.UtcNow);
            }

            if (!canStreamFrames)
            {
                await RunMessageLoopWithoutFramesAsync(cancellationToken);
                return;
            }

            while (true)
            {
                var delay = _frameScheduler.GetDelayUntilNextFrame(DateTimeOffset.UtcNow);
                if (delay is TimeSpan wait)
                {
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var readTask = _connection.ReadAsync(readCts.Token);
                    var signalTask = _frameSignal.WaitAsync(cancellationToken);
                    var timerTask = Task.Delay(wait, cancellationToken);
                    var completed = await Task.WhenAny(readTask, signalTask, timerTask);

                    if (completed == timerTask)
                    {
                        readCts.Cancel();
                        await IgnoreCanceledReadAsync(readTask);
                        await WriteFrameAsync(await _uiSession.CaptureFrameAsync(NextSequence()), cancellationToken);
                        _frameScheduler.MarkFrameSent(DateTimeOffset.UtcNow);
                        continue;
                    }

                    if (completed == signalTask)
                    {
                        readCts.Cancel();
                        await IgnoreCanceledReadAsync(readTask);
                        continue;
                    }

                    var scheduled = await readTask;
                    if (scheduled is null)
                    {
                        break;
                    }

                    await ApplyEnvelopeAsync(scheduled.Header);
                    _frameScheduler.NotifyMessageApplied(scheduled.Header, DateTimeOffset.UtcNow);
                    continue;
                }

                var nextReadTask = _connection.ReadAsync(cancellationToken);
                var nextSignalTask = _frameSignal.WaitAsync(cancellationToken);
                var idleCompleted = await Task.WhenAny(nextReadTask, nextSignalTask);

                if (idleCompleted == nextSignalTask)
                {
                    continue;
                }

                var next = await nextReadTask;
                if (next is null)
                {
                    break;
                }

                await ApplyEnvelopeAsync(next.Header);
                _frameScheduler.NotifyMessageApplied(next.Header, DateTimeOffset.UtcNow);

                if (_frameScheduler.IsFrameDue(DateTimeOffset.UtcNow))
                {
                    await WriteFrameAsync(await _uiSession.CaptureFrameAsync(NextSequence()), cancellationToken);
                    _frameScheduler.MarkFrameSent(DateTimeOffset.UtcNow);
                }
            }
        }
        finally
        {
            if (_uiSession.SupportsFrames)
            {
                _uiSession.FrameRequested -= OnUiFrameRequested;
            }
            _sharedMemoryWriter?.Dispose();
            _sharedMemoryWriter = null;
        }
    }

    private void ConfigureSharedMemory(ProtocolEnvelope envelope, bool canStreamFrames)
    {
        _sharedMemoryWriter?.Dispose();
        _sharedMemoryWriter = null;
        _sharedFrameSize = 0;
        _sharedSlotCount = 0;

        if (!canStreamFrames || !_options.UseSharedMemory || string.IsNullOrWhiteSpace(envelope.SharedMemoryName) || !envelope.FrameSize.HasValue)
        {
            return;
        }

        _sharedFrameSize = envelope.FrameSize.Value;
        _sharedSlotCount = envelope.SlotCount.GetValueOrDefault(2);
        _sharedMemoryWriter = new SharedMemoryFrameWriter(envelope.SharedMemoryName, _sharedFrameSize, _sharedSlotCount);
    }

    private async Task ApplyEnvelopeAsync(ProtocolEnvelope envelope)
    {
        if (IsBusinessResponse(envelope))
        {
            _businessEndpoint.HandleResponse(envelope);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await _uiSession.DeliverBridgeMessageAsync(envelope);
        stopwatch.Stop();

        if (RequiresFrameForEnvelope(envelope))
        {
            _lastUiApplyMs = stopwatch.Elapsed.TotalMilliseconds;
            _lastInputAppliedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _diagnostics?.RecordInput(envelope.Type, _lastUiApplyMs);
        }
    }

    private async Task WriteFrameAsync(FrameCaptureResult frameResult, CancellationToken cancellationToken)
    {
        var framePacket = frameResult.FramePacket;
        var linearConvertMs = 0.0;
        var sharedWriteMs = 0.0;

        if (_sharedMemoryWriter is null)
        {
            framePacket.Header.UiApplyMs = _lastUiApplyMs;
            framePacket.Header.InputAppliedAtUnixMs = _lastInputAppliedAtUnixMs;
            framePacket.Header.CaptureStartedAtUnixMs = frameResult.Metrics.CaptureStartedAtUnixMs;
            framePacket.Header.CapturedAtUnixMs = frameResult.Metrics.CapturedAtUnixMs;
            framePacket.Header.CaptureFrameMs = frameResult.Metrics.CaptureFrameMs;
            framePacket.Header.CopyBgraMs = frameResult.Metrics.CopyBgraMs;

            var sendStopwatch = Stopwatch.StartNew();
            await WritePacketAsync(framePacket, cancellationToken);
            sendStopwatch.Stop();
            framePacket.Header.FrameSendMs = sendStopwatch.Elapsed.TotalMilliseconds;
            _diagnostics?.RecordFrame(frameResult, framePacket.Header.CopyBgraMs);
            ClearPendingInputMetrics();
            return;
        }

        var writeStopwatch = Stopwatch.StartNew();
        var slot = _sharedMemoryWriter.WriteLinearRgbaFrameFromRgba(frameResult.RawRgbaPayload, framePacket.Header.Seq);
        writeStopwatch.Stop();
        linearConvertMs = writeStopwatch.Elapsed.TotalMilliseconds;
        sharedWriteMs = 0.0;

        var readyHeader = new ProtocolEnvelope
        {
            Type = "frame_ready",
            Seq = framePacket.Header.Seq,
            Width = framePacket.Header.Width,
            Height = framePacket.Header.Height,
            PixelFormat = "rgba32f_linear",
            Stride = framePacket.Header.Width * 16,
            FrameSize = _sharedFrameSize,
            SlotCount = _sharedSlotCount,
            Slot = slot,
            CapturedAtUnixMs = framePacket.Header.CapturedAtUnixMs,
            CaptureStartedAtUnixMs = frameResult.Metrics.CaptureStartedAtUnixMs,
            InputAppliedAtUnixMs = _lastInputAppliedAtUnixMs,
            UiApplyMs = _lastUiApplyMs,
            CaptureFrameMs = frameResult.Metrics.CaptureFrameMs,
            CopyBgraMs = frameResult.Metrics.CopyBgraMs,
            LinearConvertMs = linearConvertMs,
            SharedWriteMs = sharedWriteMs,
            Message = "shared-memory-frame-ready"
        };

        var readySendStopwatch = Stopwatch.StartNew();
        readyHeader.SentAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await WritePacketAsync(ProtocolPacket.CreateControl(readyHeader), cancellationToken);
        readySendStopwatch.Stop();
        readyHeader.FrameSendMs = readySendStopwatch.Elapsed.TotalMilliseconds;
        _diagnostics?.RecordFrame(frameResult, linearConvertMs);
        ClearPendingInputMetrics();
    }

    private static async Task IgnoreCanceledReadAsync(Task<ProtocolPacket?> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearPendingInputMetrics()
    {
        _lastUiApplyMs = null;
        _lastInputAppliedAtUnixMs = null;
    }

    private static bool RequiresFrameForEnvelope(ProtocolEnvelope envelope)
    {
        return envelope.Type is "pointer_move" or "pointer_down" or "pointer_up" or "wheel" or "key_down" or "key_up" or "text" or "resize"
            || (string.Equals(envelope.Type, "focus", StringComparison.OrdinalIgnoreCase) && envelope.Focus == true);
    }

    private void OnUiFrameRequested()
    {
        _frameScheduler.NotifyUiInvalidated(DateTimeOffset.UtcNow);
        if (_frameSignal.CurrentCount == 0)
        {
            _frameSignal.Release();
        }
    }

    private Task WriteBusinessEnvelopeAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
    {
        return WritePacketAsync(ProtocolPacket.CreateControl(envelope), cancellationToken);
    }

    private async Task WritePacketAsync(ProtocolPacket packet, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _connection.WriteAsync(packet, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private int NextSequence()
    {
        return Interlocked.Increment(ref _sequence);
    }

    private static bool IsBusinessResponse(ProtocolEnvelope envelope)
    {
        return envelope.Type == "business_response";
    }

    private async Task RunMessageLoopWithoutFramesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = await _connection.ReadAsync(cancellationToken);
            if (packet is null)
            {
                break;
            }

            await ApplyEnvelopeAsync(packet.Header);
        }
    }
}
