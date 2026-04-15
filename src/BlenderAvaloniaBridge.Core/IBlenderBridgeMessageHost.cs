using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge;

public interface IBlenderBridgeMessageHost
{
    void AttachBlenderApi(BlenderApi? blenderApi);

    Task HandleBridgeMessageAsync(ProtocolEnvelope envelope);
}
