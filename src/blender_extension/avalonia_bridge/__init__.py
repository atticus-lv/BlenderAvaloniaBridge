bl_info = {
    "name": "Avalonia Bridger",
    "author": "OpenAI Codex",
    "version": (0, 1, 0),
    "blender": (5, 0, 0),
    "location": "View3D > Sidebar > AvaloniaBridgeDemo",
    "description": "Launch an external Avalonia headless UI process and draw it inside Blender.",
    "category": "3D View",
}

from . import operators, panel, preferences, properties


MODULES = (
    properties,
    preferences,
    operators,
    panel,
)


def register():
    for module in MODULES:
        module.register()


def unregister():
    for module in reversed(MODULES):
        module.unregister()
