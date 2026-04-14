import unittest
from types import SimpleNamespace

from _test_support import import_module


class ControllerInputCaptureTests(unittest.TestCase):
    def test_pointer_up_is_forwarded_after_pointer_leaves_overlay(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        import bpy

        context = bpy.context
        rect = controller.get_overlay_rect(context)
        sent = []
        controller.send_message = lambda header, payload=b"": sent.append(dict(header)) or True

        inside_x = rect["content_x"] + 10
        inside_y = rect["content_y"] + 10
        outside_x = rect["content_x"] - 10
        outside_y = rect["content_y"] + 10

        controller.handle_event(
            context,
            SimpleNamespace(
                type="LEFTMOUSE",
                value="PRESS",
                mouse_region_x=inside_x,
                mouse_region_y=inside_y,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        controller.handle_event(
            context,
            SimpleNamespace(
                type="MOUSEMOVE",
                value="NOTHING",
                mouse_region_x=outside_x,
                mouse_region_y=outside_y,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="LEFTMOUSE",
                value="RELEASE",
                mouse_region_x=outside_x,
                mouse_region_y=outside_y,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertTrue(handled)
        self.assertEqual(["pointer_down", "pointer_up"], [packet["type"] for packet in sent])


if __name__ == "__main__":
    unittest.main()
