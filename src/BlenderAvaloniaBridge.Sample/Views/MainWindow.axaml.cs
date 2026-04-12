using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MainViewModel = BlenderAvaloniaBridge.Sample.ViewModels.MainViewModel;

namespace BlenderAvaloniaBridge.Sample.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
        : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Content = MainViewFactory.CreateMainView(viewModel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
