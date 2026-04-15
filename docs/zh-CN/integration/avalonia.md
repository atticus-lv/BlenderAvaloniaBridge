# C# API 使用指南

本页说明 Avalonia 程序在 bridge 已经接通之后，如何通过 C# API 访问 Blender 数据和 operator。

如果你还没有完成 Blender 侧与 Avalonia 侧的整体接线，请先看[集成指南](./index.md)。

## 可用能力

bridge 会直接暴露一个 Path-first 的 `IBlenderDataApi`，它对应内置的 Blender 通用业务协议：

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

## 常用接口

对外的 C# 接口保持为直接的读写与调用模型：

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

## 典型数据读写

典型用法会尽量贴近 Blender Python API：

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

## Operator 调用

operator 调用使用 tuple kwargs 和强类型 `contextOverride`，不再依赖匿名对象：

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

## Watch 订阅

`WatchAsync` 采用轻量事件通知加按需读取的模型。Blender 侧会发送 `watch.dirty`，然后由 C# 侧决定是否执行 `ReadWatchAsync`、`GetAsync` 或 `ListAsync`。

```csharp
await using var watch = await blender.WatchAsync(
    watchId: "materials",
    source: WatchSource.Depsgraph,
    path: "bpy.data.materials",
    onDirty: async _ =>
    {
        var materials = await blender.ListAsync("bpy.data.materials");
        // 在这里刷新 UI。
    });
```

## AOT 与 trimming 说明

内置 business SDK 现在按严格 AOT-safe 设计：

- 不再支持匿名对象 kwargs / operator property bag
- 不提供 `System.Text.Json` 的反射回退
- 不接收任意 CLR 对象图作为 business payload

支持的值模型会明确收敛到稳定的 RNA 类型面：

- 标量：`bool`、`int`、`long`、`double`、`string`
- 数组：`bool[]`、`int[]`、`long[]`、`double[]`、`string[]`
- RNA 引用：`RnaItemRef`
- `null`

如果你要在 `GetAsync<T>`、`SetAsync<T>` 或 `CallAsync<T>` 中使用自定义 DTO，需要在启动时注册自己的 source-generated resolver：

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

运行时的解析顺序固定为：

1. bridge 内建协议类型
2. `BlenderBridgeOptions.DataApi.TypeInfoResolvers` 中注册的 resolver
3. 若仍未找到，则抛出 `missing_json_type_info_for_type`

如果你需要完全自定义的 Blender 侧能力，建议保留默认 endpoint 处理标准的 RNA/operator 流量，只把应用私有命令放进额外的自定义 business endpoint。
