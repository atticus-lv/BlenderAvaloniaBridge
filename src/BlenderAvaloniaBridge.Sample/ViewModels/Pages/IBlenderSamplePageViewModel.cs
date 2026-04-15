using BlenderAvaloniaBridge;

namespace BlenderAvaloniaBridge.Sample.ViewModels.Pages;

public interface IBlenderSamplePageViewModel : IBlenderApiSink, IBlenderBridgeStatusSink
{
    Task ActivateAsync();

    Task DeactivateAsync();
}
