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
        ProtocolPacket? sent = null;
        var endpoint = new RemoteBusinessEndpoint((packet, cancellationToken) =>
        {
            sent = packet;
            return Task.CompletedTask;
        });

        var invokeTask = endpoint.InvokeAsync(
            new BusinessRequest(
                "rna.get",
                ToJsonElement(new JsonObject
                {
                    ["path"] = "bpy.data.objects[\"Cube\"].name",
                }))).AsTask();

        Assert.NotNull(sent);
        Assert.Equal("business_request", sent!.Header.Type);
        Assert.Equal(1, sent.Header.ProtocolVersion);
        Assert.Equal(1, sent.Header.SchemaVersion);
        Assert.Equal(1L, sent.Header.MessageId);
        Assert.Equal("rna.get", sent.Header.Name);
        Assert.Equal("bpy.data.objects[\"Cube\"].name", sent.Header.Payload?.GetProperty("path").GetString());
        Assert.Empty(sent.Payload);

        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                MessageId = 2,
                ReplyTo = sent.Header.MessageId,
                Ok = true,
                Payload = ToJsonElement(new JsonObject { ["value"] = "Cube" })
            });

        var response = await invokeTask;

        Assert.True(response.Ok);
        Assert.Equal(1, response.ProtocolVersion);
        Assert.Equal(1, response.SchemaVersion);
        Assert.Equal(2L, response.MessageId);
        Assert.Equal(1L, response.ReplyTo);
        Assert.Equal("Cube", response.Payload?.GetProperty("value").GetString());
    }

    [Fact]
    public async Task InvokeAsync_CorrelatesMultiplePendingResponsesByReplyTo()
    {
        var sent = new List<ProtocolPacket>();
        var endpoint = new RemoteBusinessEndpoint((packet, cancellationToken) =>
        {
            sent.Add(packet);
            return Task.CompletedTask;
        });

        var firstTask = endpoint.InvokeAsync(
            new BusinessRequest("rna.list", ToJsonElement(new JsonObject { ["path"] = "bpy.data.materials" }))).AsTask();
        var secondTask = endpoint.InvokeAsync(
            new BusinessRequest("ops.call", ToJsonElement(new JsonObject { ["operator"] = "mesh.primitive_cube_add" }))).AsTask();

        Assert.Equal(2, sent.Count);
        Assert.Equal(1L, sent[0].Header.MessageId);
        Assert.Equal(2L, sent[1].Header.MessageId);
        Assert.All(sent, packet => Assert.Empty(packet.Payload));

        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                MessageId = 10,
                ReplyTo = sent[1].Header.MessageId,
                Ok = false,
                Error = new BusinessError("unsupported_business_request", "not allowed")
            });
        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                MessageId = 11,
                ReplyTo = sent[0].Header.MessageId,
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

    [Fact]
    public async Task InvokeAsync_CompletesWithBusinessResponseBinaryPayload()
    {
        ProtocolPacket? sent = null;
        var endpoint = new RemoteBusinessEndpoint((packet, cancellationToken) =>
        {
            sent = packet;
            return Task.CompletedTask;
        });

        var invokeTask = endpoint.InvokeAsync(
            new BusinessRequest(
                "rna.read_array",
                ToJsonElement(new JsonObject
                {
                    ["path"] = "bpy.data.materials[\"Mat_A\"].preview.icon_pixels",
                }))).AsTask();

        Assert.NotNull(sent);
        Assert.Equal("business_request", sent!.Header.Type);
        Assert.Empty(sent.Payload);

        endpoint.HandleResponse(
            new ProtocolEnvelope
            {
                Type = "business_response",
                ProtocolVersion = 1,
                SchemaVersion = 1,
                MessageId = 3,
                ReplyTo = sent.Header.MessageId,
                Ok = true,
                Payload = ToJsonElement(new JsonObject
                {
                    ["path"] = "bpy.data.materials[\"Mat_A\"].preview.icon_pixels",
                    ["valueType"] = "array_buffer",
                    ["elementType"] = "uint8",
                    ["count"] = 4,
                })
            },
            [1, 2, 3, 4]);

        var response = await invokeTask;

        Assert.Equal([1, 2, 3, 4], response.RawPayload);
        Assert.Equal("uint8", response.Payload?.GetProperty("elementType").GetString());
    }

    private static JsonElement ToJsonElement(JsonNode node)
    {
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
