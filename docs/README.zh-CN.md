# Blender Avalonia Bridge

Windows 优先的工具套件，用于让 Avalonia UI 运行在独立进程中，把画面流传到 Blender，并把 Blender 输入事件回传给 Avalonia。

中文 | [English](E:/blender_ava_demo/README.md)

## 选择一种用法

### 推荐集成方式

适用于你同时维护自己的 Avalonia 项目和自己的 Blender 扩展。

1. 在你的 Avalonia 项目中集成 `BlenderAvaloniaBridge.Core`。
2. 把 `src/blender_extension/avalonia_bridge/core/` 复制到你自己的 Blender 扩展里。
3. 按需接入你自己的消息处理或业务处理器。

Avalonia 侧：

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

Blender 侧：

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessBridgeHandler,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="C:/path/to/YourAvaloniaApp.exe",
        width=1100,
        height=760,
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_handler=DefaultBusinessBridgeHandler(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)
```

需要复制到自己 Blender 扩展中的内容：

- `src/blender_extension/avalonia_bridge/core/`

不需要复制的内容：

- `src/blender_extension/avalonia_bridge/panel.py`
- `src/blender_extension/avalonia_bridge/operators.py`
- `src/blender_extension/avalonia_bridge/preferences.py`
- `src/blender_extension/avalonia_bridge/properties.py`
- `src/blender_extension/avalonia_bridge/runtime.py`

可选扩展点：

- Avalonia 侧：`IBlenderBridgeMessageHost`、`IBlenderBridgeStatusSink`
- Blender 侧：`BusinessBridgeHandler`、`DefaultBusinessBridgeHandler`

Bridge CLI 参数统一加前缀，避免和你自己的应用参数冲突：

- `--blender-bridge true`
- `--blender-bridge-host 127.0.0.1`
- `--blender-bridge-port 34567`
- `--blender-bridge-width 1100`
- `--blender-bridge-height 760`

### 快速试水

适用于你想先验证桥接能力，但暂时不想自己写 Blender 扩展。

1. 在你的 Avalonia 项目中集成 `BlenderAvaloniaBridge.Core`。
2. 安装当前仓库里的 `src/blender_extension/avalonia_bridge/` Blender 扩展。
3. 在 Blender 中把 `Avalonia Path` 指向你的 Avalonia 可执行文件。
4. 点击 `Start UI Bridge`。

这个路径最适合快速验证：

- 进程启动
- 连接建立
- 画面传输
- Blender 侧 overlay 与输入转发

### 预览当前示例

适用于你只是想先把仓库跑起来。

1. 编译当前仓库里的 Avalonia sample 项目。
2. 安装当前仓库里的 `src/blender_extension/avalonia_bridge/` Blender 插件。
3. 在插件里把路径指向 sample 生成的 exe。
4. 点击 `Start UI Bridge`。

## 仓库结构

```text
src/
  BlenderAvaloniaBridge.Core/            可复用的 Avalonia SDK
  BlenderAvaloniaBridge.Sample/          sample app 和 demo UI
  blender_extension/
    avalonia_bridge/                     当前仓库使用的 Blender 扩展壳层
      core/                              可复制的 Blender bridge core 包
tests/
  BlenderAvaloniaBridge.Tests/
  blender_extension/avalonia_bridge/
```

## 构建

在仓库根目录运行。

Restore：

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '.').Path
dotnet restore .\BlenderAvaloniaBridge.slnx --configfile .\NuGet.Config
```

编译 sample：

```powershell
dotnet build .\BlenderAvaloniaBridge.slnx -c Debug
```

发布 Release exe：

```powershell
dotnet build .\BlenderAvaloniaBridge.slnx -c Release
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\release-net10 --configfile .\NuGet.Config
```

发布 AOT exe：

```powershell
dotnet publish .\src\BlenderAvaloniaBridge.Sample\BlenderAvaloniaBridge.Sample.csproj -c Release -r win-x64 -p:PublishAot=true -o .\artifacts\publish\aot-net10 --configfile .\NuGet.Config
```

常见可执行文件路径：

- `src\BlenderAvaloniaBridge.Sample\bin\Debug\net10.0\BlenderAvaloniaBridge.Sample.exe`
- `artifacts\publish\release-net10\BlenderAvaloniaBridge.Sample.exe`
- `artifacts\publish\aot-net10\BlenderAvaloniaBridge.Sample.exe`

推荐给 Blender 插件配置的目标：

- AOT exe，性能和内存表现更好

## Blender 扩展安装

1. 安装 `src/blender_extension/avalonia_bridge/` 文件夹作为 Blender 扩展。
2. 打开 `View3D > Sidebar > RenderBuilder`。
3. 设置 `Avalonia Path`。
4. 点击 `Start UI Bridge`。

## 更多文档

- 架构与协议说明：[ARCHITECTURE.md](E:/blender_ava_demo/docs/ARCHITECTURE.md)
- 中文架构说明：[ARCHITECTURE.zh-CN.md](E:/blender_ava_demo/docs/ARCHITECTURE.zh-CN.md)

## 当前限制

- Windows 优先
- 共享内存路径目前仅支持 Windows
- 每次启动时桥接尺寸固定
- 暂不支持 IME / 剪贴板 / 拖放
- Blender 后台模式不适合做 GPU overlay 测试
