namespace BlenderAvaloniaBridge.Cef.Sample.ViewModels;

public interface IBrowserController
{
    event EventHandler<BrowserState>? StateChanged;

    void Initialize(string startUrl);

    void LoadUrl(string url);

    void GoBack();

    void GoForward();

    void Reload();
}
