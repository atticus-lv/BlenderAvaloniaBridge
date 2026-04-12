using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class ButtonsDemoPageView : UserControl
{
    public ButtonsDemoPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
