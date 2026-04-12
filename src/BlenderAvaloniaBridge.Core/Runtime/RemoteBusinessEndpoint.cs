using System.Collections.Concurrent;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class RemoteBusinessEndpoint : IBusinessEndpoint
{
    private readonly Func<ProtocolEnvelope, CancellationToken, Task> _sendAsync;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<BusinessResponse>> _pending = new();
    private long _nextMessageId;

    public RemoteBusinessEndpoint(Func<ProtocolEnvelope, CancellationToken, Task> sendAsync)
    {
        ArgumentNullException.ThrowIfNull(sendAsync);
        _sendAsync = sendAsync;
    }

    public async ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messageId = request.MessageId > 0 ? request.MessageId : Interlocked.Increment(ref _nextMessageId);
        var envelope = new ProtocolEnvelope
        {
            Type = "business_request",
            BusinessVersion = request.BusinessVersion,
            MessageId = messageId,
            Name = request.Name,
            Payload = request.Payload.Clone(),
        };

        var completion = new TaskCompletionSource<BusinessResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(messageId, completion))
        {
            throw new InvalidOperationException($"Duplicate pending business request id: {messageId}");
        }

        using var cancellationRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() =>
            {
                if (_pending.TryRemove(messageId, out var pending))
                {
                    pending.TrySetCanceled(cancellationToken);
                }
            })
            : default;

        try
        {
            await _sendAsync(envelope, cancellationToken);
        }
        catch
        {
            _pending.TryRemove(messageId, out _);
            throw;
        }

        return await completion.Task.WaitAsync(cancellationToken);
    }

    internal void HandleResponse(ProtocolEnvelope envelope)
    {
        if (envelope.ReplyTo is not long replyTo)
        {
            return;
        }

        if (!_pending.TryRemove(replyTo, out var completion))
        {
            return;
        }

        completion.TrySetResult(
            new BusinessResponse
            {
                BusinessVersion = envelope.BusinessVersion ?? 1,
                MessageId = envelope.MessageId ?? 0,
                ReplyTo = replyTo,
                Ok = envelope.Ok ?? false,
                Payload = envelope.Payload?.Clone(),
                Error = envelope.Error,
            });
    }
}
