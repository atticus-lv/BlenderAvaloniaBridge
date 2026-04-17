using BlenderAvaloniaBridge.Cef.Sample.ViewModels;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class CefSampleViewModelTests
{
    [Fact]
    public void MainViewModel_DefaultsToBlenderPage()
    {
        var browserController = new TestBrowserController();
        var viewModel = new MainViewModel(browserController);

        Assert.Equal("https://www.blender.org/", viewModel.Address);
        Assert.Equal("https://www.blender.org/", browserController.StartUrl);
        Assert.Equal("Browser ready", viewModel.StatusText);
    }

    [Fact]
    public void NavigateCommand_AddsHttpsSchemeBeforeLoading()
    {
        var browserController = new TestBrowserController();
        var viewModel = new MainViewModel(browserController)
        {
            Address = "example.com/docs",
        };

        viewModel.NavigateCommand.Execute(null);

        Assert.Equal("https://example.com/docs", browserController.LastLoadedUrl);
        Assert.Equal("https://example.com/docs", viewModel.Address);
    }

    [Fact]
    public void BrowserStateChanged_UpdatesNavigationState()
    {
        var browserController = new TestBrowserController();
        var viewModel = new MainViewModel(browserController);

        browserController.PublishState(new BrowserState(
            "https://docs.example.com/",
            "Docs",
            true,
            false,
            true));

        Assert.Equal("https://docs.example.com/", viewModel.Address);
        Assert.Equal("Docs", viewModel.PageTitle);
        Assert.True(viewModel.CanGoBack);
        Assert.False(viewModel.CanGoForward);
        Assert.Equal("Loading Docs", viewModel.StatusText);
    }

    private sealed class TestBrowserController : IBrowserController
    {
        public string? LastLoadedUrl { get; private set; }

        public string? StartUrl { get; private set; }

        public event EventHandler<BrowserState>? StateChanged;

        public void GoBack()
        {
        }

        public void GoForward()
        {
        }

        public void Initialize(string startUrl)
        {
            StartUrl = startUrl;
        }

        public void LoadUrl(string url)
        {
            LastLoadedUrl = url;
        }

        public void PublishState(BrowserState state)
        {
            StateChanged?.Invoke(this, state);
        }

        public void Reload()
        {
        }
    }
}
