import importlib
import pathlib
import sys
import types
from contextlib import nullcontext


ROOT = pathlib.Path(__file__).resolve().parents[3]
PACKAGE_ROOT = ROOT / "src" / "blender_extension" / "avalonia_bridge"


def bootstrap_addon_package():
    package = types.ModuleType("avalonia_bridge")
    package.__path__ = [str(PACKAGE_ROOT)]
    sys.modules["avalonia_bridge"] = package
    return package


def reset_modules():
    for name in list(sys.modules):
        if name == "avalonia_bridge" or name.startswith("avalonia_bridge."):
            sys.modules.pop(name, None)
    for name in (
        "bpy",
        "blf",
        "gpu",
        "gpu_extras",
        "gpu_extras.batch",
        "mathutils",
    ):
        sys.modules.pop(name, None)


def install_fake_blender_modules():
    bpy = types.ModuleType("bpy")
    draw_handler_add_calls = []
    draw_handler_remove_calls = []
    redraw_calls = []
    bpy.context = types.SimpleNamespace(
        scene=types.SimpleNamespace(name="Scene", objects=[]),
        screen=types.SimpleNamespace(
            areas=[
                types.SimpleNamespace(type="VIEW_3D", tag_redraw=lambda: redraw_calls.append("VIEW_3D")),
                types.SimpleNamespace(type="IMAGE_EDITOR", tag_redraw=lambda: redraw_calls.append("IMAGE_EDITOR")),
            ]
        ),
        view_layer=types.SimpleNamespace(objects=types.SimpleNamespace(active=None)),
        window_manager=types.SimpleNamespace(windows=[], avalonia_bridge_state=None),
        preferences=types.SimpleNamespace(
            addons={},
            system=types.SimpleNamespace(dpi=96),
        ),
        region=types.SimpleNamespace(width=1920, height=1080),
        temp_override=lambda **_kwargs: nullcontext(),
    )
    bpy.ops = types.SimpleNamespace()
    bpy.data = types.SimpleNamespace(
        objects={},
        materials={},
        collections={},
        images=types.SimpleNamespace(
            get=lambda _name: None,
            new=lambda **_kwargs: types.SimpleNamespace(
                size=(0, 0),
                generated_type="BLANK",
                colorspace_settings=types.SimpleNamespace(name="Raw", is_data=True),
                alpha_mode="STRAIGHT",
                use_view_as_render=False,
                pixels=types.SimpleNamespace(foreach_set=lambda _values: None),
                update=lambda: None,
                scale=lambda *_args: None,
            ),
        ),
    )
    bpy.types = types.SimpleNamespace(
        SpaceView3D=types.SimpleNamespace(
            draw_handler_add=lambda *args, **kwargs: draw_handler_add_calls.append((args, kwargs)) or ("draw-handle", args, kwargs),
            draw_handler_remove=lambda *args, **kwargs: draw_handler_remove_calls.append((args, kwargs)) or None,
        ),
        WindowManager=types.SimpleNamespace(),
    )
    bpy.props = types.SimpleNamespace(
        BoolProperty=lambda **kwargs: kwargs,
        StringProperty=lambda **kwargs: kwargs,
        IntProperty=lambda **kwargs: kwargs,
        EnumProperty=lambda **kwargs: kwargs,
        PointerProperty=lambda **kwargs: kwargs,
    )
    bpy.utils = types.SimpleNamespace(
        register_class=lambda _cls: None,
        unregister_class=lambda _cls: None,
    )
    bpy.app = types.SimpleNamespace(
        handlers=types.SimpleNamespace(
            depsgraph_update_post=[],
            frame_change_post=[],
            load_post=[],
            undo_post=[],
            redo_post=[],
            persistent=lambda fn: fn,
        )
    )
    bpy._draw_handler_add_calls = draw_handler_add_calls
    bpy._draw_handler_remove_calls = draw_handler_remove_calls
    bpy._redraw_calls = redraw_calls
    sys.modules["bpy"] = bpy

    blf = types.ModuleType("blf")
    blf.position = lambda *_args, **_kwargs: None
    blf.size = lambda *_args, **_kwargs: None
    blf.draw = lambda *_args, **_kwargs: None
    sys.modules["blf"] = blf

    gpu = types.ModuleType("gpu")
    gpu._buffer_calls = []
    gpu._texture_calls = []

    class _GPUShaderCreateInfo:
        def push_constant(self, *_args, **_kwargs):
            return None

        def sampler(self, *_args, **_kwargs):
            return None

        def vertex_in(self, *_args, **_kwargs):
            return None

        def vertex_out(self, *_args, **_kwargs):
            return None

        def fragment_out(self, *_args, **_kwargs):
            return None

        def vertex_source(self, *_args, **_kwargs):
            return None

        def fragment_source(self, *_args, **_kwargs):
            return None

    class _GPUStageInterfaceInfo:
        def __init__(self, _name):
            pass

        def smooth(self, *_args, **_kwargs):
            return None

    gpu.shader = types.SimpleNamespace(
        from_builtin=lambda _name: object(),
        create_from_info=lambda _info: object(),
    )
    gpu.state = types.SimpleNamespace(blend_set=lambda _mode: None)
    gpu.matrix = types.SimpleNamespace(
        get_projection_matrix=lambda: object(),
        get_model_view_matrix=lambda: object(),
    )
    gpu.texture = types.SimpleNamespace(from_image=lambda _image: object())
    gpu.types = types.SimpleNamespace(
        GPUShaderCreateInfo=_GPUShaderCreateInfo,
        GPUStageInterfaceInfo=_GPUStageInterfaceInfo,
        Buffer=lambda component_type, dimensions, data: gpu._buffer_calls.append(
            {
                "component_type": component_type,
                "dimensions": dimensions,
                "data": data,
            }
        ) or object(),
        GPUTexture=lambda size, format, data: gpu._texture_calls.append(
            {
                "size": size,
                "format": format,
                "data": data,
            }
        ) or object(),
    )
    sys.modules["gpu"] = gpu

    gpu_extras = types.ModuleType("gpu_extras")
    batch = types.ModuleType("gpu_extras.batch")
    batch.batch_for_shader = lambda *_args, **_kwargs: types.SimpleNamespace(draw=lambda *_a, **_k: None)
    sys.modules["gpu_extras"] = gpu_extras
    sys.modules["gpu_extras.batch"] = batch

    mathutils = types.ModuleType("mathutils")
    mathutils.Matrix = types.SimpleNamespace(Identity=lambda _size: object())
    sys.modules["mathutils"] = mathutils


def import_module(module_name):
    reset_modules()
    bootstrap_addon_package()
    install_fake_blender_modules()
    return importlib.import_module(module_name)
