using BlenderAvaloniaBridge;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public interface IBlenderSamplePageViewModel : IBlenderDataApiSink, IBlenderBridgeStatusSink
{
    Task ActivateAsync();

    Task DeactivateAsync();
}
