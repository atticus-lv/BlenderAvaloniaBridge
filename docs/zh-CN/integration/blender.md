# Blender 集成

这条路径适合你已经有自己的 Blender 扩展，只想把桥接 core 拿过去复用。

## 复制内容

复制下面这个目录到你自己的扩展包中：

```text
src\blender_extension\avalonia_bridge\core\
```

不需要一起复制的仓库壳层文件（但是可用于参考）



## 最小接入代码

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
# run
controller.start()
```



## 传输模式

传输模式有两种`window_mode`：

- `headless`：默认，将avalonia的帧画面传输至blender 窗口（默认在3dviewport绘制），区域内将捕捉鼠标/键盘事件
- `desktop`：经典的桌面窗口模式，只进行business连接



## 尺寸和渲染倍率

- `width` 和 `height` 控制 Avalonia 的逻辑窗口尺寸
- `render_scaling` 控制 headless 截图时的渲染倍率

`render_scaling`用于在高分辨率屏幕上保持和desktop模式一致的ui布局。 仅适用于 headless 模式。

在 sample 扩展的 UI 里，这两个参数对应：

- `Display Size`
- `Render Scaling`

默认 `render_scaling` 是 `1.25`

在 Windows 和 macOS 上，headless 帧传输默认走共享内存。桥接会继续复用现有 `shm_name`、`frame_size`、`slot_count` 握手字段，但一般不需要把“是否使用共享内存”单独暴露到扩展 UI。



## 接入点

- 生命周期：在自己的 operator 或 runtime adapter 中调用 `start()`、`stop()`、`tick_once()`
- 输入转发：当远程输入启用时，在自己的事件处理里调用 `handle_event(context, event)`
- 状态同步：通过 `state_callback` 或 `state_snapshot()` / `diagnostics_snapshot()` 把状态映射到你的 UI
- 用户可调参数：在 headless 模式下，建议把 `width`、`height` 和 `render_scaling` 一起暴露出来，让布局尺寸和清晰度分别可调
- capability 感知 UI：建议把当前 session 是 business-only、frame streaming 还是 input enabled 明确展示出来

共享的 session 模型和 capability 协商流程可以参考 [项目架构](../advanced/architecture.md)。
