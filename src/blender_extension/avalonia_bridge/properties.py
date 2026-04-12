import bpy


class RenderBuilderBridgeState(bpy.types.PropertyGroup):
    process_running: bpy.props.BoolProperty(name="Process Running", default=False)
    connected: bpy.props.BoolProperty(name="Connected", default=False)
    capture_input: bpy.props.BoolProperty(name="Capture Input", default=False)
    last_error: bpy.props.StringProperty(name="Last Error", default="")
    last_message: bpy.props.StringProperty(name="Last Message", default="")
    overlay_width: bpy.props.IntProperty(name="Overlay Width", default=1100, min=64, max=4096)
    overlay_height: bpy.props.IntProperty(name="Overlay Height", default=760, min=64, max=4096)
    process_id: bpy.props.IntProperty(name="Process ID", default=0)
    listen_port: bpy.props.IntProperty(name="Listen Port", default=0)
    overlay_offset_x: bpy.props.IntProperty(name="Overlay Offset X", default=0, min=-4096, max=4096)
    overlay_offset_y: bpy.props.IntProperty(name="Overlay Offset Y", default=0, min=-4096, max=4096)


CLASSES = (
    RenderBuilderBridgeState,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.WindowManager.renderbuilder_bridge_state = bpy.props.PointerProperty(type=RenderBuilderBridgeState)


def unregister():
    del bpy.types.WindowManager.renderbuilder_bridge_state
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
