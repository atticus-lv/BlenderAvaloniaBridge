# Observe

`blenderApi.Observe` 负责 watch 订阅和显式快照读取。

它采用轻量通知模型：

1. Blender 发送 `watch.dirty`。
2. Avalonia 程序自己决定刷新什么。
3. 程序再通过 `blenderApi.Observe.ReadAsync`、`blenderApi.Rna.GetAsync` 或 `blenderApi.Rna.ListAsync` 拉取新状态。

## 方法

- `WatchAsync(watchId, source, path, onDirty, cancellationToken)`
- `ReadAsync(watchId, cancellationToken)`

## 建立 Watch

```csharp
await using var watch = await blenderApi.Observe.WatchAsync(
    watchId: "materials",
    source: WatchSource.Depsgraph,
    path: "bpy.data.materials",
    onDirty: async _ =>
    {
        var materials = await blenderApi.Rna.ListAsync("bpy.data.materials");
        // 在这里刷新 UI。
    });
```

## 选择刷新策略

- 如果整个列表重新拉取成本不高，直接再次调用 `blenderApi.Rna.ListAsync(...)`
- 如果详情面板绑定在明确路径上，调用 `blenderApi.Rna.GetAsync(...)`
- 如果你需要 watch 快照和 dirty 引用一起参与判断，使用 `blenderApi.Observe.ReadAsync(...)`

## WatchSource

watch API 统一归在 `Observe` 下，具体来源仍通过 `WatchSource` 选择：

- `WatchSource.Depsgraph`
- `WatchSource.Frame`
- `WatchSource.Lifecycle`
