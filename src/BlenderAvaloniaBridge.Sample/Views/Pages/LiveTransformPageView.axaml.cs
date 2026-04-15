using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class LiveTransformPageView : UserControl
{
    public LiveTransformPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
