# 集成指南

本页说明如何把 Blender Avalonia Bridge 接入已有项目。

如果你只是想先运行仓库自带示例，请看[快速开始](../guide/quick-start.md)。

完整接入通常包含两部分改动：

- Avalonia 程序负责 UI、状态和业务逻辑，并作为 bridge 进程启动
- Blender 扩展负责启动 bridge、绘制 overlay、转发输入和承载业务通道



## 运行模式

bridge SDK 当前支持两种运行模式：

- `headless`：`frames + input + business`
- `desktop-business`：只保留 `business`，由真实 Avalonia 桌面窗口承载

如果你希望 UI 直接绘制在 Blender 中，使用 `headless`。

如果你希望继续保留真实 Avalonia 窗口，只把业务能力接入 Blender，使用 `desktop-business`。

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

可以直接引用编译后的 DLL，也可以在自己的 Avalonia 项目中使用项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="..\BlenderAvaloniaBridge.Core\BlenderAvaloniaBridge.Core.csproj" />
</ItemGroup>
```

## 2. 修改 Avalonia 程序入口

推荐在 `Program.cs` 中保留统一入口，根据 bridge 传入参数自动选择普通桌面启动或 bridge 启动。

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

模式通过 CLI bridge 参数显式选择，最终生效的 capability 会在 `init` / `init ack` 握手里确认。

## 3. 复制 Blender 侧 bridge core

把下面这个目录复制到你自己的 Blender 扩展包中：

```text
src\blender_extension\avalonia_bridge\core\
```

然后在自己的 operator 或 runtime adapter 中接入 `BridgeController`。

最小接入代码示例：

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="/path/to/YourAvaloniaApp.dll",
        width=1100,
        height=760,
        render_scaling=1.25,
        window_mode="headless",
        supports_business=True,
        supports_frames=True,
        supports_input=True,
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)

controller.start()
```

## 4. 配置 Blender 侧运行参数

`window_mode` 和 bridge SDK的运行模式一致：

- `headless`：默认模式，将 Avalonia 帧画面传输到 Blender 窗口中，通常绘制在 `3D Viewport` 内，并在该区域捕捉鼠标和键盘事件
- `desktop`：经典桌面窗口模式，只建立 business 连接

尺寸与清晰度相关参数：

- `width` 和 `height` 控制 Avalonia 的逻辑窗口尺寸
- `render_scaling` 控制 headless 截图时的渲染倍率

`render_scaling` 用于在高分辨率屏幕上保持和 desktop 模式更接近的 UI 清晰度与布局观感，只对 headless 模式生效。

在 Windows 和 macOS 上，headless 帧传输默认使用共享内存。桥接会继续复用 `shm_name`、`frame_size`、`slot_count` 等握手字段，但一般不需要把“是否使用共享内存”单独暴露到扩展 UI。

## 5. 接入扩展生命周期和输入转发

Blender 侧需要接入下面几个点：

- 生命周期：在自己的 operator 或 runtime adapter 中调用 `start()`、`stop()`、`tick_once()`
- 输入转发：当远程输入启用时，在自己的事件处理里调用 `handle_event(context, event)`
- 状态同步：通过 `state_callback` 或 `state_snapshot()` / `diagnostics_snapshot()` 把状态映射到自己的 UI
- 用户可调参数：在 headless 模式下，建议把 `width`、`height` 和 `render_scaling` 一起暴露出来，让布局尺寸和清晰度分别可调
- capability 感知 UI：建议把当前 session 是 business-only、frame streaming 还是 input enabled 明确展示出来

共享的 session 模型和 capability 协商流程可以参考 [项目架构](../advanced/architecture.md)。

## 6. C# 侧业务调用

接入完成后，Avalonia 程序可以通过内置的 `BlenderApi` 根对象访问 Blender 数据、operator 和 watch 能力，对外入口分别是 `Rna`、`Ops` 和 `Observe`。

这部分建议单独阅读：[API 章节](../api/index.md)。
