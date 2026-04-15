using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal interface IBridgeUiSession
    : IDisposable
{
    bool SupportsFrames { get; }

    bool SupportsInput { get; }

    event Action? FrameRequested;

    Task InitializeAsync();

    Task AttachBusinessApiAsync(IBusinessEndpoint businessEndpoint, BlenderApi blenderApi);

    Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope);

    Task NotifyBusinessUiActivityAsync();

    Task SetWatchRenderingActiveAsync(bool isActive);

    Task<FrameCaptureResult> CaptureFrameAsync(int seq);

    ProtocolPacket CreateInitAck(int seq);
}
