using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace BlenderAvaloniaBridge;

internal sealed class BridgeHeadlessApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }
}
