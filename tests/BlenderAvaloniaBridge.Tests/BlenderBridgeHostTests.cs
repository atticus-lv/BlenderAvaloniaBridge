using Avalonia.Controls;
using BlenderAvaloniaBridge.Runtime;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderBridgeHostTests
{
    [Fact]
    public async Task CreateWindow_WithFactory_UsesFactoryResult()
    {
        var expected = await HeadlessRuntimeThread.Shared.InvokeAsync(() => new Window());
        var host = new BlenderBridgeHost(() => expected, new BlenderBridgeOptions());

        Assert.Same(expected, host.CreateWindowForTesting());
    }

    [Fact]
    public async Task CreateWindow_WithExistingWindow_ReusesSameWindow()
    {
        var expected = await HeadlessRuntimeThread.Shared.InvokeAsync(() => new Window());
        var host = new BlenderBridgeHost(expected, new BlenderBridgeOptions());

        Assert.Same(expected, host.CreateWindowForTesting());
    }
}
