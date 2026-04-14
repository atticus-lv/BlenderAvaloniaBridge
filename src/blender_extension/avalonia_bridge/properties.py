import bpy


class AvaloniaBridgeState(bpy.types.PropertyGroup):
    process_running: bpy.props.BoolProperty(name="Process Running", default=False)
    connected: bpy.props.BoolProperty(name="Connected", default=False)
    capture_input: bpy.props.BoolProperty(name="Capture Input", default=False)
    last_error: bpy.props.StringProperty(name="Last Error", default="")
    last_message: bpy.props.StringProperty(name="Last Message", default="")
    overlay_width: bpy.props.IntProperty(name="Overlay Width", default=1100, min=64, max=4096)
    overlay_height: bpy.props.IntProperty(name="Overlay Height", default=760, min=64, max=4096)
    render_scaling: bpy.props.FloatProperty(
        name="Render Scaling",
        default=1.25,
        min=0.25,
        max=4.0,
        precision=2,
    )
    process_id: bpy.props.IntProperty(name="Process ID", default=0)
    listen_port: bpy.props.IntProperty(name="Listen Port", default=0)
    remote_window_mode: bpy.props.StringProperty(name="Remote Window Mode", default="unknown")
    remote_supports_business: bpy.props.BoolProperty(name="Remote Supports Business", default=True)
    remote_supports_frames: bpy.props.BoolProperty(name="Remote Supports Frames", default=True)
    remote_supports_input: bpy.props.BoolProperty(name="Remote Supports Input", default=True)
    overlay_offset_x: bpy.props.IntProperty(name="Overlay Offset X", default=0, min=-4096, max=4096)
    overlay_offset_y: bpy.props.IntProperty(name="Overlay Offset Y", default=0, min=-4096, max=4096)


CLASSES = (
    AvaloniaBridgeState,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.WindowManager.avalonia_bridge_state = bpy.props.PointerProperty(type=AvaloniaBridgeState)


def unregister():
    del bpy.types.WindowManager.avalonia_bridge_state
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
