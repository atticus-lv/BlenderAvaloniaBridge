using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Runtime;
using BlenderAvaloniaBridge.Transport;
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

    [Fact]
    public async Task HeadlessHost_CanHandshakeSequentiallyInSameProcess()
    {
        await RunHeadlessHandshakeAsync();
        await RunHeadlessHandshakeAsync();
    }

    private static async Task RunHeadlessHandshakeAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var hostTask = BlenderBridgeHost.RunAsync(
            () => new Window(),
            new BlenderBridgeOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                WindowMode = BridgeWindowMode.Headless,
                Width = 32,
                Height = 24,
                UseSharedMemory = false,
                SupportsBusiness = true,
                SupportsFrames = true,
                SupportsInput = false,
            },
            hostCts.Token);

        using var client = await listener.AcceptTcpClientAsync(hostCts.Token);
        var connection = new LengthPrefixedConnection(client.GetStream());

        await connection.WriteAsync(
            ProtocolPacket.CreateControl(
                new ProtocolEnvelope
                {
                    Type = "init",
                    Seq = 1,
                    Width = 32,
                    Height = 24,
                    PixelFormat = "bgra8",
                    Stride = 32 * 4,
                    WindowMode = "headless",
                    SupportsBusiness = true,
                    SupportsFrames = true,
                    SupportsInput = false,
                    Message = "test-ready",
                }),
            hostCts.Token);

        var initAck = await connection.ReadAsync(hostCts.Token);
        Assert.NotNull(initAck);
        Assert.Equal("init", initAck!.Header.Type);

        var frame = await connection.ReadAsync(hostCts.Token);
        Assert.NotNull(frame);
        Assert.Equal("frame", frame!.Header.Type);

        client.Close();
        await hostTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
