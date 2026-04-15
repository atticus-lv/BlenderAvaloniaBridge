using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlenderAvaloniaBridge.Sample.ViewModels;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class BlenderObjectsPageView : UserControl
{
    public BlenderObjectsPageView()
    {
        InitializeComponent();
    }

    private void OnNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BlenderObjectsPageViewModel viewModel)
        {
            _ = viewModel.CommitNameAsync();
        }
    }

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is BlenderObjectsPageViewModel viewModel)
        {
            _ = viewModel.CommitNameAsync();
        }
    }

    private void OnLocationLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BlenderObjectsPageViewModel viewModel)
        {
            _ = viewModel.CommitLocationAsync();
        }
    }

    private void OnLocationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is BlenderObjectsPageViewModel viewModel)
        {
            _ = viewModel.CommitLocationAsync();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
