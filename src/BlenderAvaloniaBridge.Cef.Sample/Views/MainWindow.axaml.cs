using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlenderAvaloniaBridge.Cef.Sample.Services;
using BlenderAvaloniaBridge.Cef.Sample.ViewModels;

namespace BlenderAvaloniaBridge.Cef.Sample.Views;

public sealed partial class MainWindow : Window
{
    private readonly CefBrowserController _browserController;

    public MainWindow()
    {
        InitializeComponent();
        _browserController = new CefBrowserController();
        var viewModel = new MainViewModel(_browserController);
        DataContext = viewModel;
        var browserPresenter = this.FindControl<ContentControl>("BrowserPresenter")
            ?? throw new InvalidOperationException("Browser presenter control could not be located.");
        browserPresenter.Content = _browserController.View;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _browserController.Dispose();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
