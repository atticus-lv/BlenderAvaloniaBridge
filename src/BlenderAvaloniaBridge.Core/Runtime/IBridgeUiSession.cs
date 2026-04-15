using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal interface IBridgeUiSession
    : IDisposable
{
    bool SupportsFrames { get; }

    bool SupportsInput { get; }

    event Action? FrameRequested;

    Task InitializeAsync();

    Task AttachBusinessApiAsync(IBusinessEndpoint businessEndpoint, IBlenderDataApi blenderDataApi);

    Task DeliverBridgeMessageAsync(ProtocolEnvelope envelope);

    Task<FrameCaptureResult> CaptureFrameAsync(int seq);

    ProtocolPacket CreateInitAck(int seq);
}
