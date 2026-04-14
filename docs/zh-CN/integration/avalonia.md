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
