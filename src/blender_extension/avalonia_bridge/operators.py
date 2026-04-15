import bpy

from .preferences import get_preferences
from .runtime import ProcessLaunchError, get_runtime


class AVALONIA_BRIDGE_OT_start_ui_bridge(bpy.types.Operator):
    bl_idname = "avalonia_bridge.start_ui_bridge"
    bl_label = "Start UI Bridge"
    bl_description = "Launch the Avalonia bridge process and start drawing its output"

    def execute(self, context):
        runtime = get_runtime()
        preferences = get_preferences(context)
        if preferences is None:
            message = "Addon preferences could not be resolved for this Blender extension package."
            context.window_manager.avalonia_bridge_state.last_error = message
            self.report({"ERROR"}, message)
            return {"CANCELLED"}
        try:
            runtime.start(context, preferences.avalonia_executable_path)
        except ProcessLaunchError as exc:
            context.window_manager.avalonia_bridge_state.last_error = str(exc)
            self.report({"ERROR"}, str(exc))
            return {"CANCELLED"}
        except Exception as exc:
            context.window_manager.avalonia_bridge_state.last_error = str(exc)
            self.report({"ERROR"}, f"Failed to start Avalonia bridge: {exc}")
            return {"CANCELLED"}

        bpy.ops.avalonia_bridge.ui_bridge_modal("INVOKE_DEFAULT")
        self.report({"INFO"}, "Avalonia bridge started")
        return {"FINISHED"}


class AVALONIA_BRIDGE_OT_stop_ui_bridge(bpy.types.Operator):
    bl_idname = "avalonia_bridge.stop_ui_bridge"
    bl_label = "Stop UI Bridge"
    bl_description = "Stop the Avalonia bridge process"

    def execute(self, context):
        runtime = get_runtime()
        runtime.stop()
        context.window_manager.avalonia_bridge_state.last_message = "Bridge stopped"
        self.report({"INFO"}, "Avalonia bridge stopped")
        return {"FINISHED"}


class AVALONIA_BRIDGE_OT_ui_bridge_modal(bpy.types.Operator):
    bl_idname = "avalonia_bridge.ui_bridge_modal"
    bl_label = "AvaloniaBridgeDemo UI Modal"
    bl_description = "Capture mouse and keyboard input for the Avalonia bridge"
    bl_options = {"BLOCKING"}

    _running = False
    _timer = None

    def invoke(self, context, _event):
        if self.__class__._running:
            return {"CANCELLED"}
        self.__class__._running = True
        self.__class__._timer = context.window_manager.event_timer_add(1.0 / 60.0, window=context.window)
        context.window_manager.modal_handler_add(self)
        return {"RUNNING_MODAL"}

    def modal(self, context, event):
        runtime = get_runtime()
        state = context.window_manager.avalonia_bridge_state
        capture_sensitive_events = {
            "MOUSEMOVE",
            "LEFTMOUSE",
            "RIGHTMOUSE",
            "MIDDLEMOUSE",
            "WHEELUPMOUSE",
            "WHEELDOWNMOUSE",
        }
        if not state.process_running:
            self.cancel(context)
            return {"CANCELLED"}

        if event.type == "TIMER":
            runtime.tick_once()
            return {"RUNNING_MODAL"}

        if context.area and context.area.type == "VIEW_3D":
            handled = runtime.handle_event(context, event)
            if handled:
                return {"RUNNING_MODAL"}
            if state.capture_input and event.type in capture_sensitive_events:
                return {"RUNNING_MODAL"}

        return {"PASS_THROUGH"}

    def cancel(self, context):
        if self.__class__._timer is not None:
            context.window_manager.event_timer_remove(self.__class__._timer)
            self.__class__._timer = None
        self.__class__._running = False


CLASSES = (
    AVALONIA_BRIDGE_OT_start_ui_bridge,
    AVALONIA_BRIDGE_OT_stop_ui_bridge,
    AVALONIA_BRIDGE_OT_ui_bridge_modal,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
