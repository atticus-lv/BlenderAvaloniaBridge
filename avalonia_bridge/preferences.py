from pathlib import Path

import bpy


PACKAGE_NAME = (__package__ or __name__.rpartition(".")[0]).strip(".")
PACKAGE_BASENAME = PACKAGE_NAME.rsplit(".", 1)[-1] if PACKAGE_NAME else "avalonia_bridge"


def default_executable_hint():
    root = Path(__file__).resolve().parents[1]
    return str(root / "avalonia" / "src" / "BlenderAvaloniaBridge" / "bin" / "Debug" / "net8.0" / "BlenderAvaloniaBridge.exe")


class RenderBuilderAddonPreferences(bpy.types.AddonPreferences):
    bl_idname = PACKAGE_NAME or PACKAGE_BASENAME

    avalonia_executable_path: bpy.props.StringProperty(
        name="Avalonia Executable",
        subtype="FILE_PATH",
        description="Path to the Avalonia bridge executable or DLL",
        default=default_executable_hint(),
    )

    def draw(self, _context):
        layout = self.layout
        layout.label(text="Avalonia UI Bridge")
        layout.prop(self, "avalonia_executable_path")
        box = layout.box()
        box.label(text="Suggested paths:")
        box.label(text=default_executable_hint())
        box.label(text="Release: ...\\bin\\Release\\net8.0\\BlenderAvaloniaBridge.exe")
        box.label(text="AOT: ...\\publish\\aot\\win-x64\\BlenderAvaloniaBridge.exe")


CLASSES = (
    RenderBuilderAddonPreferences,
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
