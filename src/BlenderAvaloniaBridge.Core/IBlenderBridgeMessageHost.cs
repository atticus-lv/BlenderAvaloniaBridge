using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge;

public interface IBlenderBridgeMessageHost
{
    void AttachBridgeClient(IBlenderBridgeClient? bridgeClient);

    Task HandleBridgeMessageAsync(ProtocolEnvelope envelope);
}
