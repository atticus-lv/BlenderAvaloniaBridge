# Avalonia Integration

Use this path when you already have your own Avalonia application and want to add Blender bridge support.

The bridge SDK supports two runtime modes:

- `headless`: `frames + input + business`
- `desktop-business`: `business` only, hosted by a real Avalonia desktop window

## Current distribution model

The repository does not publish a NuGet package yet, so you need to build `BlenderAvaloniaBridge.Core` locally first.

## 1. Build the SDK

Run this from the repository root:

```powershell
dotnet build .\src\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj -c Release --configfile .\NuGet.Config
```

The build output is placed at:

```text
src\BlenderAvaloniaBridge.Core\bin\Release\net10.0\BlenderAvaloniaBridge.Core.dll
```

## 2. Choose an integration style

The recommended option is a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

If you only need a temporary integration, you can also reference the built DLL directly.

## 3. Choose a bridge mode

| Mode | Window Host | Frames | Input | Business | Use when |
| --- | --- | --- | --- | --- | --- |
| `headless` | Avalonia headless runtime | Yes | Yes | Yes | You want Blender to draw an overlay and forward input |
| `desktop-business` | Real Avalonia desktop window | No | No | Yes | You want to keep a real Avalonia window and only exchange business data |

The mode is selected explicitly through CLI bridge arguments, and the final active capabilities are confirmed in the `init` / `init ack` handshake.

## 4. Update your Program entry point

The recommended approach is to keep one shared entry point in `Program.cs` and branch automatically by `WindowMode`.

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

## 5. Use the built-in Blender data API

The bridge now exposes a path-first `IBlenderDataApi` that maps directly to the built-in Blender business protocol:

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

The public C# surface keeps the common reads and writes simple:

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

`WatchAsync` uses socket events plus explicit reads. Blender sends a lightweight `watch.dirty` event, and your app decides when to call `ReadWatchAsync`, `GetAsync`, or `ListAsync` again.

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

## 6. AOT and trimming notes

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
