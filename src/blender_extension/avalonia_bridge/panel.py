import bpy

from .preferences import get_preferences
from .runtime import get_runtime


class VIEW3D_PT_avalonia_bridge(bpy.types.Panel):
    bl_label = "AvaloniaBridgeDemo"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "AvaloniaBridgeDemo"

    def draw(self, context):
        layout = self.layout
        runtime = get_runtime()
        state = context.window_manager.avalonia_bridge_state
        snapshot = runtime.state_snapshot(context)
        preferences = get_preferences(context)

        if preferences is not None:
            layout.prop(preferences, "avalonia_executable_path", text="Avalonia Path")
            layout.prop(preferences, "bridge_transport_mode", text="Mode")
        else:
            warning_box = layout.box()
            warning_box.alert = True
            warning_box.label(text="Addon preferences not resolved.")
            warning_box.label(text="Reopen Blender or reinstall the extension if needed.")

        size_box = layout.box()
        size_box.label(text="Display Size")
        row = size_box.row(align=True)
        row.prop(state, "overlay_width", text="W")
        row.prop(state, "overlay_height", text="H")
        size_box.prop(state, "render_scaling", text="Render Scaling")

        if state.process_running:
            size_box.label(text="Restart the bridge after changing size or scaling.")

        status_box = layout.box()
        status_box.label(text=f"Process: {'Running' if snapshot.process_running else 'Stopped'}")
        status_box.label(text=f"Connection: {'Connected' if snapshot.connected else 'Waiting'}")
        status_box.label(text=f"Hover: {'Inside' if snapshot.capture_input else 'Outside'}")
        status_box.label(text=f"Port: {snapshot.listen_port}")
        status_box.label(text=f"Remote Mode: {snapshot.remote_window_mode}")
        status_box.label(text=f"Business: {'On' if snapshot.remote_supports_business else 'Off'}")
        status_box.label(text=f"Frames: {'On' if snapshot.remote_supports_frames else 'Off'}")
        status_box.label(text=f"Input: {'On' if snapshot.remote_supports_input else 'Off'}")

        if snapshot.last_message:
            status_box.label(text=f"Last Message: {snapshot.last_message}")
        if snapshot.last_error:
            error_box = layout.box()
            error_box.alert = True
            error_box.label(text="Last Error")
            error_box.label(text=snapshot.last_error)

        diagnostics = runtime.diagnostics_snapshot(context)
        diag_box = layout.box()
        diag_box.label(text="Bridge Diagnostics")
        diag_box.label(text=f"Mode: {diagnostics['mode']}")
        if not snapshot.remote_supports_frames:
            diag_box.label(text="FPS: disabled")
        elif diagnostics["fps"] is not None and diagnostics["frame_cadence_ms"] is not None:
            diag_box.label(text=f"FPS: {diagnostics['fps']:.1f} | Cadence: {diagnostics['frame_cadence_ms']:.1f} ms")
        else:
            diag_box.label(text="FPS: waiting")
        diag_box.label(text=f"Last frame seq: {diagnostics['last_frame_seq']}")
        diag_box.label(text=f"Last input: {diagnostics['last_input_type']}")
        if not snapshot.remote_supports_frames:
            diag_box.label(text="Input -> next frame: disabled")
        elif diagnostics["input_to_next_frame_ms"] is not None:
            diag_box.label(text=f"Input -> next frame: {diagnostics['input_to_next_frame_ms']:.1f} ms")
        else:
            diag_box.label(text="Input -> next frame: waiting")
        diag_box.label(text=f"PointerMove drop: {diagnostics['pointer_move_drop_pct']:.0f}%")
        diag_box.label(text=f"Pending move: {'yes' if diagnostics['pending_pointer_move'] else 'no'}")

        if preferences is not None and preferences.show_diagnostics_json:
            json_box = layout.box()
            json_box.label(text="Diagnostics JSON")
            for line in diagnostics["json_pretty"].splitlines():
                json_box.label(text=line)

        row = layout.row(align=True)
        row.operator("avalonia_bridge.start_ui_bridge", text="Start UI Bridge")
        row.operator("avalonia_bridge.stop_ui_bridge", text="Stop UI Bridge")


CLASSES = (
    VIEW3D_PT_avalonia_bridge,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
