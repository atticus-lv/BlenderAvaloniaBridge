using System.Text.Json;
using System.Text.Json.Nodes;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Runtime;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BusinessEndpointTests
{
    [Fact]
    public async Task InvokeAsync_WritesBusinessRequestAndCompletesWithBusinessResponse()
    {
        ProtocolEnvelope? sent = null;
        var endpoint = new RemoteBusinessEndpoint((envelope, cancellationToken) =>
        {
            sent = envelope;
            return Task.CompletedTask;
        });

        var invokeTask = endpoint.InvokeAsync(
            new BusinessRequest(
                "object.property.get",
                ToJsonElement(
                    new JsonObject
                    {
                        ["target"] = new JsonObject
                        {
                            ["rna_type"] = "bpy.types.Object",
                            ["name"] = "Cube",
                        },
                        ["data_path"] = "name",
                    }))).AsTask();

        Assert.NotNull(sent);
        Assert.Equal("business_request", sent!.Type);
        Assert.Equal(1, sent.BusinessVersion);
        Assert.Equal(1L, sent.MessageId);
        Assert.Equal("object.property.get", sent.Name);
        Assert.Equal("name", sent.Payload?.GetProperty("data_path").GetString());

        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                BusinessVersion = 1,
                MessageId = 2,
                ReplyTo = sent.MessageId,
                Ok = true,
                Payload = ToJsonElement(new JsonObject { ["value"] = "Cube" })
            });

        var response = await invokeTask;

        Assert.True(response.Ok);
        Assert.Equal(1, response.BusinessVersion);
        Assert.Equal(2L, response.MessageId);
        Assert.Equal(1L, response.ReplyTo);
        Assert.Equal("Cube", response.Payload?.GetProperty("value").GetString());
    }

    [Fact]
    public async Task InvokeAsync_CorrelatesMultiplePendingResponsesByReplyTo()
    {
        var sent = new List<ProtocolEnvelope>();
        var endpoint = new RemoteBusinessEndpoint((envelope, cancellationToken) =>
        {
            sent.Add(envelope);
            return Task.CompletedTask;
        });

        var firstTask = endpoint.InvokeAsync(
            new BusinessRequest("scene.objects.list", ToJsonElement(new JsonObject()))).AsTask();
        var secondTask = endpoint.InvokeAsync(
            new BusinessRequest("operator.call", ToJsonElement(new JsonObject { ["op"] = "mesh.primitive_cube_add" }))).AsTask();

        Assert.Equal(2, sent.Count);
        Assert.Equal(1L, sent[0].MessageId);
        Assert.Equal(2L, sent[1].MessageId);

        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                BusinessVersion = 1,
                MessageId = 10,
                ReplyTo = sent[1].MessageId,
                Ok = false,
                Error = new BusinessError("unsupported_business_request", "not allowed")
            });
        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                BusinessVersion = 1,
                MessageId = 11,
                ReplyTo = sent[0].MessageId,
                Ok = true,
                Payload = ToJsonElement(new JsonObject { ["items"] = new JsonArray() })
            });

        var second = await secondTask;
        var first = await firstTask;

        Assert.False(second.Ok);
        Assert.Equal("unsupported_business_request", second.Error?.Code);
        Assert.True(first.Ok);
        Assert.Equal(11L, first.MessageId);
    }

    private static JsonElement ToJsonElement(JsonNode node)
    {
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
