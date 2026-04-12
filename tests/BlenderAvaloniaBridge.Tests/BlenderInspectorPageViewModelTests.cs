using System.Text.Json;
using System.Text.Json.Nodes;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Sample.ViewModels;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderInspectorPageViewModelTests
{
    [Fact]
    public async Task AttachBusinessEndpoint_WhenInitialRefreshFails_StoresErrorStatus()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var endpoint = new DelegateBusinessEndpoint(request =>
        {
            if (request.Name == "scene.objects.list")
            {
                throw new InvalidOperationException("refresh failed");
            }

            return new ValueTask<BusinessResponse>(BusinessRequest.Response(request.MessageId));
        });

        viewModel.AttachBusinessEndpoint(endpoint);

        await WaitForAsync(() => viewModel.StatusText == "refresh failed");

        Assert.Equal("refresh failed", viewModel.StatusText);
    }

    [Fact]
    public async Task CommitNameAsync_WhenEndpointThrows_DoesNotPropagateAndStoresErrorStatus()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var endpoint = new DelegateBusinessEndpoint(request =>
        {
            return request.Name switch
            {
                "scene.objects.list" => new ValueTask<BusinessResponse>(
                    BusinessRequest.Response(
                        request.MessageId,
                        ToJsonElement(new JsonObject { ["items"] = new JsonArray() }))),
                "object.property.set" => ValueTask.FromException<BusinessResponse>(new InvalidOperationException("name set failed")),
                _ => new ValueTask<BusinessResponse>(BusinessRequest.Response(request.MessageId))
            };
        });

        viewModel.AttachBusinessEndpoint(endpoint);
        viewModel.SelectedObject = new BlenderObjectListItem(
            new BlenderRnaRef
            {
                RnaType = "bpy.types.Object",
                Name = "Cube",
                SessionUid = 1,
            },
            "Cube",
            "MESH",
            true);
        viewModel.ObjectName = "Cube.001";

        var exception = await Record.ExceptionAsync(() => viewModel.CommitNameAsync());

        Assert.Null(exception);
        Assert.Equal("name set failed", viewModel.StatusText);
    }

    [Fact]
    public async Task AttachBusinessEndpoint_WhenInitialRefreshSucceeds_PopulatesObjects()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var endpoint = new DelegateBusinessEndpoint(request =>
        {
            return request.Name switch
            {
                "scene.objects.list" => new ValueTask<BusinessResponse>(
                    BusinessRequest.Response(
                        request.MessageId,
                        ToJsonElement(
                            new JsonObject
                            {
                                ["items"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["rna_ref"] = new JsonObject
                                        {
                                            ["rna_type"] = "bpy.types.Object",
                                            ["id_type"] = "OBJECT",
                                            ["name"] = "Cube",
                                            ["session_uid"] = 7,
                                        },
                                        ["label"] = "Cube",
                                        ["meta"] = new JsonObject
                                        {
                                            ["object_type"] = "MESH",
                                            ["is_active"] = true,
                                        }
                                    }
                                }
                            }))),
                "object.property.get" => new ValueTask<BusinessResponse>(
                    BusinessRequest.Response(
                        request.MessageId,
                        ToJsonElement(
                            new JsonObject
                            {
                                ["target"] = new JsonObject
                                {
                                    ["rna_type"] = "bpy.types.Object",
                                    ["id_type"] = "OBJECT",
                                    ["name"] = "Cube",
                                    ["session_uid"] = 7,
                                },
                                ["data_path"] = "name",
                                ["value"] = "Cube",
                            }))),
                _ => new ValueTask<BusinessResponse>(BusinessRequest.Response(request.MessageId))
            };
        });

        viewModel.AttachBusinessEndpoint(endpoint);

        await WaitForAsync(() => viewModel.Objects.Count == 1);

        Assert.Equal("Cube", viewModel.Objects[0].Label);
        Assert.Equal("MESH", viewModel.Objects[0].ObjectType);
        Assert.Equal("bpy.types.Object", viewModel.SelectedObject?.RnaRef.RnaType);
    }

    [Fact]
    public async Task CommitNameAsync_WhenPropertySetSucceeds_UpdatesSelectedObjectFromPayload()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var endpoint = new DelegateBusinessEndpoint(request =>
        {
            return request.Name switch
            {
                "scene.objects.list" => new ValueTask<BusinessResponse>(
                    BusinessRequest.Response(
                        request.MessageId,
                        ToJsonElement(new JsonObject { ["items"] = new JsonArray() }))),
                "object.property.set" => new ValueTask<BusinessResponse>(
                    BusinessRequest.Response(
                        request.MessageId,
                        ToJsonElement(
                            new JsonObject
                            {
                                ["target"] = new JsonObject
                                {
                                    ["rna_type"] = "bpy.types.Object",
                                    ["id_type"] = "OBJECT",
                                    ["name"] = "Cube.001",
                                    ["session_uid"] = 9,
                                },
                                ["data_path"] = "name",
                                ["value"] = "Cube.001",
                            }))),
                _ => new ValueTask<BusinessResponse>(BusinessRequest.Response(request.MessageId))
            };
        });

        viewModel.AttachBusinessEndpoint(endpoint);
        viewModel.SelectedObject = new BlenderObjectListItem(
            new BlenderRnaRef
            {
                RnaType = "bpy.types.Object",
                Name = "Cube",
                SessionUid = 9,
            },
            "Cube",
            "MESH",
            true);
        viewModel.ObjectName = "Cube.001";

        await viewModel.CommitNameAsync();

        Assert.Equal("Cube.001", viewModel.ObjectName);
        Assert.Equal("Cube.001", viewModel.SelectedObject?.Label);
        Assert.Equal("Cube.001", viewModel.SelectedObject?.RnaRef.Name);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class DelegateBusinessEndpoint : IBusinessEndpoint
    {
        private readonly Func<BusinessRequest, ValueTask<BusinessResponse>> _handler;

        public DelegateBusinessEndpoint(Func<BusinessRequest, ValueTask<BusinessResponse>> handler)
        {
            _handler = handler;
        }

        public ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default)
        {
            return _handler(request);
        }
    }

    private static JsonElement ToJsonElement(JsonNode node)
    {
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
