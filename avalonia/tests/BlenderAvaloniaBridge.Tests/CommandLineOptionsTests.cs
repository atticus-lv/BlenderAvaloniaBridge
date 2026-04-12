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
    }

    [Fact]
    public void Parse_WithHostAndPort_UsesBridgeMode()
    {
        var options = CommandLineOptions.Parse(new[] { "--host", "127.0.0.1", "--port", "34567", "--width", "1024", "--height", "768" });

        Assert.Equal(LaunchMode.BlenderBridge, options.Mode);
        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(34567, options.Port);
        Assert.Equal(1024, options.Width);
        Assert.Equal(768, options.Height);
    }

    [Fact]
    public void Parse_WithExplicitBridgeFlag_UsesBridgeMode()
    {
        var options = CommandLineOptions.Parse(new[] { "--bridge", "true", "--host", "127.0.0.1", "--port", "45678" });

        Assert.Equal(LaunchMode.BlenderBridge, options.Mode);
        Assert.Equal(45678, options.Port);
    }
}
