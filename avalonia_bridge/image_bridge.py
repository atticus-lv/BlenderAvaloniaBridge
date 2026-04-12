from array import array

import bpy


class ImageBridge:
    IMAGE_NAME = "RenderBuilderAvaloniaBridge"

    def __init__(self):
        self._image = None

    @property
    def image(self):
        return self._image

    def clear(self):
        self._image = None

    def ensure_image(self, width, height):
        image = bpy.data.images.get(self.IMAGE_NAME)
        if image is None:
            image = bpy.data.images.new(self.IMAGE_NAME, width=width, height=height, alpha=True, float_buffer=False)
        elif image.size[0] != width or image.size[1] != height:
            image.scale(width, height)
        image.generated_type = "BLANK"
        self._image = image
        return image

    def update_from_bgra(self, payload, width, height):
        image = self.ensure_image(width, height)
        floats = array("f", [0.0]) * (width * height * 4)
        row_stride = width * 4
        for src_row in range(height):
            dst_row = height - 1 - src_row
            src_base = src_row * row_stride
            dst_base = dst_row * row_stride
            for col in range(width):
                src = src_base + col * 4
                dst = dst_base + col * 4
                blue = payload[src] / 255.0
                green = payload[src + 1] / 255.0
                red = payload[src + 2] / 255.0
                alpha = payload[src + 3] / 255.0
                floats[dst] = red
                floats[dst + 1] = green
                floats[dst + 2] = blue
                floats[dst + 3] = alpha
        image.pixels.foreach_set(floats)
        image.update()
        return image
