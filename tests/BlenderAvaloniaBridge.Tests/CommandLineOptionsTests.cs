using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Parse_WithoutBridgeArguments_UsesDesktopMode()
    {
        var options = CommandLineOptions.Parse(Array.Empty<string>());

        Assert.Equal(LaunchMode.DesktopWindow, options.Mode);
        Assert.Equal(0, options.Port);
        Assert.Empty(options.AppArgs);
    }

    [Fact]
    public void Parse_WithNonBridgePortArgument_DoesNotSwitchModes()
    {
        var options = CommandLineOptions.Parse(new[] { "--port", "34567", "--environment", "dev" });

        Assert.Equal(LaunchMode.DesktopWindow, options.Mode);
        Assert.Equal(0, options.Port);
        Assert.Equal(new[] { "--port", "34567", "--environment", "dev" }, options.AppArgs);
    }

    [Fact]
    public void Parse_WithExplicitPrefixedBridgeArguments_UsesBridgeMode()
    {
        var options = CommandLineOptions.Parse(new[]
        {
            "--blender-bridge", "true",
            "--blender-bridge-host", "127.0.0.1",
            "--blender-bridge-port", "34567",
            "--blender-bridge-width", "1024",
            "--blender-bridge-height", "768",
            "--blender-bridge-render-scaling", "1.25",
            "--theme", "dark",
        });

        Assert.Equal(LaunchMode.BlenderBridge, options.Mode);
        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(34567, options.Port);
        Assert.Equal(1024, options.Width);
        Assert.Equal(768, options.Height);
        Assert.Equal(1.25, options.RenderScaling);
        Assert.Equal(BridgeWindowMode.Headless, options.WindowMode);
        Assert.True(options.SupportsBusiness);
        Assert.True(options.SupportsFrames);
        Assert.True(options.SupportsInput);
        Assert.Equal(new[] { "--theme", "dark" }, options.AppArgs);
    }

    [Fact]
    public void Parse_WithDesktopBridgeArguments_UsesDesktopBusinessCapabilities()
    {
        var options = CommandLineOptions.Parse(new[]
        {
            "--blender-bridge", "true",
            "--blender-bridge-window-mode", "desktop",
            "--blender-bridge-supports-business", "true",
            "--blender-bridge-supports-frames", "false",
            "--blender-bridge-supports-input", "false",
        });

        Assert.Equal(LaunchMode.BlenderBridge, options.Mode);
        Assert.Equal(BridgeWindowMode.Desktop, options.WindowMode);
        Assert.True(options.SupportsBusiness);
        Assert.False(options.SupportsFrames);
        Assert.False(options.SupportsInput);
    }

    [Fact]
    public void Parse_WithDesktopWindowModeDefaultsFramesAndInputToFalse()
    {
        var options = CommandLineOptions.Parse(new[]
        {
            "--blender-bridge", "true",
            "--blender-bridge-window-mode", "desktop",
        });

        Assert.Equal(BridgeWindowMode.Desktop, options.WindowMode);
        Assert.True(options.SupportsBusiness);
        Assert.False(options.SupportsFrames);
        Assert.False(options.SupportsInput);
    }

    [Fact]
    public void Parse_WithLegacyBridgeFlags_DoesNotUseBridgeMode()
    {
        var options = CommandLineOptions.Parse(new[] { "--bridge", "true", "--host", "127.0.0.1", "--port", "45678" });

        Assert.Equal(LaunchMode.DesktopWindow, options.Mode);
        Assert.Equal(0, options.Port);
        Assert.Equal(new[] { "--bridge", "true", "--host", "127.0.0.1", "--port", "45678" }, options.AppArgs);
    }
}
