# Avalonia 集成

这条路径适合你已经有自己的 Avalonia 应用，想把 Blender 桥接能力接进去。

bridge SDK 支持两种运行模式：

- `headless`：`frames + input + business`
- `desktop-business`：只保留 `business`，由真实 Avalonia 桌面窗口承载

## 当前分发方式

目前仓库还没有发布 NuGet 包，所以需要你先本地构建 `BlenderAvaloniaBridge.Core`。

## 1. 构建 SDK

在仓库根目录运行：

```powershell
dotnet build .\src\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj -c Release --configfile .\NuGet.Config
```

构建产物默认位于：

```text
src\BlenderAvaloniaBridge.Core\bin\Release\net10.0\BlenderAvaloniaBridge.Core.dll
```

## 2. 选择集成方式

推荐方式是直接项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

如果你只是做临时接入，也可以直接引用编译后的 DLL。

## 3. 选择 bridge 模式

| 模式 | 窗口宿主 | Frames | Input | Business | 适用场景 |
| --- | --- | --- | --- | --- | --- |
| `headless` | Avalonia 无头运行时 | Yes | Yes | Yes | 需要 Blender 绘制 overlay 并转发输入 |
| `desktop-business` | 真实 Avalonia 桌面窗口 | No | No | Yes | 保留真实 Avalonia 窗口，只交换业务数据 |

模式通过 CLI bridge 参数显式选择，最终生效的 capability 会在 `init` / `init ack` 握手里确认。

## 4. 修改入口代码

推荐做法是在 `Program.cs` 中保留一套通用入口，根据 `WindowMode` 自动走不同分支。

```csharp
using Avalonia;
using BlenderAvaloniaBridge;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        var launch = BlenderBridgeLauncher.TryParse(args);

        if (launch.IsBridgeMode)
        {
            var options = launch.GetRequiredBridgeOptions();

            if (options.WindowMode == BridgeWindowMode.Desktop)
            {
                DesktopBridgeLaunchContext.Configure(options);
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(launch.AppArgs);
                return 0;
            }

            await BlenderBridgeLauncher.RunBridgeAsync(
                launch,
                createBridgeWindow: () => new MainWindow());
            return 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(launch.AppArgs);
        return 0;
    }

public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

## 5. 使用内置 Blender 数据 API

现在 bridge 会直接暴露一个 Path-first 的 `IBlenderDataApi`，它对应内置的 Blender 通用业务协议：

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

对外的 C# 接口仍然保持常见读写足够直接：

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

operator 调用改成 tuple kwargs 加强类型 `contextOverride`，不再依赖匿名对象：

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

`WatchAsync` 采用 socket 小事件加按需读取的模型。Blender 侧会发送轻量 `watch.dirty`，然后由 C# 侧决定是否执行 `ReadWatchAsync`、`GetAsync` 或 `ListAsync`。

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

## 6. AOT 与 trimming 说明

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
