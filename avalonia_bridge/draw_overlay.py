import blf
import gpu
from gpu_extras.batch import batch_for_shader

from . import input_mapper


class OverlayDrawer:
    def __init__(self, runtime):
        self.runtime = runtime
        self._handle = None
        self._image_shader = None
        self._color_shader = None

    def ensure_handler(self):
        if self._handle is not None:
            return
        self._image_shader = gpu.shader.from_builtin("IMAGE")
        self._color_shader = gpu.shader.from_builtin("UNIFORM_COLOR")
        self._handle = __import__("bpy").types.SpaceView3D.draw_handler_add(self.draw, (), "WINDOW", "POST_PIXEL")

    def remove_handler(self):
        if self._handle is None:
            return
        __import__("bpy").types.SpaceView3D.draw_handler_remove(self._handle, "WINDOW")
        self._handle = None

    def draw(self):
        context = __import__("bpy").context
        region = context.region
        if region is None:
            return

        rect = input_mapper.overlay_rect(region.width, region.height, self.runtime.width, self.runtime.height)
        image = self.runtime.image_bridge.image
        gpu.state.blend_set("ALPHA")

        if image is not None:
            texture = gpu.texture.from_image(image)
            batch = batch_for_shader(
                self._image_shader,
                "TRI_FAN",
                {
                    "pos": (
                        (rect["x"], rect["y"]),
                        (rect["x"] + rect["width"], rect["y"]),
                        (rect["x"] + rect["width"], rect["y"] + rect["height"]),
                        (rect["x"], rect["y"] + rect["height"]),
                    ),
                    "texCoord": ((0, 0), (1, 0), (1, 1), (0, 1)),
                },
            )
            self._image_shader.bind()
            self._image_shader.uniform_sampler("image", texture)
            batch.draw(self._image_shader)

        border_batch = batch_for_shader(
            self._color_shader,
            "LINE_LOOP",
            {
                "pos": (
                    (rect["x"], rect["y"]),
                    (rect["x"] + rect["width"], rect["y"]),
                    (rect["x"] + rect["width"], rect["y"] + rect["height"]),
                    (rect["x"], rect["y"] + rect["height"]),
                )
            },
        )
        self._color_shader.bind()
        color = (0.98, 0.85, 0.15, 1.0) if self.runtime.capture_input else (0.2, 0.8, 0.9, 1.0)
        self._color_shader.uniform_float("color", color)
        border_batch.draw(self._color_shader)

        text_y = rect["y"] + rect["height"] + 8
        blf.position(0, rect["x"], text_y, 0)
        blf.size(0, 12)
        blf.draw(0, self.runtime.status_line())

        gpu.state.blend_set("NONE")
