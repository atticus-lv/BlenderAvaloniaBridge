# RNA

`blenderApi.Rna` 是按路径访问 Blender 状态的领域入口。

## 方法

- `ListAsync(path)`
- `GetAsync<T>(path)`
- `SetAsync<T>(path, value)`
- `DescribeAsync(path)`
- `CallAsync<T>(path, methodName, params (string Name, object? Value)[] kwargs)`

## 列举集合项

```csharp
var objects = await blenderApi.Rna.ListAsync("bpy.context.scene.objects", ct);
```

每一项都是 `RnaItemRef`，可以继续用于后续路径访问。

## 读取和写入值

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

## 读取路径描述

当 UI 需要知道只读状态、声明类型等元数据时，使用 `DescribeAsync`。

```csharp
var description = await blenderApi.Rna.DescribeAsync(
    "bpy.context.scene.frame_current",
    ct);
```

## 调用 RNA 方法

`blenderApi.Rna.CallAsync<T>` 用来调用 RNA 对象上的方法，不用于 Blender operator。

```csharp
var material = await blenderApi.Rna.CallAsync<RnaItemRef>(
    "bpy.data.materials",
    "new",
    ("name", "PreviewMaterial"));
```

## 服务示例

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

共享值类型和引用类型请看 [Types](./types.md)。
