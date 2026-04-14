using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderBridgeOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new BlenderBridgeOptions();

        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(0, options.Port);
        Assert.Equal(800, options.Width);
        Assert.Equal(600, options.Height);
        Assert.Equal(1.25, options.RenderScaling);
        Assert.Equal(BridgeWindowMode.Headless, options.WindowMode);
        Assert.True(options.SupportsBusiness);
        Assert.True(options.SupportsFrames);
        Assert.True(options.SupportsInput);
        Assert.Equal(60, options.TargetFps);
        Assert.Equal(4, options.IdleHeartbeatFps);
        Assert.Equal(1000, options.ContinuousFrameWindowMs);
        Assert.True(options.UseSharedMemory);
        Assert.True(options.EnableDiagnostics);
    }

    [Fact]
    public void Validate_RejectsFramesWhenBusinessIsDisabled()
    {
        var options = new BlenderBridgeOptions
        {
            SupportsBusiness = false,
            SupportsFrames = true,
            SupportsInput = false,
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_RejectsDesktopModeWithFramesOrInput()
    {
        var frameOptions = new BlenderBridgeOptions
        {
            WindowMode = BridgeWindowMode.Desktop,
            SupportsFrames = true,
            SupportsInput = false,
        };
        var inputOptions = new BlenderBridgeOptions
        {
            WindowMode = BridgeWindowMode.Desktop,
            SupportsFrames = false,
            SupportsInput = true,
        };

        Assert.Throws<ArgumentException>(() => frameOptions.Validate());
        Assert.Throws<ArgumentException>(() => inputOptions.Validate());
    }
}
