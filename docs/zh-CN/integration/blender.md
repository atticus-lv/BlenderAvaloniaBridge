# Blender 集成

这条路径适合你已经有自己的 Blender 扩展，只想把桥接 core 拿过去复用。

## 复制内容

复制下面这个目录到你自己的扩展包中：

```text
src\blender_extension\avalonia_bridge\core\
```

不需要一起复制的仓库壳层文件：

- `src/blender_extension/avalonia_bridge/panel.py`
- `src/blender_extension/avalonia_bridge/operators.py`
- `src/blender_extension/avalonia_bridge/preferences.py`
- `src/blender_extension/avalonia_bridge/properties.py`
- `src/blender_extension/avalonia_bridge/runtime.py`

## 最小接入代码

```python
from .core import (
    BridgeConfig,
    BridgeController,
    DefaultBusinessEndpoint,
)


controller = BridgeController(
    BridgeConfig(
        executable_path="C:/path/to/YourAvaloniaApp.exe",
        width=1100,
        height=760,
        render_scaling=1.25,
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)
```

## 尺寸和渲染倍率

现在 bridge 已经把逻辑尺寸和渲染密度拆开了：

- `width` 和 `height` 控制 Avalonia 的逻辑窗口尺寸
- `render_scaling` 控制 headless 截图时的渲染倍率

这在 Blender 和 Avalonia 的 DPI 基准不完全一致时尤其有用。与其单纯放大 `width` / `height` 来换清晰度，更推荐提高 `render_scaling`，这样布局保持稳定，输出帧的像素密度也会更高。

在 sample 扩展的 UI 里，这两个参数对应：

- `Display Size`
- `Render Scaling`

默认 `render_scaling` 是 `1.25`。

## 你通常要接的几个点

- 生命周期：在自己的 operator 或 runtime adapter 中调用 `start()`、`stop()`、`tick_once()`
- 输入转发：在自己的事件处理里调用 `handle_event(context, event)`
- 状态同步：通过 `state_callback` 或 `state_snapshot()` / `diagnostics_snapshot()` 把状态映射到你的 UI
- 用户可调参数：建议把 `width`、`height` 和 `render_scaling` 一起暴露出来，让布局尺寸和清晰度分别可调

## 推荐接法

建议把你自己的扩展拆成两层：

- 你的 addon 壳层：负责 panel、operator、preferences、property group
- 复制进来的 `core/`：负责进程、传输、帧管线、overlay 和业务桥接

这样后续你更新业务层时，不需要去 fork 一整套桥接底层。
