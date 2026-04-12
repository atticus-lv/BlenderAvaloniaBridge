import bpy
import gpu
from array import array
from collections import deque
from time import perf_counter


class ImageBridge:
    IMAGE_NAME = "RenderBuilderAvaloniaBridge"

    def __init__(self):
        self._image = None
        self._texture = None
        self._texture_size = (0, 0)
        self._last_mode = "none"
        self._last_error = ""
        self._pending_rgba_bytes = None
        self._pending_rgba_float_source = None
        self._pending_size = (0, 0)
        self._stats = {
            "texture_update_ms": deque(maxlen=60),
            "image_fallback_ms": deque(maxlen=60),
        }

    @property
    def image(self):
        return self._image

    @property
    def texture(self):
        return self._texture

    @property
    def last_mode(self):
        return self._last_mode

    @property
    def last_error(self):
        return self._last_error

    @property
    def expects_gpu_draw(self):
        return (
            self._texture is not None
            or self._pending_rgba_float_source is not None
            or self._last_mode == "gpu"
        )

    def clear(self):
        self._image = None
        self._texture = None
        self._texture_size = (0, 0)
        self._last_mode = "none"
        self._last_error = ""
        self._pending_rgba_bytes = None
        self._pending_rgba_float_source = None
        self._pending_size = (0, 0)
        self._stats["texture_update_ms"].clear()
        self._stats["image_fallback_ms"].clear()

    def ensure_image(self, width, height):
        image = bpy.data.images.get(self.IMAGE_NAME)
        if image is None:
            image = bpy.data.images.new(self.IMAGE_NAME, width=width, height=height, alpha=True, float_buffer=False)
        elif image.size[0] != width or image.size[1] != height:
            image.scale(width, height)
        image.generated_type = "BLANK"
        self._configure_color_management(image)
        self._image = image
        return image

    def _configure_color_management(self, image):
        image.alpha_mode = "STRAIGHT"
        image.use_view_as_render = False

        colorspace = image.colorspace_settings
        try:
            colorspace.name = "Raw"
        except (TypeError, ValueError):
            try:
                colorspace.name = "Non-Color"
            except (TypeError, ValueError):
                if hasattr(colorspace, "is_data"):
                    try:
                        colorspace.is_data = True
                    except AttributeError:
                        pass
        else:
            if hasattr(colorspace, "is_data"):
                try:
                    colorspace.is_data = True
                except AttributeError:
                    pass

    def update_from_bgra(self, payload, width, height):
        rgba_floats = array("f", [0.0]) * (width * height * 4)
        row_stride = width * 4
        for src_row in range(height):
            dst_row = height - 1 - src_row
            src_base = src_row * row_stride
            dst_base = dst_row * row_stride
            for col in range(width):
                src = src_base + col * 4
                dst = dst_base + col * 4
                rgba_floats[dst] = payload[src + 2] / 255.0
                rgba_floats[dst + 1] = payload[src + 1] / 255.0
                rgba_floats[dst + 2] = payload[src] / 255.0
                rgba_floats[dst + 3] = payload[src + 3] / 255.0

        self._pending_rgba_float_source = rgba_floats
        self._pending_rgba_bytes = bytes(int(max(0.0, min(1.0, value)) * 255.0 + 0.5) for value in rgba_floats)
        self._pending_size = (width, height)
        return self._texture

    def update_from_rgba32f_linear(self, payload, width, height):
        self._pending_rgba_float_source = payload
        self._pending_rgba_bytes = None
        self._pending_size = (width, height)
        return self._texture

    def ensure_gpu_texture(self):
        if self._pending_rgba_float_source is None:
            return self._texture

        width, height = self._pending_size
        started = perf_counter()
        try:
            self._update_texture(self._pending_rgba_float_source, width, height)
        except Exception as exc:
            self._texture = None
            self._texture_size = (0, 0)
            self._last_error = f"gpu texture upload failed: {exc}"
            fallback_started = perf_counter()
            self._update_image_from_rgba_source(self._pending_rgba_float_source, width, height)
            self._stats["image_fallback_ms"].append((perf_counter() - fallback_started) * 1000.0)
            self._last_mode = "image"
        else:
            self._last_mode = "gpu"
            self._last_error = ""
            self._stats["texture_update_ms"].append((perf_counter() - started) * 1000.0)
        finally:
            self._pending_rgba_bytes = None
            self._pending_rgba_float_source = None
            self._pending_size = (0, 0)

        return self._texture

    def _coerce_float_source(self, rgba_source):
        if isinstance(rgba_source, array):
            return rgba_source
        try:
            return memoryview(rgba_source).cast("f")
        except TypeError:
            floats = array("f")
            floats.frombytes(rgba_source)
            return floats

    def _update_texture(self, rgba_source, width, height):
        float_source = self._coerce_float_source(rgba_source)
        buffer = gpu.types.Buffer("FLOAT", [height, width, 4], float_source)
        self._texture = gpu.types.GPUTexture((width, height), format="RGBA32F", data=buffer)
        self._texture_size = (width, height)

    def _update_image_from_rgba_source(self, rgba_source, width, height):
        image = self.ensure_image(width, height)
        image.pixels.foreach_set(self._coerce_float_source(rgba_source))
        image.update()
        self._image = image

    def diagnostics_snapshot(self):
        texture_samples = self._stats["texture_update_ms"]
        fallback_samples = self._stats["image_fallback_ms"]
        return {
            "texture_update_avg_ms": (sum(texture_samples) / len(texture_samples)) if texture_samples else None,
            "image_fallback_avg_ms": (sum(fallback_samples) / len(fallback_samples)) if fallback_samples else None,
        }
