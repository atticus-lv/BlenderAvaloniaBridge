# Avalonia 集成

这条路径适合你已经有自己的 Avalonia 应用，想把 Blender 桥接能力接进去。

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

## 3. 修改 Program 入口

你需要在自己的 `Program.cs` 里显式处理 bridge 模式，并手动提供桥接窗口创建逻辑：

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

## 4. 可选扩展点

如果你想做更深的集成，可以在自己的窗口或 ViewModel 中实现：

- `IBusinessEndpointSink`
- `IBlenderBridgeStatusSink`

这样可以拿到统一的 business endpoint，并接收桥接状态更新。
