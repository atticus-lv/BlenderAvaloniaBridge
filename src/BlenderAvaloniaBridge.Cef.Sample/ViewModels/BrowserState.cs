namespace BlenderAvaloniaBridge.Cef.Sample.ViewModels;

public sealed record BrowserState(
    string Address,
    string Title,
    bool CanGoBack,
    bool CanGoForward,
    bool IsLoading);
