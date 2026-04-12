import bpy

from . import input_mapper
from .draw_overlay import OverlayDrawer
from .frame_store import FrameStore
from .image_bridge import ImageBridge
from .process_manager import ProcessLaunchError, ProcessManager, validate_executable_path
from .transport import BridgeServer


class BridgeRuntime:
    def __init__(self):
        self.width = 1100
        self.height = 760
        self.capture_input = False
        self.frame_store = FrameStore()
        self.image_bridge = ImageBridge()
        self.process_manager = ProcessManager()
        self.server = None
        self.drawer = OverlayDrawer(self)
        self._timer_registered = False

    @property
    def state(self):
        return bpy.context.window_manager.renderbuilder_bridge_state

    def start(self, context, executable_path):
        self.stop()
        path = validate_executable_path(executable_path)
        state = context.window_manager.renderbuilder_bridge_state
        self.width = state.overlay_width
        self.height = state.overlay_height
        self.server = BridgeServer(
            on_packet=self._on_packet,
            on_connect=self._on_connect,
            on_disconnect=self._on_disconnect,
            on_error=self._on_error,
        )
        self.server.start()
        process = self.process_manager.start(str(path), "127.0.0.1", self.server.port, self.width, self.height)
        state.process_running = True
        state.connected = False
        state.capture_input = False
        state.process_id = process.pid or 0
        state.listen_port = self.server.port
        state.last_error = ""
        state.last_message = "Process launched"
        self.capture_input = False
        self.drawer.ensure_handler()
        if not self._timer_registered:
            bpy.app.timers.register(self._tick, first_interval=0.05, persistent=False)
            self._timer_registered = True
        self.tag_redraw()

    def stop(self):
        self.capture_input = False
        if self.server is not None:
            self.server.stop()
            self.server = None
        self.process_manager.stop()
        self.drawer.remove_handler()
        self.image_bridge.clear()
        state = bpy.context.window_manager.renderbuilder_bridge_state
        state.process_running = False
        state.connected = False
        state.capture_input = False
        state.process_id = 0
        state.listen_port = 0
        self.tag_redraw()

    def _on_connect(self, server):
        state = bpy.context.window_manager.renderbuilder_bridge_state
        state.connected = True
        state.last_message = "Avalonia connected"
        self.send_message(
            {
                "type": "init",
                "seq": 1,
                "width": self.width,
                "height": self.height,
                "pixel_format": "bgra8",
                "stride": self.width * 4,
                "payload_length": 0,
                "message": "blender-ready",
            }
        )
        self.tag_redraw()

    def _on_disconnect(self):
        state = bpy.context.window_manager.renderbuilder_bridge_state
        state.connected = False
        state.capture_input = False
        self.capture_input = False
        state.last_message = "Avalonia disconnected"
        self.tag_redraw()

    def _on_error(self, exc):
        state = bpy.context.window_manager.renderbuilder_bridge_state
        state.last_error = str(exc)
        self.tag_redraw()

    def _on_packet(self, header, payload):
        state = bpy.context.window_manager.renderbuilder_bridge_state
        packet_type = header.get("type", "")
        if packet_type == "frame":
            self.frame_store.update(header, payload)
            state.last_message = "Frame received"
        elif packet_type == "init":
            state.last_message = header.get("message", "Init acknowledged")
        elif packet_type == "error":
            state.last_error = header.get("message", "Avalonia reported an error")
        self.tag_redraw()

    def _tick(self):
        frame = self.frame_store.pop_latest()
        if frame is not None:
            header, payload = frame
            width = int(header.get("width", self.width))
            height = int(header.get("height", self.height))
            self.width = width
            self.height = height
            self.image_bridge.update_from_bgra(payload, width, height)
        self.tag_redraw()
        if self.server is None and self.process_manager.process is None:
            self._timer_registered = False
            return None
        return 1.0 / 30.0

    def send_message(self, header, payload=b""):
        if self.server is None:
            return False
        header = dict(header)
        header.setdefault("payload_length", len(payload or b""))
        return self.server.send(header, payload)

    def get_overlay_rect(self, context):
        region = context.region
        return input_mapper.overlay_rect(region.width, region.height, self.width, self.height)

    def handle_event(self, context, event):
        rect = self.get_overlay_rect(context)
        x = getattr(event, "mouse_region_x", -1)
        y = getattr(event, "mouse_region_y", -1)
        inside = input_mapper.contains_point(rect, x, y)
        modifiers = input_mapper.modifiers_from_event(event)

        if event.type == "LEFTMOUSE" and event.value == "PRESS":
            if inside:
                self.capture_input = True
                self.state.capture_input = True
                px, py = input_mapper.to_avalonia_coords(rect, x, y)
                self.send_message({"type": "focus", "seq": 2, "focus": True})
                self.send_message({"type": "pointer_down", "seq": 3, "x": px, "y": py, "button": "left", "modifiers": modifiers})
                self.state.last_message = f"PointerDown {px},{py}"
                self.tag_redraw()
                return True
            if self.capture_input:
                self.capture_input = False
                self.state.capture_input = False
                self.send_message({"type": "focus", "seq": 4, "focus": False})
                self.state.last_message = "Focus released"
                self.tag_redraw()
            return False

        if event.type == "LEFTMOUSE" and event.value == "RELEASE" and self.capture_input:
            px, py = input_mapper.to_avalonia_coords(rect, x, y)
            self.send_message({"type": "pointer_up", "seq": 5, "x": px, "y": py, "button": "left", "modifiers": modifiers})
            self.state.last_message = f"PointerUp {px},{py}"
            self.tag_redraw()
            return True

        if event.type == "MOUSEMOVE" and self.capture_input:
            px, py = input_mapper.to_avalonia_coords(rect, x, y)
            self.send_message({"type": "pointer_move", "seq": 6, "x": px, "y": py, "modifiers": modifiers})
            return True

        if event.type in {"WHEELUPMOUSE", "WHEELDOWNMOUSE"} and self.capture_input:
            px, py = input_mapper.to_avalonia_coords(rect, x, y)
            dx, dy = input_mapper.wheel_delta(event.type)
            self.send_message({"type": "wheel", "seq": 7, "x": px, "y": py, "delta_x": dx, "delta_y": dy, "modifiers": modifiers})
            self.state.last_message = f"Wheel {dy:+.0f}"
            self.tag_redraw()
            return True

        if self.capture_input and event.type in input_mapper.KEYBOARD_EVENT_TYPES and event.value in {"PRESS", "RELEASE"}:
            message_type = "key_down" if event.value == "PRESS" else "key_up"
            self.send_message({"type": message_type, "seq": 8, "key": event.type, "text": event.unicode or "", "modifiers": modifiers})
            if event.value == "PRESS" and event.unicode:
                self.send_message({"type": "text", "seq": 9, "text": event.unicode})
            self.state.last_message = f"{message_type} {event.type}"
            self.tag_redraw()
            return True

        return False

    def status_line(self):
        state = bpy.context.window_manager.renderbuilder_bridge_state
        connection = "connected" if state.connected else "waiting"
        capture = "capture on" if state.capture_input else "capture off"
        return f"Avalonia {self.width}x{self.height} | {connection} | {capture}"

    def tag_redraw(self):
        context = bpy.context
        screen = context.screen
        if screen is None:
            return
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.tag_redraw()


_RUNTIME = BridgeRuntime()


def get_runtime():
    return _RUNTIME


__all__ = [
    "BridgeRuntime",
    "ProcessLaunchError",
    "get_runtime",
]
