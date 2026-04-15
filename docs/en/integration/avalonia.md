# C# API Usage Guide

This page explains how an Avalonia app can use the C# bridge API after the Blender-side and Avalonia-side integration is already connected.

If you have not wired both sides together yet, start with the [Integration Guide](./index.md).

## Available capabilities

The bridge exposes a path-first `IBlenderDataApi` that maps directly to the built-in Blender business protocol:

- `rna.list`
- `rna.get`
- `rna.set`
- `rna.describe`
- `rna.call`
- `ops.poll`
- `ops.call`
- `watch.subscribe`
- `watch.unsubscribe`
- `watch.read`

## Common surface area

The public C# surface keeps common reads, writes, and calls direct:

```csharp
Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default);
Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default);
Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default);
Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default);
Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs);
Task<OperatorPollResult> PollOperatorAsync(
    string operatorName,
    string operatorContext = "EXEC_DEFAULT",
    BlenderContextOverride? contextOverride = null,
    CancellationToken cancellationToken = default);
Task<OperatorCallResult> CallOperatorAsync(string operatorName, params BlenderNamedArg[] properties);
Task<IAsyncDisposable> WatchAsync(
    string watchId,
    WatchSource source,
    string path,
    Func<WatchDirtyEvent, Task> onDirty,
    CancellationToken cancellationToken = default);
```

## Typical data access

Typical usage stays close to Blender's Python API:

```csharp
public sealed class MaterialService
{
    private readonly IBlenderDataApi _blender;

    public MaterialService(IBlenderDataApi blender)
    {
        _blender = blender;
    }

    public Task<IReadOnlyList<RnaItemRef>> ListMaterialsAsync(CancellationToken ct = default)
        => _blender.ListAsync("bpy.data.materials", ct);

    public Task<string> GetMaterialNameAsync(string name, CancellationToken ct = default)
        => _blender.GetAsync<string>($"bpy.data.materials[\"{name}\"].name", ct);

    public Task RenameMaterialAsync(string name, string newName, CancellationToken ct = default)
        => _blender.SetAsync($"bpy.data.materials[\"{name}\"].name", newName, ct);

    public Task<RnaItemRef> CreateMaterialAsync(string newName, CancellationToken ct = default)
        => _blender.CallAsync<RnaItemRef>(
            "bpy.data.materials",
            "new",
            ("name", newName));
}
```

## Operator calls

Operator calls use tuple kwargs and optional strongly typed context override data instead of anonymous objects:

```csharp
await blender.CallOperatorAsync(
    "mesh.primitive_cube_add",
    ("size", 2.0));

await blender.CallOperatorAsync(
    "object.duplicate_move",
    new BlenderOperatorCall
    {
        ContextOverride = new BlenderContextOverride
        {
            ActiveObject = "bpy.data.objects[\"Cube\"]",
            SelectedObjects = new[] { "bpy.data.objects[\"Cube\"]" },
        }
    });
```

## Watch subscriptions

`WatchAsync` uses lightweight socket events plus explicit reads. Blender sends `watch.dirty`, and your app decides when to call `ReadWatchAsync`, `GetAsync`, or `ListAsync` again.

```csharp
await using var watch = await blender.WatchAsync(
    watchId: "materials",
    source: WatchSource.Depsgraph,
    path: "bpy.data.materials",
    onDirty: async _ =>
    {
        var materials = await blender.ListAsync("bpy.data.materials");
        // Refresh your UI here.
    });
```

## AOT and trimming notes

The built-in business SDK is designed to be AOT-safe:

- no anonymous-object kwargs or operator property bags
- no reflection fallback for `System.Text.Json`
- no arbitrary CLR object graph in business payloads

Supported value shapes are intentionally limited to the stable RNA surface:

- scalars: `bool`, `int`, `long`, `double`, `string`
- arrays: `bool[]`, `int[]`, `long[]`, `double[]`, `string[]`
- RNA references: `RnaItemRef`
- `null`

For custom DTOs used with `GetAsync<T>`, `SetAsync<T>`, or `CallAsync<T>`, register your own source-generated resolver at startup:

```csharp
using System.Text.Json.Serialization;
using BlenderAvaloniaBridge;

var options = new BlenderBridgeOptions();
options.DataApi.TypeInfoResolvers.Add(AppJsonContext.Default);

[JsonSerializable(typeof(MyDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}
```

Resolver lookup order is:

1. built-in bridge protocol types
2. resolvers registered through `BlenderBridgeOptions.DataApi.TypeInfoResolvers`
3. explicit failure with `missing_json_type_info_for_type`

If you need full Blender-side freedom, keep the default endpoint for standard RNA and operator traffic and add a separate custom business endpoint only for application-specific commands.
