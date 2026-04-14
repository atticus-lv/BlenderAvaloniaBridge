import importlib
import pathlib
import sys
import types


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
    bpy.context = types.SimpleNamespace(
        scene=types.SimpleNamespace(name="Scene", objects=[]),
        screen=types.SimpleNamespace(areas=[]),
        view_layer=types.SimpleNamespace(objects=types.SimpleNamespace(active=None)),
        window_manager=types.SimpleNamespace(windows=[], avalonia_bridge_state=None),
        preferences=types.SimpleNamespace(
            addons={},
            system=types.SimpleNamespace(dpi=96),
        ),
        region=types.SimpleNamespace(width=1920, height=1080),
    )
    bpy.ops = types.SimpleNamespace()
    bpy.data = types.SimpleNamespace(
        objects={},
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
            draw_handler_add=lambda *args, **kwargs: ("draw-handle", args, kwargs),
            draw_handler_remove=lambda *_args, **_kwargs: None,
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
    sys.modules["bpy"] = bpy

    blf = types.ModuleType("blf")
    blf.position = lambda *_args, **_kwargs: None
    blf.size = lambda *_args, **_kwargs: None
    blf.draw = lambda *_args, **_kwargs: None
    sys.modules["blf"] = blf

    gpu = types.ModuleType("gpu")
    gpu._buffer_calls = []
    gpu._texture_calls = []
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
        GPUShaderCreateInfo=type("GPUShaderCreateInfo", (), {}),
        GPUStageInterfaceInfo=type("GPUStageInterfaceInfo", (), {}),
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
