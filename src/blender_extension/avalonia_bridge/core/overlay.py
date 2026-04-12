import blf
import gpu
from collections import deque
from gpu_extras.batch import batch_for_shader
from mathutils import Matrix
from math import cos, pi, sin
from time import perf_counter

from . import input as input_mapper


class OverlayDrawer:
    def __init__(self, runtime):
        self.runtime = runtime
        self._handle = None
        self._texture_shader = None
        self._shadow_shader = None
        self._color_shader = None
        self._stats = {
            "draw_ms": deque(maxlen=60),
        }

    def ensure_handler(self):
        if self._handle is not None:
            return
        self._texture_shader = self._create_texture_shader()
        self._shadow_shader = self._create_shadow_shader()
        self._color_shader = gpu.shader.from_builtin("UNIFORM_COLOR")
        self._handle = __import__("bpy").types.SpaceView3D.draw_handler_add(self.draw, (), "WINDOW", "POST_PIXEL")

    def remove_handler(self):
        if self._handle is None:
            return
        __import__("bpy").types.SpaceView3D.draw_handler_remove(self._handle, "WINDOW")
        self._handle = None

    def draw(self):
        started = perf_counter()
        context = __import__("bpy").context
        region = context.region
        if region is None:
            return
        show_overlay_debug = bool(getattr(self.runtime, "show_overlay_debug", False))

        rect = self.runtime.get_overlay_rect(context)
        texture = self.runtime.image_bridge.ensure_gpu_texture()
        image = self.runtime.image_bridge.image
        texture = texture if texture is not None else (gpu.texture.from_image(image) if image is not None else None)
        corner_radius = self._corner_radius(rect)
        gpu.state.blend_set("ALPHA")

        self._draw_shadow(rect, corner_radius)
        self._draw_window_background(rect, corner_radius)

        if texture is not None:
            batch = batch_for_shader(
                self._texture_shader,
                "TRI_FAN",
                {
                    "position": (
                        (rect["content_x"], rect["content_y"]),
                        (rect["content_x"] + rect["content_width"], rect["content_y"]),
                        (rect["content_x"] + rect["content_width"], rect["content_y"] + rect["content_height"]),
                        (rect["content_x"], rect["content_y"] + rect["content_height"]),
                    ),
                    "uv": ((0, 1), (1, 1), (1, 0), (0, 0)),
                },
            )
            self._texture_shader.bind()
            view_projection = gpu.matrix.get_projection_matrix() @ gpu.matrix.get_model_view_matrix()
            self._texture_shader.uniform_float("viewProjectionMatrix", view_projection)
            self._texture_shader.uniform_float("modelMatrix", Matrix.Identity(4))
            self._texture_shader.uniform_float(
                "clipRect",
                (
                    rect["content_x"],
                    rect["content_y"],
                    rect["content_width"],
                    rect["content_height"],
                ),
            )
            self._texture_shader.uniform_float("cornerRadius", corner_radius)
            self._texture_shader.uniform_float("edgeSoftness", 1.5)
            self._texture_shader.uniform_sampler("image", texture)
            batch.draw(self._texture_shader)

        self._draw_title_bar_separator(rect)

        if show_overlay_debug:
            blf.position(0, rect["x"] + 10, rect["title_bar_y"] + 7, 0)
            blf.size(0, 12)
            blf.draw(0, "Avalonia Bridge")

        if show_overlay_debug:
            border_batch = batch_for_shader(
                self._color_shader,
                "LINE_LOOP",
                {
                    "pos": self._rounded_outline_points(
                        rect["x"],
                        rect["y"],
                        rect["width"],
                        rect["height"],
                        corner_radius,
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
        self._stats["draw_ms"].append((perf_counter() - started) * 1000.0)

    def _create_texture_shader(self):
        shader_info = gpu.types.GPUShaderCreateInfo()
        shader_info.push_constant("MAT4", "viewProjectionMatrix")
        shader_info.push_constant("MAT4", "modelMatrix")
        shader_info.push_constant("VEC4", "clipRect")
        shader_info.push_constant("FLOAT", "cornerRadius")
        shader_info.push_constant("FLOAT", "edgeSoftness")
        shader_info.sampler(0, "FLOAT_2D", "image")
        shader_info.vertex_in(0, "VEC2", "position")
        shader_info.vertex_in(1, "VEC2", "uv")

        interface = gpu.types.GPUStageInterfaceInfo("renderbuilder_image_interface")
        interface.smooth("VEC2", "uvInterp")
        interface.smooth("VEC2", "screenPosInterp")
        shader_info.vertex_out(interface)
        shader_info.fragment_out(0, "VEC4", "FragColor")

        shader_info.vertex_source(
            """
            void main()
            {
                uvInterp = uv;
                screenPosInterp = position;
                gl_Position = viewProjectionMatrix * modelMatrix * vec4(position, 0.0, 1.0);
            }
            """
        )
        shader_info.fragment_source(
            """
            float roundedBoxSdf(vec2 point, vec2 halfSize, float radius)
            {
                vec2 q = abs(point) - max(halfSize - vec2(radius), vec2(0.0));
                return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
            }

            void main()
            {
                vec2 clipCenter = clipRect.xy + clipRect.zw * 0.5;
                vec2 localPoint = screenPosInterp - clipCenter;
                float distanceToEdge = roundedBoxSdf(localPoint, clipRect.zw * 0.5, cornerRadius);
                float mask = 1.0 - smoothstep(0.0, edgeSoftness, distanceToEdge);
                vec4 texel = texture(image, uvInterp);
                FragColor = vec4(texel.rgb, texel.a * mask);
            }
            """
        )
        return gpu.shader.create_from_info(shader_info)

    def _create_shadow_shader(self):
        shader_info = gpu.types.GPUShaderCreateInfo()
        shader_info.push_constant("MAT4", "viewProjectionMatrix")
        shader_info.push_constant("MAT4", "modelMatrix")
        shader_info.push_constant("VEC4", "shadowRect")
        shader_info.push_constant("VEC2", "shadowOffset")
        shader_info.push_constant("FLOAT", "cornerRadius")
        shader_info.push_constant("FLOAT", "shadowSoftness")
        shader_info.push_constant("VEC4", "shadowColor")
        shader_info.vertex_in(0, "VEC2", "position")

        interface = gpu.types.GPUStageInterfaceInfo("renderbuilder_shadow_interface")
        interface.smooth("VEC2", "screenPosInterp")
        shader_info.vertex_out(interface)
        shader_info.fragment_out(0, "VEC4", "FragColor")

        shader_info.vertex_source(
            """
            void main()
            {
                screenPosInterp = position;
                gl_Position = viewProjectionMatrix * modelMatrix * vec4(position, 0.0, 1.0);
            }
            """
        )
        shader_info.fragment_source(
            """
            float roundedBoxSdf(vec2 point, vec2 halfSize, float radius)
            {
                vec2 q = abs(point) - max(halfSize - vec2(radius), vec2(0.0));
                return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
            }

            void main()
            {
                vec2 rectCenter = shadowRect.xy + shadowRect.zw * 0.5 + shadowOffset;
                vec2 localPoint = screenPosInterp - rectCenter;
                float distanceToEdge = roundedBoxSdf(localPoint, shadowRect.zw * 0.5, cornerRadius);
                if (distanceToEdge <= 0.0)
                {
                    FragColor = vec4(0.0);
                    return;
                }

                float alpha = 1.0 - smoothstep(0.0, shadowSoftness, distanceToEdge);
                FragColor = vec4(shadowColor.rgb, shadowColor.a * alpha);
            }
            """
        )
        return gpu.shader.create_from_info(shader_info)

    def _corner_radius(self, rect):
        return max(10.0, min(22.0, rect["scale"] * 14.0))

    def _draw_shadow(self, rect, corner_radius):
        shadow_layers = (
            (28.0, (0.0, -8.0), 34.0, (0.0, 0.0, 0.0, 0.16)),
            (14.0, (0.0, -3.0), 16.0, (0.0, 0.0, 0.0, 0.11)),
        )
        for expand, offset, softness, color in shadow_layers:
            self._draw_shadow_pass(rect, corner_radius, expand, offset, softness, color)

    def _draw_window_background(self, rect, corner_radius):
        self._draw_rounded_fill(
            rect["x"],
            rect["y"],
            rect["width"],
            rect["height"],
            corner_radius,
            (0.10, 0.12, 0.16, 0.96),
        )

    def _draw_title_bar_separator(self, rect):
        separator_y = rect["title_bar_y"]
        separator_batch = batch_for_shader(
            self._color_shader,
            "LINES",
            {
                "pos": (
                    (rect["x"] + 12, separator_y),
                    (rect["x"] + rect["width"] - 12, separator_y),
                )
            },
        )
        self._color_shader.bind()
        self._color_shader.uniform_float("color", (1.0, 1.0, 1.0, 0.08))
        separator_batch.draw(self._color_shader)

    def _draw_shadow_pass(self, rect, corner_radius, expand, offset, softness, color):
        x = rect["x"] - expand
        y = rect["y"] - expand
        width = rect["width"] + expand * 2.0
        height = rect["height"] + expand * 2.0
        batch = batch_for_shader(
            self._shadow_shader,
            "TRI_FAN",
            {
                "position": (
                    (x, y),
                    (x + width, y),
                    (x + width, y + height),
                    (x, y + height),
                )
            },
        )
        self._shadow_shader.bind()
        view_projection = gpu.matrix.get_projection_matrix() @ gpu.matrix.get_model_view_matrix()
        self._shadow_shader.uniform_float("viewProjectionMatrix", view_projection)
        self._shadow_shader.uniform_float("modelMatrix", Matrix.Identity(4))
        self._shadow_shader.uniform_float(
            "shadowRect",
            (
                rect["x"],
                rect["y"],
                rect["width"],
                rect["height"],
            ),
        )
        self._shadow_shader.uniform_float("shadowOffset", offset)
        self._shadow_shader.uniform_float("cornerRadius", corner_radius)
        self._shadow_shader.uniform_float("shadowSoftness", softness)
        self._shadow_shader.uniform_float("shadowColor", color)
        batch.draw(self._shadow_shader)

    def _draw_rounded_fill(self, x, y, width, height, radius, color):
        points = self._rounded_outline_points(x, y, width, height, radius)
        center = (x + width * 0.5, y + height * 0.5)
        batch = batch_for_shader(
            self._color_shader,
            "TRI_FAN",
            {
                "pos": (center, *points)
            },
        )
        self._color_shader.bind()
        self._color_shader.uniform_float("color", color)
        batch.draw(self._color_shader)

    def _rounded_outline_points(self, x, y, width, height, radius):
        radius = max(0.0, min(radius, width * 0.5, height * 0.5))
        if radius <= 0.0:
            return (
                (x, y),
                (x + width, y),
                (x + width, y + height),
                (x, y + height),
            )

        arc_segments = 6
        corners = (
            (x + width - radius, y + radius, -0.5 * pi, 0.0),
            (x + width - radius, y + height - radius, 0.0, 0.5 * pi),
            (x + radius, y + height - radius, 0.5 * pi, pi),
            (x + radius, y + radius, pi, 1.5 * pi),
        )
        points = []
        for center_x, center_y, start_angle, end_angle in corners:
            for step in range(arc_segments + 1):
                angle = start_angle + (end_angle - start_angle) * (step / arc_segments)
                points.append((center_x + cos(angle) * radius, center_y + sin(angle) * radius))
        return tuple(points)

    def diagnostics_snapshot(self):
        samples = self._stats["draw_ms"]
        return {
            "draw_avg_ms": (sum(samples) / len(samples)) if samples else None,
        }
