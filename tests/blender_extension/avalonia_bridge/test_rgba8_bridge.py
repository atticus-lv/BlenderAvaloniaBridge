import sys
import unittest

from _test_support import import_module


class _RecordingImageBridge:
    def __init__(self):
        self.calls = []
        self.last_mode = "gpu"
        self.last_error = ""
        self.expects_gpu_draw = False

    def update_from_rgba8(self, payload, width, height):
        self.calls.append(("rgba8", bytes(payload), width, height))

    def diagnostics_snapshot(self):
        return {
            "texture_update_avg_ms": None,
            "image_fallback_avg_ms": None,
        }


class Rgba8BridgeTests(unittest.TestCase):
    def test_controller_connect_requests_rgba8_shared_memory_contract(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        sent = []

        controller.shared_memory_bridge._name = "TestSharedMemory"
        controller.shared_memory_bridge._frame_size = 320
        controller.shared_memory_bridge._slot_count = 2
        controller.send_message = lambda header, payload=b"": sent.append((dict(header), bytes(payload))) or True

        controller._on_connect(None)

        self.assertEqual(1, len(sent))
        header, payload = sent[0]
        self.assertEqual("init", header["type"])
        self.assertEqual("rgba8", header["pixel_format"])
        self.assertEqual(controller._render_width * 4, header["stride"])
        self.assertEqual(320, header["frame_size"])
        self.assertEqual(2, header["slot_count"])
        self.assertEqual(b"", payload)

    def test_controller_tick_once_routes_rgba8_frames_to_rgba8_image_path(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        image_bridge = _RecordingImageBridge()
        controller.image_bridge = image_bridge
        controller.frame_pipeline.image_bridge = image_bridge
        controller.frame_store.update(
            {"type": "frame", "seq": 1, "width": 2, "height": 1, "pixel_format": "rgba8"},
            b"\x01\x02\x03\x04\x05\x06\x07\x08",
        )

        controller.tick_once()

        self.assertEqual(
            [("rgba8", b"\x01\x02\x03\x04\x05\x06\x07\x08", 2, 1)],
            image_bridge.calls,
        )

    def test_image_bridge_uploads_rgba8_texture_via_ubyte_buffer(self):
        image_bridge_module = import_module("avalonia_bridge.core.image_bridge")
        image_bridge = image_bridge_module.ImageBridge()
        gpu = sys.modules["gpu"]

        image_bridge.update_from_rgba8(b"\x0F\x17\x2A\xFF", 1, 1)
        image_bridge.ensure_gpu_texture()

        self.assertEqual(1, len(gpu._buffer_calls))
        self.assertEqual("UBYTE", gpu._buffer_calls[0]["component_type"])
        self.assertEqual([1, 1, 4], gpu._buffer_calls[0]["dimensions"])
        self.assertEqual(1, len(gpu._texture_calls))
        self.assertEqual("RGBA8", gpu._texture_calls[0]["format"])
        self.assertEqual((1, 1), gpu._texture_calls[0]["size"])


if __name__ == "__main__":
    unittest.main()
