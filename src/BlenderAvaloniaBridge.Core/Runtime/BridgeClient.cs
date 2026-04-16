using System.Diagnostics;
using System.Net.Sockets;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Transport;
using BlenderAvaloniaBridge.Runtime.FrameTransport;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class BridgeClient
{
    private readonly BlenderBridgeOptions _options;
    private readonly LengthPrefixedConnection _connection;
    private readonly BridgeDiagnosticsCollector? _diagnostics;
    private readonly IBridgeUiSession _uiSession;
    private readonly ISharedFrameWriterFactory _sharedFrameWriterFactory;
    private readonly FrameDispatchScheduler _frameScheduler;
    private readonly RemoteBusinessEndpoint _businessEndpoint;
    private readonly BlenderApi _blenderApi;
    private readonly LatestWinsSignal _frameSignal = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _frameSchedulerGate = new();
    private ISharedFrameWriter? _sharedMemoryWriter;
    private int _sharedFrameSize;
    private int _sharedSlotCount;
    private int _sequence;
    private double? _lastUiApplyMs;
    private long? _lastInputAppliedAtUnixMs;
    private volatile bool _hasActiveWatches;

    internal BridgeClient(
        LengthPrefixedConnection connection,
        IBridgeUiSession uiSession,
        BlenderBridgeOptions options,
        BridgeDiagnosticsCollector? diagnostics = null,
        ISharedFrameWriterFactory? sharedFrameWriterFactory = null)
    {
        _connection = connection;
        _uiSession = uiSession;
        _options = options.Clone();
        _diagnostics = diagnostics;
        _sharedFrameWriterFactory = sharedFrameWriterFactory ?? SharedFrameWriterFactory.Instance;
        _frameScheduler = new FrameDispatchScheduler(_options.ActiveFrameInterval, _options.IdleHeartbeatInterval);
        _businessEndpoint = new RemoteBusinessEndpoint(WriteBusinessPacketAsync);
        _blenderApi = new BlenderApi(_businessEndpoint, _options.Api);
        if (_uiSession.SupportsFrames)
        {
            _uiSession.FrameRequested += OnUiFrameRequested;
        }

        ((IWatchActivitySource)_blenderApi).ActiveWatchStateChanged += OnActiveWatchStateChanged;
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
                await _uiSession.AttachBusinessApiAsync(_businessEndpoint, _blenderApi);
            }
            if (canStreamFrames)
            {
                if (await TryWriteCurrentFrameAsync(cancellationToken))
                {
                    _frameScheduler.MarkFrameSent(DateTimeOffset.UtcNow);
                }
            }

            if (!canStreamFrames)
            {
                await RunMessageLoopWithoutFramesAsync(cancellationToken);
                return;
            }

            using var frameLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var frameLoopTask = RunFrameLoopAsync(frameLoopCts.Token);
            try
            {
                await RunMessageLoopWithFramesAsync(cancellationToken);
            }
            finally
            {
                frameLoopCts.Cancel();
                await IgnoreCanceledTaskAsync(frameLoopTask);
            }
        }
        catch (Exception ex) when (IsTransportDisconnect(ex))
        {
            // Blender owns the transport lifetime. If the host closes the socket while the bridge
            // is still flushing frames or business traffic, treat that as a normal shutdown.
        }
        finally
        {
            ((IWatchActivitySource)_blenderApi).ActiveWatchStateChanged -= OnActiveWatchStateChanged;
            try
            {
                await _uiSession.SetWatchRenderingActiveAsync(false);
            }
            catch (ObjectDisposedException)
            {
            }
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
        _sharedMemoryWriter = _sharedFrameWriterFactory.Create(envelope.SharedMemoryName, _sharedFrameSize, _sharedSlotCount);
    }

    private async Task ApplyPacketAsync(ProtocolPacket packet)
    {
        var envelope = packet.Header;
        if (IsBusinessResponse(envelope))
        {
            _businessEndpoint.HandleResponse(envelope, packet.Payload);
            await _uiSession.NotifyBusinessUiActivityAsync();
            NotifyBusinessUiActivity();
            return;
        }

        if (IsBusinessEvent(envelope))
        {
            await ((IBusinessEventSink)_blenderApi).HandleEventAsync(
                new BusinessEvent
                {
                    ProtocolVersion = envelope.ProtocolVersion ?? BlenderBusinessProtocolVersions.ProtocolVersion,
                    SchemaVersion = envelope.SchemaVersion ?? BlenderBusinessProtocolVersions.SchemaVersion,
                    Name = envelope.Name ?? string.Empty,
                    Payload = envelope.Payload?.Clone(),
                });
            await _uiSession.NotifyBusinessUiActivityAsync();
            NotifyBusinessUiActivity();
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

    private static async Task IgnoreCanceledTaskAsync(Task task)
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
        NotifyUiInvalidated();
    }

    private void NotifyBusinessUiActivity()
    {
        if (!_uiSession.SupportsFrames)
        {
            return;
        }

        NotifyUiInvalidated();
    }

    private void OnActiveWatchStateChanged(bool hasActiveWatches)
    {
        if (!_uiSession.SupportsFrames)
        {
            return;
        }

        _hasActiveWatches = hasActiveWatches;
        _ = _uiSession.SetWatchRenderingActiveAsync(hasActiveWatches);
        if (hasActiveWatches)
        {
            NotifyBusinessUiActivity();
        }
    }

    private Task WriteBusinessPacketAsync(ProtocolPacket packet, CancellationToken cancellationToken)
    {
        return WritePacketAsync(packet, cancellationToken);
    }

    private async Task RunMessageLoopWithFramesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = await _connection.ReadAsync(cancellationToken);
            if (packet is null)
            {
                break;
            }

            await ApplyPacketAsync(packet);
            lock (_frameSchedulerGate)
            {
                _frameScheduler.NotifyMessageApplied(packet.Header, DateTimeOffset.UtcNow);
            }

            SignalFrameLoop();
        }
    }

    private async Task RunFrameLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan? delay;
            var scheduledByActiveWatchClock = false;
            lock (_frameSchedulerGate)
            {
                delay = _frameScheduler.GetDelayUntilNextFrame(DateTimeOffset.UtcNow);
            }

            if (_hasActiveWatches && delay is null)
            {
                delay = _options.ActiveFrameInterval;
                scheduledByActiveWatchClock = true;
            }

            if (delay is null)
            {
                await _frameSignal.WaitAsync(cancellationToken);
                continue;
            }

            if (delay.Value > TimeSpan.Zero)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var signalTask = _frameSignal.WaitAsync(waitCts.Token).AsTask();
                var timerTask = Task.Delay(delay.Value, waitCts.Token);
                var completed = await Task.WhenAny(signalTask, timerTask);
                waitCts.Cancel();
                await IgnoreCanceledTaskAsync(signalTask);
                await IgnoreCanceledTaskAsync(timerTask);

                if (completed == signalTask)
                {
                    continue;
                }
            }

            if (_hasActiveWatches)
            {
                await _uiSession.NotifyBusinessUiActivityAsync();
            }

            var now = DateTimeOffset.UtcNow;
            if (!scheduledByActiveWatchClock)
            {
                lock (_frameSchedulerGate)
                {
                    if (!_frameScheduler.IsFrameDue(now))
                    {
                        continue;
                    }
                }
            }

            if (!await TryWriteCurrentFrameAsync(cancellationToken))
            {
                lock (_frameSchedulerGate)
                {
                    _frameScheduler.DeferPendingFrame(DateTimeOffset.UtcNow, GetRecoverableFrameRetryDelay());
                }

                continue;
            }

            lock (_frameSchedulerGate)
            {
                _frameScheduler.MarkFrameSent(DateTimeOffset.UtcNow);
            }
        }
    }

    private void NotifyUiInvalidated()
    {
        lock (_frameSchedulerGate)
        {
            _frameScheduler.NotifyUiInvalidated(DateTimeOffset.UtcNow);
        }

        SignalFrameLoop();
    }

    private void SignalFrameLoop()
    {
        _frameSignal.Signal();
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

    private async Task<bool> TryWriteCurrentFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WriteFrameAsync(await _uiSession.CaptureFrameAsync(NextSequence()), cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsRecoverableFrameFailure(ex))
        {
            Trace.WriteLine($"Bridge frame capture skipped after recoverable failure: {ex}");
            return false;
        }
    }

    private TimeSpan GetRecoverableFrameRetryDelay()
    {
        return _options.ActiveFrameInterval > TimeSpan.Zero
            ? _options.ActiveFrameInterval
            : TimeSpan.FromMilliseconds(16);
    }

    private static bool IsBusinessResponse(ProtocolEnvelope envelope)
    {
        return envelope.Type == "business_response";
    }

    private static bool IsBusinessEvent(ProtocolEnvelope envelope)
    {
        return envelope.Type == "business_event";
    }

    internal static bool IsTransportDisconnect(Exception exception)
    {
        return exception switch
        {
            EndOfStreamException => true,
            ObjectDisposedException => true,
            IOException ioException when ioException.InnerException is SocketException socketException
                => IsSocketDisconnect(socketException.SocketErrorCode),
            SocketException socketException => IsSocketDisconnect(socketException.SocketErrorCode),
            AggregateException aggregateException when aggregateException.InnerExceptions.Count == 1
                => IsTransportDisconnect(aggregateException.InnerException!),
            _ => false,
        };
    }

    internal static bool IsRecoverableFrameFailure(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException invalidOperationException
                when invalidOperationException.Message.Contains("Headless renderer did not produce a frame.", StringComparison.Ordinal)
                => true,
            InvalidOperationException invalidOperationException
                when invalidOperationException.Message.Contains("Headless window is not initialized.", StringComparison.Ordinal)
                => true,
            AggregateException aggregateException when aggregateException.InnerExceptions.Count == 1
                => IsRecoverableFrameFailure(aggregateException.InnerException!),
            _ => false,
        };
    }

    private static bool IsSocketDisconnect(SocketError socketError)
    {
        return socketError is SocketError.ConnectionAborted
            or SocketError.ConnectionReset
            or SocketError.Disconnecting
            or SocketError.Interrupted
            or SocketError.NotConnected
            or SocketError.OperationAborted
            or SocketError.Shutdown;
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

            await ApplyPacketAsync(packet);
        }
    }
}
