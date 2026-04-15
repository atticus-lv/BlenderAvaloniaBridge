using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BlenderAvaloniaBridge;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed partial class BlenderDataApiTests
{
    [Fact]
    public async Task CallOperatorAsync_ParamsNamedArgs_SerializesProperties()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["operator"] = "mesh.primitive_cube_add",
                    ["result"] = new JsonArray("FINISHED"),
                })));
        var api = new BlenderDataApi(endpoint);

        var result = await api.CallOperatorAsync(
            "mesh.primitive_cube_add",
            ("size", 2.0));

        Assert.Equal("mesh.primitive_cube_add", result.OperatorName);
        Assert.Single(endpoint.Requests);
        Assert.Equal("ops.call", endpoint.Requests[0].Name);
        Assert.Equal("mesh.primitive_cube_add", endpoint.Requests[0].Payload.GetProperty("operator").GetString());
        Assert.Equal(2.0, endpoint.Requests[0].Payload.GetProperty("properties").GetProperty("size").GetDouble());
    }

    [Fact]
    public async Task CallAsync_ParamsNamedArgs_SerializesKwargs()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["return"] = new JsonObject
                    {
                        ["kind"] = "rna",
                        ["path"] = "bpy.data.materials[\"Mat_A\"]",
                        ["name"] = "Mat_A",
                        ["label"] = "Mat_A",
                        ["rnaType"] = "Material",
                    }
                })));
        var api = new BlenderDataApi(endpoint);

        var material = await api.CallAsync<RnaItemRef>(
            "bpy.data.materials",
            "new",
            ("name", "Mat_A"));

        Assert.Equal("Mat_A", material.Name);
        Assert.Equal("rna.call", endpoint.Requests[0].Name);
        Assert.Equal("new", endpoint.Requests[0].Payload.GetProperty("method").GetString());
        Assert.Equal("Mat_A", endpoint.Requests[0].Payload.GetProperty("kwargs").GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallOperatorAsync_WithContextOverride_SerializesExpectedWireShape()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["operator"] = "object.duplicate_move",
                    ["result"] = new JsonArray("FINISHED"),
                })));
        var api = new BlenderDataApi(endpoint);

        await api.CallOperatorAsync(
            "object.duplicate_move",
            new BlenderOperatorCall
            {
                ContextOverride = new BlenderContextOverride
                {
                    ActiveObject = "bpy.data.objects[\"Cube\"]",
                    SelectedObjects = new[] { "bpy.data.objects[\"Cube\"]" },
                }
            });

        var contextOverride = endpoint.Requests[0].Payload.GetProperty("contextOverride");
        Assert.Equal(
            "bpy.data.objects[\"Cube\"]",
            contextOverride.GetProperty("active_object").GetProperty("path").GetString());
        Assert.Equal(
            "bpy.data.objects[\"Cube\"]",
            contextOverride.GetProperty("selected_objects")[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task GetAsync_UsesRegisteredTypeInfoResolver()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["value"] = new JsonObject
                    {
                        ["name"] = "Cube",
                        ["index"] = 3,
                    }
                })));
        var options = new BlenderDataApiOptions();
        options.TypeInfoResolvers.Add(TestJsonContext.Default);
        var api = new BlenderDataApi(endpoint, options);

        var result = await api.GetAsync<TestPayload>("bpy.data.objects[\"Cube\"]");

        Assert.Equal("Cube", result.Name);
        Assert.Equal(3, result.Index);
    }

    [Fact]
    public async Task GetAsync_ThrowsWhenTypeInfoResolverIsMissing()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["value"] = new JsonObject
                    {
                        ["name"] = "Cube",
                        ["index"] = 3,
                    }
                })));
        var api = new BlenderDataApi(endpoint);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => api.GetAsync<TestPayload>("bpy.data.objects[\"Cube\"]"));

        Assert.Contains("missing_json_type_info_for_type", exception.Message);
    }

    [Fact]
    public async Task WatchAsync_SubscribesAndDispatchesDirtyEvents()
    {
        var endpoint = new RecordingEndpoint(_ => BusinessRequest.Response(1, ToJsonElement(new JsonObject())));
        var api = new BlenderDataApi(endpoint);
        var received = new TaskCompletionSource<WatchDirtyEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await api.WatchAsync(
            "materials",
            WatchSource.Depsgraph,
            "bpy.data.materials",
            watchDirtyEvent =>
            {
                received.TrySetResult(watchDirtyEvent);
                return Task.CompletedTask;
            });

        Assert.Single(endpoint.Requests);
        Assert.Equal("watch.subscribe", endpoint.Requests[0].Name);

        await ((IBusinessEventSink)api).HandleEventAsync(
            new BusinessEvent
            {
                Name = "watch.dirty",
                Payload = ToJsonElement(new JsonObject
                {
                    ["watchId"] = "materials",
                    ["revision"] = 4,
                    ["source"] = "depsgraph"
                })
            });

        var dirtyEvent = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("materials", dirtyEvent.WatchId);
        Assert.Equal(4, dirtyEvent.Revision);
        Assert.Equal(WatchSource.Depsgraph, dirtyEvent.Source);
    }

    [Fact]
    public async Task WatchAsync_DirtyEventDispatch_DoesNotAwaitCallbackCompletion()
    {
        var endpoint = new RecordingEndpoint(_ => BusinessRequest.Response(1, ToJsonElement(new JsonObject())));
        var api = new BlenderDataApi(endpoint);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCallbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await api.WatchAsync(
            "live-transform",
            WatchSource.Depsgraph,
            "bpy.data.objects[\"Cube\"]",
            async _ =>
            {
                callbackStarted.TrySetResult();
                await allowCallbackCompletion.Task;
            });

        var handleTask = ((IBusinessEventSink)api).HandleEventAsync(
            new BusinessEvent
            {
                Name = "watch.dirty",
                Payload = ToJsonElement(new JsonObject
                {
                    ["watchId"] = "live-transform",
                    ["revision"] = 1,
                    ["source"] = "depsgraph"
                })
            });

        await handleTask.WaitAsync(TimeSpan.FromSeconds(2));
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(allowCallbackCompletion.Task.IsCompleted);

        allowCallbackCompletion.TrySetResult();
    }

    private static JsonElement ToJsonElement(JsonNode node)
    {
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }

    public sealed class TestPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    [JsonSerializable(typeof(TestPayload))]
    public partial class TestJsonContext : JsonSerializerContext
    {
    }

    private sealed class RecordingEndpoint : IBusinessEndpoint
    {
        private readonly Func<BusinessRequest, BusinessResponse> _handler;

        public RecordingEndpoint(Func<BusinessRequest, BusinessResponse>? handler = null)
        {
            _handler = handler ?? (_ => BusinessRequest.Response(1, ToJsonElement(new JsonObject())));
        }

        public List<BusinessRequest> Requests { get; } = new();

        public ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return new ValueTask<BusinessResponse>(_handler(request));
        }
    }
}
