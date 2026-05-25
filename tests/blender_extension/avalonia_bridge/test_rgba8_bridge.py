import sys
import unittest
import importlib
from unittest.mock import patch

from _test_support import import_module


class _RecordingImageBridge:
    def __init__(self):
        self.calls = []
        self.last_mode = "gpu"
        self.last_error = ""
        self.expects_gpu_draw = False

    def update_from_bgra(self, payload, width, height):
        self.calls.append(("bgra8", bytes(payload), width, height))

    def update_from_macos_iosurface(self, header, width, height):
        self.calls.append(("macos_iosurface", dict(header), width, height))

    def diagnostics_snapshot(self):
        return {
            "texture_update_avg_ms": None,
            "image_fallback_avg_ms": None,
        }


class Rgba8BridgeTests(unittest.TestCase):
    def test_controller_connect_requests_linear_shared_memory_contract(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        sent = []

        controller.shared_memory_bridge._backend._name = "TestSharedMemory"
        controller.shared_memory_bridge._backend._frame_size = 320
        controller.shared_memory_bridge._backend._slot_count = 2
        controller.send_message = lambda header, payload=b"": sent.append((dict(header), bytes(payload))) or True

        controller._on_connect(None)

        self.assertEqual(1, len(sent))
        header, payload = sent[0]
        self.assertEqual("init", header["type"])
        self.assertEqual("rgba32f_linear", header["pixel_format"])
        self.assertEqual(controller._render_width * 16, header["stride"])
        self.assertEqual(320, header["frame_size"])
        self.assertEqual(2, header["slot_count"])
        self.assertEqual(["shared_memory"], header["supported_frame_transports"])
        self.assertEqual(b"", payload)

    def test_controller_connect_advertises_macos_iosurface_when_native_hook_is_available(self):
        core = import_module("avalonia_bridge.core")
        native_gpu = importlib.import_module("avalonia_bridge.core.native_gpu")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        sent = []

        controller.shared_memory_bridge._backend._name = "TestSharedMemory"
        controller.shared_memory_bridge._backend._frame_size = 320
        controller.shared_memory_bridge._backend._slot_count = 2
        controller.send_message = lambda header, payload=b"": sent.append((dict(header), bytes(payload))) or True

        with patch.object(native_gpu, "available", return_value=True):
            controller._on_connect(None)

        header, _payload = sent[0]
        self.assertEqual(["macos_iosurface", "shared_memory"], header["supported_frame_transports"])

    def test_controller_tick_once_routes_bgra8_frames_to_bgra_image_path(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        image_bridge = _RecordingImageBridge()
        controller.image_bridge = image_bridge
        controller.frame_pipeline.image_bridge = image_bridge
        controller.frame_store.update(
            {"type": "frame", "seq": 1, "width": 2, "height": 1, "pixel_format": "bgra8"},
            b"\x01\x02\x03\x04\x05\x06\x07\x08",
        )

        controller.tick_once()

        self.assertEqual(
            [("bgra8", b"\x01\x02\x03\x04\x05\x06\x07\x08", 2, 1)],
            image_bridge.calls,
        )

    def test_controller_tick_once_routes_iosurface_frames_to_native_image_path(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        image_bridge = _RecordingImageBridge()
        controller.image_bridge = image_bridge
        controller.frame_pipeline.image_bridge = image_bridge
        header = {
            "type": "frame_ready",
            "seq": 1,
            "width": 2,
            "height": 1,
            "pixel_format": "bgra8_unorm",
            "frame_transport": "macos_iosurface",
            "handle_type": "iosurface",
            "handle_id": 42,
        }
        controller.frame_store.update(header, b"")

        controller.tick_once()

        self.assertEqual([("macos_iosurface", header, 2, 1)], image_bridge.calls)

    def test_image_bridge_uploads_linear_rgba_texture_via_float_buffer(self):
        image_bridge_module = import_module("avalonia_bridge.core.image_bridge")
        image_bridge = image_bridge_module.ImageBridge()
        gpu = sys.modules["gpu"]

        image_bridge.update_from_rgba32f_linear(b"\x00\x00\x80?\x00\x00\x00?\x00\x00\x00?\x00\x00\x80?", 1, 1)
        image_bridge.ensure_gpu_texture()

        self.assertEqual(1, len(gpu._buffer_calls))
        self.assertEqual("FLOAT", gpu._buffer_calls[0]["component_type"])
        self.assertEqual([1, 1, 4], gpu._buffer_calls[0]["dimensions"])
        self.assertEqual(1, len(gpu._texture_calls))
        self.assertEqual("RGBA32F", gpu._texture_calls[0]["format"])
        self.assertEqual((1, 1), gpu._texture_calls[0]["size"])

    def test_image_bridge_marks_iosurface_texture_for_bgra_srgb_shader_path(self):
        image_bridge_module = import_module("avalonia_bridge.core.image_bridge")
        native_gpu = importlib.import_module("avalonia_bridge.core.native_gpu")
        image_bridge = image_bridge_module.ImageBridge()

        with patch.object(native_gpu, "copy_iosurface_to_texture", return_value=True):
            image_bridge.update_from_macos_iosurface({"handle_id": 42}, 2, 1)

        self.assertTrue(image_bridge.texture_swizzle_bgra)
        self.assertTrue(image_bridge.texture_srgb_to_linear)


if __name__ == "__main__":
    unittest.main()
