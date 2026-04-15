using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Linq;
using BlenderAvaloniaBridge;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed partial class BlenderApiTests
{
    [Fact]
    public async Task Ops_CallAsync_ParamsNamedArgs_SerializesProperties()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["operator"] = "mesh.primitive_cube_add",
                    ["result"] = new JsonArray("FINISHED"),
                })));
        var blenderApi = new BlenderApi(endpoint);

        var result = await blenderApi.Ops.CallAsync(
            "mesh.primitive_cube_add",
            ("size", 2.0));

        Assert.Equal("mesh.primitive_cube_add", result.OperatorName);
        Assert.Single(endpoint.Requests);
        Assert.Equal("ops.call", endpoint.Requests[0].Name);
        Assert.Equal("mesh.primitive_cube_add", endpoint.Requests[0].Payload.GetProperty("operator").GetString());
        Assert.Equal(2.0, endpoint.Requests[0].Payload.GetProperty("properties").GetProperty("size").GetDouble());
    }

    [Fact]
    public async Task Rna_CallAsync_ParamsNamedArgs_SerializesKwargs()
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
        var blenderApi = new BlenderApi(endpoint);

        var material = await blenderApi.Rna.CallAsync<RnaItemRef>(
            "bpy.data.materials",
            "new",
            ("name", "Mat_A"));

        Assert.Equal("Mat_A", material.Name);
        Assert.Equal("rna.call", endpoint.Requests[0].Name);
        Assert.Equal("new", endpoint.Requests[0].Payload.GetProperty("method").GetString());
        Assert.Equal("Mat_A", endpoint.Requests[0].Payload.GetProperty("kwargs").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Rna_CallAsync_AllowsNullableReturnForVoidLikeMethods()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["return"] = null,
                })));
        var blenderApi = new BlenderApi(endpoint);

        var result = await blenderApi.Rna.CallAsync<RnaItemRef?>(
            "bpy.data.materials[\"Mat_A\"]",
            "asset_generate_preview");

        Assert.Null(result);
        Assert.Equal("asset_generate_preview", endpoint.Requests[0].Payload.GetProperty("method").GetString());
    }

    [Fact]
    public async Task Ops_CallAsync_WithContextOverride_SerializesExpectedWireShape()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ToJsonElement(new JsonObject
                {
                    ["operator"] = "object.duplicate_move",
                    ["result"] = new JsonArray("FINISHED"),
                })));
        var blenderApi = new BlenderApi(endpoint);

        await blenderApi.Ops.CallAsync(
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
    public async Task Rna_GetAsync_UsesRegisteredTypeInfoResolver()
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
        var options = new BlenderApiOptions();
        options.TypeInfoResolvers.Add(TestJsonContext.Default);
        var blenderApi = new BlenderApi(endpoint, options);

        var result = await blenderApi.Rna.GetAsync<TestPayload>("bpy.data.objects[\"Cube\"]");

        Assert.Equal("Cube", result.Name);
        Assert.Equal(3, result.Index);
    }

    [Fact]
    public async Task Rna_GetAsync_ThrowsWhenTypeInfoResolverIsMissing()
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
        var blenderApi = new BlenderApi(endpoint);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => blenderApi.Rna.GetAsync<TestPayload>("bpy.data.objects[\"Cube\"]"));

        Assert.Contains("missing_json_type_info_for_type", exception.Message);
    }

    [Fact]
    public async Task Rna_GetAsync_WhenBusinessRequestFails_PreservesStructuredError()
    {
        var endpoint = new RecordingEndpoint(
            _ => BusinessRequest.Response(
                1,
                ok: false,
                error: new BusinessError(
                    "unsupported_business_request",
                    "Reading this path is not allowed.")));
        var blenderApi = new BlenderApi(endpoint);

        var exception = await Assert.ThrowsAsync<BlenderBusinessException>(
            () => blenderApi.Rna.GetAsync<string>("bpy.data.objects[\"Cube\"].name"));

        Assert.Equal("rna.get", exception.RequestName);
        Assert.Equal("unsupported_business_request", exception.Error?.Code);
        Assert.Equal("Reading this path is not allowed.", exception.Error?.Message);
        Assert.Contains("unsupported_business_request", exception.Message);
    }

    [Fact]
    public async Task Rna_ReadArrayAsync_ReturnsMetadataAndRawBytes()
    {
        var endpoint = new RecordingEndpoint(
            _ => new BusinessResponse
            {
                ReplyTo = 1,
                Ok = true,
                Payload = ToJsonElement(new JsonObject
                {
                    ["path"] = "bpy.data.objects[\"Cube\"].location",
                    ["rnaType"] = "Object",
                    ["valueType"] = "array_buffer",
                    ["elementType"] = "float32",
                    ["count"] = 3,
                    ["shape"] = new JsonArray(3),
                }),
                RawPayload = BitConverter.GetBytes(1.0f)
                    .Concat(BitConverter.GetBytes(2.0f))
                    .Concat(BitConverter.GetBytes(3.0f))
                    .ToArray(),
            });
        var blenderApi = new BlenderApi(endpoint);

        var result = await blenderApi.Rna.ReadArrayAsync("bpy.data.objects[\"Cube\"].location");

        Assert.Equal("bpy.data.objects[\"Cube\"].location", result.Path);
        Assert.Equal("Object", result.RnaType);
        Assert.Equal("float32", result.ElementType);
        Assert.Equal(3, result.Count);
        Assert.Equal([3], result.Shape);
        Assert.Equal(12, result.RawBytes.Length);
        Assert.Equal("rna.read_array", endpoint.Requests[0].Name);
        Assert.Equal(
            "bpy.data.objects[\"Cube\"].location",
            endpoint.Requests[0].Payload.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Observe_WatchAsync_SubscribesAndDispatchesDirtyEvents()
    {
        var endpoint = new RecordingEndpoint(_ => BusinessRequest.Response(1, ToJsonElement(new JsonObject())));
        var blenderApi = new BlenderApi(endpoint);
        var received = new TaskCompletionSource<WatchDirtyEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await blenderApi.Observe.WatchAsync(
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

        await ((IBusinessEventSink)blenderApi).HandleEventAsync(
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
    public async Task Observe_WatchAsync_DirtyEventDispatch_DoesNotAwaitCallbackCompletion()
    {
        var endpoint = new RecordingEndpoint(_ => BusinessRequest.Response(1, ToJsonElement(new JsonObject())));
        var blenderApi = new BlenderApi(endpoint);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCallbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await blenderApi.Observe.WatchAsync(
            "live-transform",
            WatchSource.Depsgraph,
            "bpy.data.objects[\"Cube\"]",
            async _ =>
            {
                callbackStarted.TrySetResult();
                await allowCallbackCompletion.Task;
            });

        var handleTask = ((IBusinessEventSink)blenderApi).HandleEventAsync(
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
