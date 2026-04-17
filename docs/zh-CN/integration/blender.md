# Blender 侧接入

## 1. 复制 Blender 侧 bridge core

把下面这个目录复制到你自己的 Blender 扩展包中：

```text
src\blender_extension\avalonia_bridge\core\
```

Blender 侧的职责划分：

- `BridgeController`：bridge core，本身只负责进程生命周期、传输、frame pipeline、business packet、状态与诊断
- `View3DOverlayHost`：可选的 `3D View` 宿主，负责 overlay 绘制、标题栏拖拽、hit-test、输入转发与 redraw

## 2. 组装 controller

最小组装示例：

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

## 3. 选择展示宿主

- `View3DOverlayHost` 是 Blender `3D View` 的可选展示宿主
- 当前示例在 `headless` 模式下使用它把 UI 绘制到 `3D View`
- 如果不希望绘制到 `3D View`，可以不组装 `View3DOverlayHost`，只保留 business 通道
- 其他 Blender 展示方式需要在 addon 层自行组装适配
## 4. 生命周期与事件驱动

Blender 侧分两层接入：

- runtime adapter：构造 `BridgeConfig`，并组装 `BridgeController` 与可选的 `View3DOverlayHost`
- modal operator：在 `TIMER` 中调用 `tick_once()`，在事件链路中调用 `handle_event(context, event)`

当前会转发的 packet 类型：

- 指针类：`pointer_down`、`pointer_up`、`pointer_move`、`wheel`
- 键盘类：`key_down`、`key_up`
- 文本类：按键带有非空 `event.unicode` 时发送 `text`

当前 `View3DOverlayHost` 支持的 Blender `event.type`：

- 指针与滚轮：`MOUSEMOVE`、`INBETWEEN_MOUSEMOVE`、`LEFTMOUSE`、`RIGHTMOUSE`、`MIDDLEMOUSE`、`WHEELUPMOUSE`、`WHEELDOWNMOUSE`、`EVT_TWEAK_L`、`EVT_TWEAK_M`、`EVT_TWEAK_R`
- 仅用于标题栏拖拽：`LEFTMOUSE`、`EVT_TWEAK_L`
- 字母键：`A`-`Z`
- 主键盘数字：`ZERO`-`NINE`
- 基本编辑与导航：`SPACE`、`TAB`、`RET`、`NUMPAD_ENTER`、`BACK_SPACE`、`DEL`、`INSERT`、`HOME`、`END`、`PAGE_UP`、`PAGE_DOWN`、`ESC`、`LINE_FEED`
- 方向键：`LEFT_ARROW`、`RIGHT_ARROW`、`UP_ARROW`、`DOWN_ARROW`
- 标点键：`PERIOD`、`NUMPAD_PERIOD`、`COMMA`、`MINUS`、`PLUS`、`EQUAL`、`SEMI_COLON`、`QUOTE`、`SLASH`、`BACK_SLASH`、`LEFT_BRACKET`、`RIGHT_BRACKET`、`ACCENT_GRAVE`
- 修饰键：`LEFT_SHIFT`、`RIGHT_SHIFT`、`LEFT_CTRL`、`RIGHT_CTRL`、`LEFT_ALT`、`RIGHT_ALT`、`OSKEY`、`APP`
- 小键盘：`NUMPAD_0`-`NUMPAD_9`、`NUMPAD_SLASH`、`NUMPAD_ASTERIX`、`NUMPAD_MINUS`、`NUMPAD_PLUS`
- 功能键：`F1`-`F24`

说明：

- 键盘 packet 只会在 overlay 已捕获输入时转发
- `desktop` 模式没有 frame host，不转发指针或键盘输入
- `View3DOverlayHost` 会在本地消费标题栏拖拽事件，不会把它们作为键盘 packet 转发

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

## 下一步

- 返回[集成概览](./index.md)
- 想查看 C# 侧调用方式，请看 [API 章节](../api/index.md)
