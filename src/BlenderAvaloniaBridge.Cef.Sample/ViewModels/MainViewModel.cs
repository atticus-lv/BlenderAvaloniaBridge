using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderAvaloniaBridge.Cef.Sample.ViewModels;

public partial class MainViewModel : ObservableObject, IBlenderBridgeStatusSink, IBlenderApiSink
{
    private const string DefaultAddress = "https://www.blender.org/";

    private readonly IBrowserController _browserController;

    [ObservableProperty]
    private string _address = DefaultAddress;

    [ObservableProperty]
    private string _pageTitle = "CEF Browser";

    [ObservableProperty]
    private string _statusText = "Browser ready";

    [ObservableProperty]
    private string _bridgeStatus = "Bridge disconnected";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private BlenderApi? _blenderApi;

    public MainViewModel(IBrowserController browserController)
    {
        _browserController = browserController ?? throw new ArgumentNullException(nameof(browserController));
        _browserController.StateChanged += OnBrowserStateChanged;
        _browserController.Initialize(Address);
    }

    public void SetBridgeStatus(string status)
    {
        BridgeStatus = string.IsNullOrWhiteSpace(status) ? "Bridge connected" : status;
    }

    public void AttachBlenderApi(BlenderApi? blenderApi)
    {
        BlenderApi = blenderApi;
    }

    [RelayCommand]
    private void Navigate()
    {
        var normalizedAddress = NormalizeAddress(Address);
        Address = normalizedAddress;
        _browserController.LoadUrl(normalizedAddress);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        _browserController.GoBack();
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        _browserController.GoForward();
    }

    [RelayCommand]
    private void Reload()
    {
        _browserController.Reload();
    }

    partial void OnCanGoBackChanged(bool value)
    {
        GoBackCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanGoForwardChanged(bool value)
    {
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    private void OnBrowserStateChanged(object? sender, BrowserState state)
    {
        Address = string.IsNullOrWhiteSpace(state.Address) ? Address : state.Address;
        PageTitle = string.IsNullOrWhiteSpace(state.Title) ? "CEF Browser" : state.Title;
        CanGoBack = state.CanGoBack;
        CanGoForward = state.CanGoForward;
        IsLoading = state.IsLoading;
        StatusText = state.IsLoading
            ? $"Loading {PageTitle}"
            : $"Showing {PageTitle}";
    }

    private static string NormalizeAddress(string? address)
    {
        var value = (address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultAddress;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return $"https://{value}";
    }
}
