# RNA

`blenderApi.Rna` is the path-based domain for reading and writing Blender state.

## Methods

- `ListAsync(path)`
- `GetAsync<T>(path)`
- `SetAsync<T>(path, value)`
- `DescribeAsync(path)`
- `CallAsync<T>(path, methodName, params (string Name, object? Value)[] kwargs)`

## List Collection Items

```csharp
var objects = await blenderApi.Rna.ListAsync("bpy.context.scene.objects", ct);
```

Each item is an `RnaItemRef` that can be used for follow-up path access.

## Read And Write Values

```csharp
var activeObject = await blenderApi.Rna.GetAsync<RnaItemRef>(
    "bpy.context.active_object",
    ct);

var objectType = await blenderApi.Rna.GetAsync<string>(
    $"{activeObject.Path}.type",
    ct);

await blenderApi.Rna.SetAsync(
    $"{activeObject.Path}.location",
    new[] { 0.0, 0.0, 1.0 },
    ct);
```

## Describe A Path

Use `DescribeAsync` when the UI needs metadata such as readonly state or declared type.

```csharp
var description = await blenderApi.Rna.DescribeAsync(
    "bpy.context.scene.frame_current",
    ct);
```

## Call RNA Methods

`blenderApi.Rna.CallAsync<T>` is for methods on RNA objects, not Blender operators.

```csharp
var material = await blenderApi.Rna.CallAsync<RnaItemRef>(
    "bpy.data.materials",
    "new",
    ("name", "PreviewMaterial"));
```

## Service Example

```csharp
public sealed class MaterialService
{
    private readonly BlenderApi _blenderApi;

    public MaterialService(BlenderApi blenderApi)
    {
        _blenderApi = blenderApi;
    }

    public Task<IReadOnlyList<RnaItemRef>> ListMaterialsAsync(CancellationToken ct = default)
        => _blenderApi.Rna.ListAsync("bpy.data.materials", ct);

    public Task<string> GetMaterialNameAsync(string name, CancellationToken ct = default)
        => _blenderApi.Rna.GetAsync<string>($"bpy.data.materials[\"{name}\"].name", ct);

    public Task RenameMaterialAsync(string name, string newName, CancellationToken ct = default)
        => _blenderApi.Rna.SetAsync($"bpy.data.materials[\"{name}\"].name", newName, ct);
}
```

Shared value and reference types are documented in [Types](./types.md).
