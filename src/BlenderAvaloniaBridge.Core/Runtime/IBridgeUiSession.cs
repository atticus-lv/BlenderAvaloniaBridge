using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal interface IBridgeUiSession
    : IDisposable
{
    bool SupportsFrames { get; }

    bool SupportsInput { get; }

    event Action? FrameRequested;

    Task InitializeAsync();

    Task AttachBusinessEndpointAsync(IBusinessEndpoint businessEndpoint);

    Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope);

    Task<FrameCaptureResult> CaptureFrameAsync(int seq);

    ProtocolPacket CreateInitAck(int seq);
}
