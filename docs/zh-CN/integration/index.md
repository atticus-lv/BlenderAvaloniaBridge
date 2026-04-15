# 集成指南

本页说明如何把 Blender Avalonia Bridge 接入已有项目。

如果你只是想先运行仓库自带示例，请看[快速开始](../guide/quick-start.md)。

完整接入通常包含两部分改动：

- Avalonia 程序负责 UI、状态和业务逻辑，并作为 bridge 进程启动
- Blender 扩展负责启动 bridge、绘制 overlay、转发输入和承载业务通道



## 公共配置

`window_mode` 和 bridge SDK 的运行模式一致：

- `headless`：默认模式，启用 `frames + input + business`
- `desktop`：经典桌面窗口模式，只建立 `business` 连接

尺寸与清晰度相关参数：

- `width` 和 `height` 控制 Avalonia 的逻辑窗口尺寸
- `render_scaling` 控制 headless 截图时的渲染倍率

`render_scaling` 只对 headless 模式生效，用于在高分辨率屏幕上保持更接近 desktop 模式的清晰度与布局观感。

模式通过 CLI bridge 参数显式选择，最终生效的 capability 会在 `init` / `init ack` 握手里确认。

在 Windows 和 macOS 上，headless 帧传输默认使用共享内存。桥接会继续复用 `shm_name`、`frame_size`、`slot_count` 等握手字段，但一般不需要把“是否使用共享内存”单独暴露到扩展 UI。

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

## 3. 复制 Blender 侧 bridge core

把下面这个目录复制到你自己的 Blender 扩展包中：

```text
src\blender_extension\avalonia_bridge\core\
```

Blender 侧结构：

- `BridgeController` 只负责 bridge core：进程生命周期、传输、frame pipeline、business packet、状态与诊断
- `View3DOverlayHost` 负责可选的 `3D View` 展示与输入适配：overlay 绘制、标题栏拖拽、hit-test、输入转发与 redraw

最小组装代码示例：

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
    View3DOverlayHost,
)


config = BridgeConfig(
    executable_path="/path/to/YourAvaloniaApp",
    width=1100,
    height=760,
    render_scaling=1.25,
    window_mode="headless",
    supports_business=True,
    supports_frames=True,
    supports_input=True,
    host="127.0.0.1",
    show_overlay_debug=False,
)

presentation_host = View3DOverlayHost() if config.supports_frames else None

controller = BridgeController(
    config,
    host=presentation_host,
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)

controller.start()
```

- `headless`：`BridgeController(..., host=View3DOverlayHost(...))`
- `desktop` / business-only：`BridgeController(..., host=None)`

## 4. 接入扩展生命周期和输入转发

Blender 侧通常在 runtime adapter 和 modal operator 两层接入：

- runtime adapter：根据模式构造 `BridgeConfig`，并组装 `BridgeController` 与可选的 `View3DOverlayHost`
- modal operator：在 `TIMER` 中调用 `tick_once()`，在事件链路中调用 `handle_event(context, event)`
- 状态同步：通过 `state_callback` 或 `state_snapshot()` / `diagnostics_snapshot()` 把状态映射到自己的 UI
- 用户可调参数：在 headless 模式下，建议把 `width`、`height` 和 `render_scaling` 一起暴露出来，让布局尺寸和清晰度分别可调

runtime 组装示例：

```python
def create_controller(mode: str) -> BridgeController:
    config = BridgeConfig(
        executable_path="/path/to/YourAvaloniaApp",
        width=1100,
        height=760,
        render_scaling=1.25,
        window_mode=mode,
        supports_business=True,
        supports_frames=mode != "desktop",
        supports_input=mode != "desktop",
        host="127.0.0.1",
        show_overlay_debug=False,
    )
    host = View3DOverlayHost() if config.supports_frames else None
    return BridgeController(
        config,
        host=host,
        business_endpoint=DefaultBusinessEndpoint(),
    )
```

最小 modal operator 示例：

```python
import bpy


class BRIDGE_OT_start(bpy.types.Operator):
    bl_idname = "your_addon.bridge_start"
    bl_label = "Start Bridge"

    def execute(self, context):
        controller = create_controller(mode="headless")
        context.window_manager.your_bridge_controller = controller
        controller.start()
        bpy.ops.your_addon.bridge_modal("INVOKE_DEFAULT")
        return {"FINISHED"}


class BRIDGE_OT_modal(bpy.types.Operator):
    bl_idname = "your_addon.bridge_modal"
    bl_label = "Bridge Modal"
    bl_options = {"BLOCKING"}

    _timer = None

    def invoke(self, context, _event):
        self._timer = context.window_manager.event_timer_add(1.0 / 60.0, window=context.window)
        context.window_manager.modal_handler_add(self)
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        controller = getattr(context.window_manager, "your_bridge_controller", None)
        if controller is None:
            self.cancel(context)
            return {"CANCELLED"}

        if not controller.state_snapshot().process_running:
            self.cancel(context)
            return {"CANCELLED"}

        if event.type == "TIMER":
            controller.tick_once()
            return {"RUNNING_MODAL"}

        if context.area and context.area.type == "VIEW_3D":
            if controller.handle_event(context, event):
                return {"RUNNING_MODAL"}

        return {"PASS_THROUGH"}

    def cancel(self, context):
        controller = getattr(context.window_manager, "your_bridge_controller", None)
        if controller is not None:
            controller.stop()
            context.window_manager.your_bridge_controller = None
        if self._timer is not None:
            context.window_manager.event_timer_remove(self._timer)
            self._timer = None
```

共享的 session 模型和 capability 协商流程可以参考 [项目架构](../advanced/architecture.md)。

## 5. C# 侧业务调用

接入完成后，Avalonia 程序可以通过内置的 `BlenderApi` 根对象访问 Blender 数据、operator 和 watch 能力，对外入口分别是 `Rna`、`Ops` 和 `Observe`。

这部分建议单独阅读：[API 章节](../api/index.md)。
