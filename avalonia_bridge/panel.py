import bpy

from .preferences import default_executable_hint, get_preferences


class VIEW3D_PT_renderbuilder_bridge(bpy.types.Panel):
    bl_label = "RenderBuilder"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "RenderBuilder"

    def draw(self, context):
        layout = self.layout
        state = context.window_manager.renderbuilder_bridge_state
        preferences = get_preferences(context)

        if preferences is not None:
            layout.prop(preferences, "avalonia_executable_path", text="Avalonia Path")
        else:
            warning_box = layout.box()
            warning_box.alert = True
            warning_box.label(text="Addon preferences not resolved.")
            warning_box.label(text="Reopen Blender or reinstall the extension if needed.")

        hint_box = layout.box()
        hint_box.label(text="Suggested default")
        hint_box.label(text=default_executable_hint())

        size_box = layout.box()
        size_box.label(text="Overlay Size")
        row = size_box.row(align=True)
        row.prop(state, "overlay_width", text="W")
        row.prop(state, "overlay_height", text="H")
        size_box.label(text="The panel is now centered and auto-fits inside the area.")

        status_box = layout.box()
        status_box.label(text=f"Process: {'Running' if state.process_running else 'Stopped'}")
        status_box.label(text=f"Connection: {'Connected' if state.connected else 'Waiting'}")
        status_box.label(text=f"Capture: {'On' if state.capture_input else 'Off'}")
        status_box.label(text=f"Port: {state.listen_port}")

        if state.last_message:
            status_box.label(text=f"Last Message: {state.last_message}")
        if state.last_error:
            error_box = layout.box()
            error_box.alert = True
            error_box.label(text="Last Error")
            error_box.label(text=state.last_error)

        row = layout.row(align=True)
        row.operator("renderbuilder.start_ui_bridge", text="Start UI Bridge")
        row.operator("renderbuilder.stop_ui_bridge", text="Stop UI Bridge")


CLASSES = (
    VIEW3D_PT_renderbuilder_bridge,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
