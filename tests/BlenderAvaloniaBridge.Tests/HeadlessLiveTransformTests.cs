using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Avalonia.Controls;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Sample.ViewModels;
using BlenderAvaloniaBridge.Sample.Views;
using BlenderAvaloniaBridge.Transport;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

[Collection(nameof(HeadlessUiCollection))]
public sealed class HeadlessLiveTransformTests
{
    [Fact]
    public async Task HeadlessLiveTransform_WatchDirty_TriggersTransformReload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var mainViewModel = new MainViewModel();
        using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var hostTask = BlenderBridgeHost.RunAsync(
            () => new MainWindow(mainViewModel),
            new BlenderBridgeOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                WindowMode = BridgeWindowMode.Headless,
                Width = 96,
                Height = 96,
                UseSharedMemory = false,
                SupportsBusiness = true,
                SupportsFrames = true,
                SupportsInput = true,
            },
            hostCts.Token);

        await using var server = await TestBridgeServer.AcceptAsync(listener, hostCts.Token);
        await server.SendAsync(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = 1,
                Width = 96,
                Height = 96,
                PixelFormat = "bgra8",
                Stride = 96 * 4,
                WindowMode = "headless",
                SupportsBusiness = true,
                SupportsFrames = true,
                SupportsInput = true,
                Message = "blender-ready",
            },
            hostCts.Token);

        await server.WaitForAsync(packet => packet.Header.Type == "init", hostCts.Token);

        mainViewModel.ShowLiveTransformPageCommand.Execute(null);
        await WaitForAsync(() => mainViewModel.IsLiveTransformPageSelected);

        await server.WaitForRequestsAsync("rna.list", 1, hostCts.Token);
        await server.WaitForRequestsAsync("rna.get", 3, hostCts.Token);

        var liveViewModel = Assert.IsType<LiveTransformPageViewModel>(mainViewModel.CurrentPage);
        await WaitForAsync(() => liveViewModel.CanEnableLiveWatch);

        liveViewModel.IsLiveWatchEnabled = true;

        var subscribe = await server.WaitForRequestAsync("watch.subscribe", hostCts.Token);
        Assert.Equal("bpy.data.objects[\"Cube\"]", subscribe.Header.Payload?.GetProperty("path").GetString());

        var getCountBeforeDirty = server.CountRequests("rna.get");
        var frameCountBeforeDirty = server.CountPackets("frame");

        await server.SendAsync(
            new ProtocolEnvelope
            {
                Type = "business_event",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                Name = "watch.dirty",
                Payload = JsonDocument.Parse(
                    """
                    {
                      "watchId": "live-transform-3",
                      "revision": 1,
                      "source": "depsgraph"
                    }
                    """).RootElement.Clone(),
            },
            hostCts.Token);

        await server.WaitForRequestsAsync("rna.get", getCountBeforeDirty + 3, hostCts.Token);
        await server.WaitForPacketsAsync("frame", frameCountBeforeDirty + 1, hostCts.Token);

        Assert.False(hostTask.IsFaulted);
        Assert.False(hostTask.IsCompletedSuccessfully);

        await server.DisposeAsync();
        await hostTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task HeadlessLiveTransform_WatchDirty_EventuallyPublishesChangedFrameWithoutInput()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var mainViewModel = new MainViewModel();
        using var hostCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var hostTask = BlenderBridgeHost.RunAsync(
            () => new MainWindow(mainViewModel),
            new BlenderBridgeOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                WindowMode = BridgeWindowMode.Headless,
                Width = 96,
                Height = 96,
                UseSharedMemory = false,
                SupportsBusiness = true,
                SupportsFrames = true,
                SupportsInput = true,
            },
            hostCts.Token);

        await using var server = await TestBridgeServer.AcceptAsync(listener, hostCts.Token);
        await server.SendAsync(
            new ProtocolEnvelope
            {
                Type = "init",
                Seq = 1,
                Width = 96,
                Height = 96,
                PixelFormat = "bgra8",
                Stride = 96 * 4,
                WindowMode = "headless",
                SupportsBusiness = true,
                SupportsFrames = true,
                SupportsInput = true,
                Message = "blender-ready",
            },
            hostCts.Token);

        await server.WaitForAsync(packet => packet.Header.Type == "init", hostCts.Token);

        mainViewModel.ShowLiveTransformPageCommand.Execute(null);
        await WaitForAsync(() => mainViewModel.IsLiveTransformPageSelected);

        await server.WaitForRequestsAsync("rna.list", 1, hostCts.Token);
        await server.WaitForRequestsAsync("rna.get", 3, hostCts.Token);

        var liveViewModel = Assert.IsType<LiveTransformPageViewModel>(mainViewModel.CurrentPage);
        await WaitForAsync(() => liveViewModel.CanEnableLiveWatch);

        liveViewModel.IsLiveWatchEnabled = true;
        await server.WaitForRequestAsync("watch.subscribe", hostCts.Token);

        var baselineFrame = await server.WaitForAsync(
            packet => packet.Header.Type == "frame" && packet.Payload.Length > 0,
            hostCts.Token);
        var baselinePayload = baselineFrame.Payload.ToArray();

        server.SwitchToUpdatedTransformSnapshot();
        await server.SendAsync(
            new ProtocolEnvelope
            {
                Type = "business_event",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                Name = "watch.dirty",
                Payload = JsonDocument.Parse(
                    """
                    {
                      "watchId": "live-transform-3",
                      "revision": 2,
                      "source": "depsgraph"
                    }
                    """).RootElement.Clone(),
            },
            hostCts.Token);

        await server.WaitForFramePayloadChangeAsync(baselinePayload, hostCts.Token);

        Assert.False(hostTask.IsFaulted);
        Assert.False(hostTask.IsCompletedSuccessfully);

        await server.DisposeAsync();
        await hostTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class TestBridgeServer : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly LengthPrefixedConnection _connection;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly List<ProtocolPacket> _received = [];
        private readonly object _gate = new();
        private readonly Task _readLoop;
        private volatile bool _useUpdatedTransformSnapshot;

        private TestBridgeServer(TcpClient client)
        {
            _client = client;
            _connection = new LengthPrefixedConnection(client.GetStream());
            _readLoop = Task.Run(ReadLoopAsync);
        }

        public static async Task<TestBridgeServer> AcceptAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken);
            return new TestBridgeServer(client);
        }

        public async Task SendAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
        {
            await WritePacketAsync(ProtocolPacket.CreateControl(envelope), cancellationToken);
        }

        public int CountRequests(string name)
        {
            lock (_gate)
            {
                return _received.Count(packet =>
                    packet.Header.Type == "business_request"
                    && string.Equals(packet.Header.Name, name, StringComparison.Ordinal));
            }
        }

        public int CountPackets(string type)
        {
            lock (_gate)
            {
                return _received.Count(packet =>
                    string.Equals(packet.Header.Type, type, StringComparison.Ordinal));
            }
        }

        public async Task WaitForRequestsAsync(string name, int count, CancellationToken cancellationToken)
        {
            await WaitForAsync(
                packet => packet.Header.Type == "business_request" && string.Equals(packet.Header.Name, name, StringComparison.Ordinal),
                () => CountRequests(name) >= count,
                cancellationToken);
        }

        public async Task WaitForPacketsAsync(string type, int count, CancellationToken cancellationToken)
        {
            await WaitForAsync(
                packet => string.Equals(packet.Header.Type, type, StringComparison.Ordinal),
                () => CountPackets(type) >= count,
                cancellationToken);
        }

        public async Task<ProtocolPacket> WaitForRequestAsync(string name, CancellationToken cancellationToken)
        {
            return await WaitForAsync(
                packet => packet.Header.Type == "business_request" && string.Equals(packet.Header.Name, name, StringComparison.Ordinal),
                () =>
                {
                    lock (_gate)
                    {
                        return _received.LastOrDefault(packet =>
                            packet.Header.Type == "business_request"
                            && string.Equals(packet.Header.Name, name, StringComparison.Ordinal));
                    }
                },
                cancellationToken);
        }

        public async Task<ProtocolPacket> WaitForAsync(Func<ProtocolPacket, bool> predicate, CancellationToken cancellationToken)
        {
            return await WaitForAsync(
                predicate,
                () =>
                {
                    lock (_gate)
                    {
                        return _received.LastOrDefault(predicate);
                    }
                },
                cancellationToken);
        }

        public void SwitchToUpdatedTransformSnapshot()
        {
            _useUpdatedTransformSnapshot = true;
        }

        public async Task<ProtocolPacket> WaitForFramePayloadChangeAsync(byte[] baselinePayload, CancellationToken cancellationToken)
        {
            return await WaitForAsync(
                packet => packet.Header.Type == "frame"
                          && packet.Payload.Length > 0
                          && !packet.Payload.AsSpan().SequenceEqual(baselinePayload),
                cancellationToken);
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (true)
                {
                    var packet = await _connection.ReadAsync(CancellationToken.None);
                    if (packet is null)
                    {
                        break;
                    }

                    lock (_gate)
                    {
                        _received.Add(packet);
                    }

                    if (packet.Header.Type == "business_request")
                    {
                        await HandleBusinessRequestAsync(packet);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task HandleBusinessRequestAsync(ProtocolPacket packet)
        {
            var payload = packet.Header.Payload ?? default;
            switch (packet.Header.Name)
            {
                case "rna.list":
                    await SendBusinessResponseAsync(
                        packet.Header,
                        """
                        {
                          "path": "bpy.context.scene.objects",
                          "items": [
                            {
                              "kind": "rna",
                              "path": "bpy.data.objects[\"Cube\"]",
                              "name": "Cube",
                              "label": "Cube",
                              "rnaType": "Object",
                              "idType": "OBJECT",
                              "sessionUid": 3,
                              "metadata": {
                                "objectType": "MESH",
                                "isActive": true
                              }
                            }
                          ]
                        }
                        """);
                    break;
                case "rna.get":
                    var path = payload.GetProperty("path").GetString() ?? string.Empty;
                    var isUpdated = _useUpdatedTransformSnapshot;
                    var responseJson = path switch
                    {
                        "bpy.data.objects[\"Cube\"].location" => isUpdated
                            ? """{ "value": [9.0, 8.0, 7.0] }"""
                            : """{ "value": [1.0, 2.0, 3.0] }""",
                        "bpy.data.objects[\"Cube\"].rotation_euler" => isUpdated
                            ? """{ "value": [0.9, 0.8, 0.7] }"""
                            : """{ "value": [0.1, 0.2, 0.3] }""",
                        "bpy.data.objects[\"Cube\"].scale" => isUpdated
                            ? """{ "value": [2.0, 2.5, 3.0] }"""
                            : """{ "value": [1.0, 1.0, 1.0] }""",
                        _ => """{ "value": null }""",
                    };
                    await SendBusinessResponseAsync(packet.Header, responseJson);
                    break;
                case "watch.subscribe":
                case "watch.unsubscribe":
                    await SendBusinessResponseAsync(packet.Header, "{}");
                    break;
                default:
                    await SendAsync(
                        new ProtocolEnvelope
                        {
                            Type = "business_response",
                            ProtocolVersion = 1,
                            SchemaVersion = 1,
                            MessageId = 9000,
                            ReplyTo = packet.Header.MessageId,
                            Ok = false,
                            Error = new BusinessError("unsupported_business_request", packet.Header.Name ?? "unknown"),
                        },
                        CancellationToken.None);
                    break;
            }
        }

        private async Task SendBusinessResponseAsync(ProtocolEnvelope request, string payloadJson)
        {
            await WritePacketAsync(
                ProtocolPacket.CreateControl(
                new ProtocolEnvelope
                {
                    Type = "business_response",
                    ProtocolVersion = 1,
                    SchemaVersion = 1,
                    MessageId = 1000 + (int)(request.MessageId ?? 0),
                    ReplyTo = request.MessageId,
                    Ok = true,
                    Payload = JsonDocument.Parse(payloadJson).RootElement.Clone(),
                }),
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Close();
            try
            {
                await _readLoop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }

        private static async Task<T> WaitForAsync<T>(
            Func<ProtocolPacket, bool> predicate,
            Func<T?> currentValue,
            CancellationToken cancellationToken)
            where T : class
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = currentValue();
                if (value is not null)
                {
                    return value;
                }

                if (DateTime.UtcNow >= timeoutAt)
                {
                    throw new TimeoutException("Timed out waiting for packet.");
                }

                await Task.Delay(25, cancellationToken);
            }
        }

        private static async Task WaitForAsync(
            Func<ProtocolPacket, bool> predicate,
            Func<bool> condition,
            CancellationToken cancellationToken)
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= timeoutAt)
                {
                    throw new TimeoutException("Timed out waiting for packets.");
                }

                await Task.Delay(25, cancellationToken);
            }
        }

        private async Task WritePacketAsync(ProtocolPacket packet, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _connection.WriteAsync(packet, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}

[CollectionDefinition(nameof(HeadlessUiCollection), DisableParallelization = true)]
public sealed class HeadlessUiCollection
{
}
