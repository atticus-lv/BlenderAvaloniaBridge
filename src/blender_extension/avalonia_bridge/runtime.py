from .core import BridgeConfig, BridgeController, BridgeDiagnosticsSnapshot, BridgeStateSnapshot
from .core.process import ProcessLaunchError


def apply_state_snapshot(state, snapshot):
    state.process_running = bool(snapshot.process_running)
    state.connected = bool(snapshot.connected)
    state.capture_input = bool(snapshot.capture_input)
    state.process_id = int(snapshot.process_id)
    state.listen_port = int(snapshot.listen_port)
    state.last_error = snapshot.last_error
    state.last_message = snapshot.last_message
    if hasattr(state, "overlay_width"):
        state.overlay_width = int(snapshot.width)
    if hasattr(state, "overlay_height"):
        state.overlay_height = int(snapshot.height)
    if hasattr(state, "overlay_offset_x"):
        state.overlay_offset_x = int(snapshot.overlay_offset_x)
    if hasattr(state, "overlay_offset_y"):
        state.overlay_offset_y = int(snapshot.overlay_offset_y)
    return state


class BridgeRuntime:
    def __init__(self):
        self._controller = None

    @property
    def controller(self):
        if self._controller is None:
            self._controller = BridgeController(BridgeConfig(), state_callback=self._sync_state)
        return self._controller

    def start(self, context, executable_path):
        controller = self._ensure_controller(context, executable_path)
        return controller.start()

    def stop(self):
        if self._controller is None:
            return
        self._controller.stop()
        self._sync_state(self._controller.state_snapshot())

    def tick_once(self):
        self.controller.tick_once()

    def handle_event(self, context, event):
        return self.controller.handle_event(context, event)

    def release_capture_input(self):
        return self.controller.release_capture_input()

    def send_message(self, header, payload=b""):
        return self.controller.send_message(header, payload)

    def get_overlay_rect(self, context):
        return self.controller.get_overlay_rect(context)

    def state_snapshot(self, context=None):
        controller = self._ensure_controller(context) if context is not None else self.controller
        return controller.state_snapshot()

    def diagnostics_snapshot(self, context=None):
        controller = self._ensure_controller(context) if context is not None else self.controller
        return controller.diagnostics_snapshot().to_dict()

    def status_line(self):
        return self.controller.status_line()

    @property
    def capture_input(self):
        return self.controller.capture_input

    def _ensure_controller(self, context=None, executable_path=None):
        config = self._build_config(context, executable_path)
        if self._controller is None:
            self._controller = BridgeController(config, state_callback=self._sync_state)
        else:
            self._controller.set_config(config)
        if context is not None:
            self._sync_state(self._controller.state_snapshot())
        return self._controller

    def _build_config(self, context=None, executable_path=None):
        state = self._resolve_state(context)
        preferences = self._resolve_preferences(context)
        if executable_path is None and preferences is not None:
            executable_path = getattr(preferences, "avalonia_executable_path", "")
        return BridgeConfig(
            executable_path=executable_path or "",
            width=max(64, int(getattr(state, "overlay_width", 1100))),
            height=max(64, int(getattr(state, "overlay_height", 760))),
            render_scaling=float(getattr(state, "render_scaling", 1.25)),
            host="127.0.0.1",
            show_overlay_debug=bool(getattr(preferences, "show_overlay_debug", False)),
            overlay_offset_x=int(getattr(state, "overlay_offset_x", 0)),
            overlay_offset_y=int(getattr(state, "overlay_offset_y", 0)),
        )

    def _sync_state(self, snapshot):
        state = self._resolve_state()
        if state is not None:
            apply_state_snapshot(state, snapshot)

    def _resolve_state(self, context=None):
        bpy = __import__("bpy")
        active_context = context or getattr(bpy, "context", None)
        window_manager = getattr(active_context, "window_manager", None)
        return getattr(window_manager, "avalonia_bridge_state", None)

    def _resolve_preferences(self, context=None):
        if context is None:
            return None
        try:
            from .preferences import get_preferences
        except Exception:
            return None
        try:
            return get_preferences(context)
        except Exception:
            return None


_RUNTIME = BridgeRuntime()


def get_runtime():
    return _RUNTIME


__all__ = [
    "BridgeRuntime",
    "BridgeDiagnosticsSnapshot",
    "BridgeStateSnapshot",
    "ProcessLaunchError",
    "apply_state_snapshot",
    "get_runtime",
]
