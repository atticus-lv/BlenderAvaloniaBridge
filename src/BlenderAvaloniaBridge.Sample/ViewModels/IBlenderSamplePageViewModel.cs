using BlenderAvaloniaBridge;

namespace BlenderAvaloniaBridge.Sample.ViewModels;

public interface IBlenderSamplePageViewModel : IBlenderDataApiSink, IBlenderBridgeStatusSink
{
    Task ActivateAsync();

    Task DeactivateAsync();
}
