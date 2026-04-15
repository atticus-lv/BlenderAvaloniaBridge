using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BlenderAvaloniaBridge.Sample.ViewModels;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class MaterialsPageView : UserControl
{
    public MaterialsPageView()
    {
        InitializeComponent();
    }

    private void OnNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MaterialsPageViewModel viewModel)
        {
            _ = viewModel.CommitMaterialNameAsync();
        }
    }

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MaterialsPageViewModel viewModel)
        {
            _ = viewModel.CommitMaterialNameAsync();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
