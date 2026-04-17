using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BlenderAvaloniaBridge.Cef.Sample.Views;

namespace BlenderAvaloniaBridge.Cef.Sample;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            if (DesktopBridgeLaunchContext.IsConfigured)
            {
                DesktopBridgeLaunchContext.StartBridge(mainWindow, desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
