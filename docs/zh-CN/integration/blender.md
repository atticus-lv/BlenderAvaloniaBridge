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
        host="127.0.0.1",
        show_overlay_debug=False,
    ),
    business_endpoint=DefaultBusinessEndpoint(),
    state_callback=lambda snapshot: print(snapshot.last_message),
)
```

## 你通常要接的几个点

- 生命周期：在自己的 operator 或 runtime adapter 中调用 `start()`、`stop()`、`tick_once()`
- 输入转发：在自己的事件处理里调用 `handle_event(context, event)`
- 状态同步：通过 `state_callback` 或 `state_snapshot()` / `diagnostics_snapshot()` 把状态映射到你的 UI

## 推荐接法

建议把你自己的扩展拆成两层：

- 你的 addon 壳层：负责 panel、operator、preferences、property group
- 复制进来的 `core/`：负责进程、传输、帧管线、overlay 和业务桥接

这样后续你更新业务层时，不需要去 fork 一整套桥接底层。
