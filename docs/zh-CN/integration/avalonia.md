# Avalonia 侧接入

## 1. 构建并引用 SDK

目前仓库还没有发布 NuGet 包，所以需要先本地构建 `BlenderAvaloniaBridge.Core`。

在仓库根目录运行：

```powershell
dotnet build .\src\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj -c Release --configfile .\NuGet.Config
```

构建产物默认位于：

```text
src\BlenderAvaloniaBridge.Core\bin\Release\net10.0\BlenderAvaloniaBridge.Core.dll
```

项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

也可以直接引用编译后的 DLL。

## 2. 修改程序入口

在 `Program.cs` 中保留统一入口，根据 bridge 参数选择普通桌面启动或 bridge 启动。

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

## 3. C# 侧业务调用

接入完成后，Avalonia 程序可以通过内置的 `BlenderApi` 根对象访问 Blender 数据、operator 和 watch 能力，对外入口分别是 `Rna`、`Ops` 和 `Observe`。

这部分请看 [API 章节](../api/index.md)。

## 下一步

- 返回[集成概览](./index.md)
- 继续看 [Blender 侧接入](./blender.md)
