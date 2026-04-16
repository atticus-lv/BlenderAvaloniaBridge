using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class FrameDispatchScheduler
{
    private readonly TimeSpan _activeFrameInterval;
    private readonly TimeSpan _idleHeartbeatInterval;
    private DateTimeOffset _lastFrameSentAt = DateTimeOffset.MinValue;
    private DateTimeOffset _nextFrameAt = DateTimeOffset.MinValue;
    private bool _pendingFrame;

    public FrameDispatchScheduler(TimeSpan activeFrameInterval, TimeSpan idleHeartbeatInterval)
    {
        _activeFrameInterval = activeFrameInterval;
        _idleHeartbeatInterval = idleHeartbeatInterval;
    }

    public void NotifyMessageApplied(ProtocolEnvelope envelope, DateTimeOffset now)
    {
        if (!RequiresFrame(envelope))
        {
            return;
        }

        _pendingFrame = true;
        var earliest = _lastFrameSentAt == DateTimeOffset.MinValue
            ? now
            : _lastFrameSentAt + _activeFrameInterval;

        if (_nextFrameAt == DateTimeOffset.MinValue || earliest < _nextFrameAt)
        {
            _nextFrameAt = earliest;
        }
    }

    public void NotifyUiInvalidated(DateTimeOffset now)
    {
        _pendingFrame = true;
        var earliest = _lastFrameSentAt == DateTimeOffset.MinValue
            ? now
            : _lastFrameSentAt + _activeFrameInterval;

        if (_nextFrameAt == DateTimeOffset.MinValue || earliest < _nextFrameAt)
        {
            _nextFrameAt = earliest;
        }
    }

    public TimeSpan? GetDelayUntilNextFrame(DateTimeOffset now)
    {
        if (_pendingFrame)
        {
            var delay = _nextFrameAt - now;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        if (_lastFrameSentAt == DateTimeOffset.MinValue || _idleHeartbeatInterval <= TimeSpan.Zero)
        {
            return null;
        }

        var heartbeatDueAt = _lastFrameSentAt + _idleHeartbeatInterval;
        var heartbeatDelay = heartbeatDueAt - now;
        return heartbeatDelay > TimeSpan.Zero ? heartbeatDelay : TimeSpan.Zero;
    }

    public bool IsFrameDue(DateTimeOffset now)
    {
        if (_pendingFrame)
        {
            return now >= _nextFrameAt;
        }

        return _lastFrameSentAt != DateTimeOffset.MinValue
            && _idleHeartbeatInterval > TimeSpan.Zero
            && now >= _lastFrameSentAt + _idleHeartbeatInterval;
    }

    public void MarkFrameSent(DateTimeOffset now)
    {
        _pendingFrame = false;
        _nextFrameAt = DateTimeOffset.MinValue;
        _lastFrameSentAt = now;
    }

    public void DeferPendingFrame(DateTimeOffset now, TimeSpan retryDelay)
    {
        _pendingFrame = true;
        _nextFrameAt = now + (retryDelay > TimeSpan.Zero ? retryDelay : TimeSpan.Zero);
    }

    private static bool RequiresFrame(ProtocolEnvelope envelope)
    {
        return !string.Equals(envelope.Type, "focus", StringComparison.OrdinalIgnoreCase) || envelope.Focus == true;
    }
}
