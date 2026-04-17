using Avalonia.Controls;
using Avalonia.Threading;
using BlenderAvaloniaBridge.Cef.Sample.ViewModels;
using Xilium.CefGlue;
using Xilium.CefGlue.Avalonia;
using Xilium.CefGlue.Common.Events;

namespace BlenderAvaloniaBridge.Cef.Sample.Services;

internal sealed class CefBrowserController : IBrowserController, IDisposable
{
    private readonly AvaloniaCefBrowser _browser;
    private BrowserState _lastState = new(string.Empty, string.Empty, false, false, false);

    public CefBrowserController()
    {
        _browser = new AvaloniaCefBrowser(CefRequestContext.GetGlobalContext);
        _browser.AddressChanged += OnBrowserStateChanged;
        _browser.TitleChanged += OnBrowserStateChanged;
        _browser.LoadingStateChange += OnBrowserStateChanged;
        View = _browser;
    }

    public Control View { get; }

    public event EventHandler<BrowserState>? StateChanged;

    public void Initialize(string startUrl)
    {
        LoadUrl(startUrl);
    }

    public void LoadUrl(string url)
    {
        _browser.Address = url;
        PublishCurrentState();
    }

    public void GoBack()
    {
        if (_browser.CanGoBack)
        {
            _browser.GoBack();
        }

        PublishCurrentState();
    }

    public void GoForward()
    {
        if (_browser.CanGoForward)
        {
            _browser.GoForward();
        }

        PublishCurrentState();
    }

    public void Reload()
    {
        _browser.Reload(ignoreCache: false);
        PublishCurrentState();
    }

    public void Dispose()
    {
        _browser.AddressChanged -= OnBrowserStateChanged;
        _browser.TitleChanged -= OnBrowserStateChanged;
        _browser.LoadingStateChange -= OnBrowserStateChanged;
        _browser.Dispose();
    }

    private void OnBrowserStateChanged(object? sender, string _)
    {
        PublishCurrentState();
    }

    private void OnBrowserStateChanged(object? sender, LoadingStateChangeEventArgs _)
    {
        PublishCurrentState();
    }

    private void PublishCurrentState()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(PublishCurrentState);
            return;
        }

        var state = new BrowserState(
            _browser.Address ?? string.Empty,
            _browser.Title ?? string.Empty,
            _browser.CanGoBack,
            _browser.CanGoForward,
            _browser.IsLoading);

        if (state == _lastState)
        {
            return;
        }

        _lastState = state;
        Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, state));
    }
}
