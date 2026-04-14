from pathlib import Path

import bpy


PACKAGE_NAME = (__package__ or __name__.rpartition(".")[0]).strip(".")
PACKAGE_BASENAME = PACKAGE_NAME.rsplit(".", 1)[-1] if PACKAGE_NAME else "avalonia_bridge"


def default_executable_hint():
    repo_root = Path(__file__).resolve().parents[3]
    return str(repo_root / "src" / "BlenderAvaloniaBridge.Sample" / "bin" / "Debug" / "net10.0" / "BlenderAvaloniaBridge.Sample.exe")


class AvaloniaBridgeAddonPreferences(bpy.types.AddonPreferences):
    bl_idname = PACKAGE_NAME or PACKAGE_BASENAME

    avalonia_executable_path: bpy.props.StringProperty(
        name="Avalonia Executable",
        subtype="FILE_PATH",
        description="Path to the Avalonia bridge executable or DLL",
        default=default_executable_hint(),
    )
    show_diagnostics_json: bpy.props.BoolProperty(
        name="Show Diagnostics JSON",
        description="Show the pretty JSON diagnostics block in the AvaloniaBridgeDemo panel",
        default=False,
    )
    show_overlay_debug: bpy.props.BoolProperty(
        name="Show Overlay Debug Text",
        description="Show debug text in the GPU overlay",
        default=False,
    )
    bridge_transport_mode: bpy.props.EnumProperty(
        name="Bridge Mode",
        description="Choose whether to stream a headless overlay or open a real Avalonia window with business-only transport",
        items=(
            ("headless", "Headless Frames + Input", "Use headless rendering with frame streaming and pointer/keyboard input"),
            ("desktop", "Desktop Business Only", "Open a real Avalonia window and use only business transport"),
        ),
        default="headless",
    )

    def draw(self, _context):
        layout = self.layout
        layout.label(text="Avalonia UI Bridge")
        layout.prop(self, "avalonia_executable_path")
        layout.prop(self, "bridge_transport_mode")
        box = layout.box()
        box.label(text="Display")
        box.prop(self, "show_diagnostics_json")
        box.prop(self, "show_overlay_debug")


CLASSES = (
    AvaloniaBridgeAddonPreferences,
)


def get_preferences(context):
    addons = context.preferences.addons

    for key in (PACKAGE_NAME, PACKAGE_BASENAME):
        if key and key in addons:
            return addons[key].preferences

    for addon in addons:
        module_name = getattr(addon, "module", "")
        if module_name in {PACKAGE_NAME, PACKAGE_BASENAME}:
            return addon.preferences
        if PACKAGE_BASENAME and module_name.endswith(f".{PACKAGE_BASENAME}"):
            return addon.preferences

    return None


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
