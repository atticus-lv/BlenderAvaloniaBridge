using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Runtime;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class FrameDispatchSchedulerTests
{
    [Fact]
    public void PointerMoveWithinInterval_KeepsSingleFrameDeadline()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var start = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);
        scheduler.MarkFrameSent(start);

        scheduler.NotifyMessageApplied(new ProtocolEnvelope { Type = "pointer_move" }, start.AddMilliseconds(2));
        var firstDelay = scheduler.GetDelayUntilNextFrame(start.AddMilliseconds(2));

        scheduler.NotifyMessageApplied(new ProtocolEnvelope { Type = "pointer_move" }, start.AddMilliseconds(6));
        var secondDelay = scheduler.GetDelayUntilNextFrame(start.AddMilliseconds(6));

        Assert.Equal(TimeSpan.FromMilliseconds(14), firstDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(10), secondDelay);
    }

    [Fact]
    public void FocusFalse_DoesNotScheduleFrame()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var now = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);

        scheduler.NotifyMessageApplied(new ProtocolEnvelope { Type = "focus", Focus = false }, now);

        Assert.Null(scheduler.GetDelayUntilNextFrame(now));
        Assert.False(scheduler.IsFrameDue(now));
    }

    [Fact]
    public void InputAfterInterval_MakesFrameImmediatelyDue()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var start = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);
        scheduler.MarkFrameSent(start);

        var later = start.AddMilliseconds(20);
        scheduler.NotifyMessageApplied(new ProtocolEnvelope { Type = "text", Text = "a" }, later);

        Assert.Equal(TimeSpan.Zero, scheduler.GetDelayUntilNextFrame(later));
        Assert.True(scheduler.IsFrameDue(later));
    }

    [Fact]
    public void UiInvalidationWithoutInput_SchedulesFrame()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var start = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);
        scheduler.MarkFrameSent(start);

        var invalidateAt = start.AddMilliseconds(5);
        scheduler.NotifyUiInvalidated(invalidateAt);

        Assert.Equal(TimeSpan.FromMilliseconds(11), scheduler.GetDelayUntilNextFrame(invalidateAt));
        Assert.False(scheduler.IsFrameDue(invalidateAt));
        Assert.True(scheduler.IsFrameDue(start.AddMilliseconds(16)));
    }

    [Fact]
    public void IdleHeartbeat_SchedulesLowFrequencyFrameWhenNoChanges()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var start = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);
        scheduler.MarkFrameSent(start);

        Assert.Equal(TimeSpan.FromMilliseconds(240), scheduler.GetDelayUntilNextFrame(start.AddMilliseconds(10)));
        Assert.True(scheduler.IsFrameDue(start.AddMilliseconds(250)));
    }

    [Fact]
    public void DeferredPendingFrame_RetriesWithoutClearingPendingState()
    {
        var scheduler = new FrameDispatchScheduler(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(250));
        var start = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);
        scheduler.NotifyUiInvalidated(start);

        var retryAt = start.AddMilliseconds(5);
        scheduler.DeferPendingFrame(retryAt, TimeSpan.FromMilliseconds(16));

        Assert.Equal(TimeSpan.FromMilliseconds(16), scheduler.GetDelayUntilNextFrame(retryAt));
        Assert.False(scheduler.IsFrameDue(retryAt));
        Assert.True(scheduler.IsFrameDue(retryAt.AddMilliseconds(16)));
    }
}
