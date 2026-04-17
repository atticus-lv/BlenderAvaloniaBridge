using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace BlenderAvaloniaBridge;

internal sealed class BridgeHeadlessApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<BridgeHeadlessApp>()
            .UseSkia()
#if !AVALONIA11_COMPAT
            .UseHarfBuzz()
#endif
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .LogToTrace();
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }
}
