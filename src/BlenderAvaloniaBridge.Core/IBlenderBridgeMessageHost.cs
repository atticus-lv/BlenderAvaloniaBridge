using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge;

public interface IBlenderBridgeMessageHost
{
    void AttachBlenderDataApi(IBlenderDataApi? blenderDataApi);

    Task HandleBridgeMessageAsync(ProtocolEnvelope envelope);
}
