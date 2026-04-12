using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderBridgeLauncherTests
{
    [Fact]
    public void TryParse_WithBridgeArguments_SplitsBridgeAndApplicationArguments()
    {
        var launch = BlenderBridgeLauncher.TryParse(new[]
        {
            "--blender-bridge", "true",
            "--blender-bridge-host", "127.0.0.1",
            "--blender-bridge-port", "34567",
            "--user-flag", "abc",
        });

        Assert.True(launch.IsBridgeMode);
        Assert.Equal(new[] { "--user-flag", "abc" }, launch.AppArgs);
        Assert.NotNull(launch.BridgeCommandLine);
        Assert.Equal("127.0.0.1", launch.BridgeCommandLine!.Host);
        Assert.Equal(34567, launch.BridgeCommandLine.Port);
    }

    [Fact]
    public void TryParse_WithoutBridgeArguments_PreservesApplicationArguments()
    {
        var launch = BlenderBridgeLauncher.TryParse(new[]
        {
            "--port", "8080",
            "--environment", "dev",
        });

        Assert.False(launch.IsBridgeMode);
        Assert.Equal(new[] { "--port", "8080", "--environment", "dev" }, launch.AppArgs);
        Assert.Null(launch.BridgeCommandLine);
    }
}
