using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlenderAvaloniaBridge.Sample.Views;

public sealed partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
