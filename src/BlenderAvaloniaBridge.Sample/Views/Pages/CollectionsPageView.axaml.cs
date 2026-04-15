using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class CollectionsPageView : UserControl
{
    public CollectionsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
