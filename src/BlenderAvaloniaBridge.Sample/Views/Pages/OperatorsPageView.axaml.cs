using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class OperatorsPageView : UserControl
{
    public OperatorsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
