import unittest
from types import SimpleNamespace

from _test_support import import_module


class ControllerInputCaptureTests(unittest.TestCase):
    def test_title_bar_drag_updates_overlay_offset_for_tweak_events(self):
        core = import_module("avalonia_bridge.core")
        host = core.View3DOverlayHost()
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"), host=host)
        import bpy

        context = bpy.context
        rect = host.get_overlay_rect(context)
        start_x = rect["x"] + 24
        start_y = rect["title_bar_y"] + 8

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="LEFTMOUSE",
                value="PRESS",
                mouse_region_x=start_x,
                mouse_region_y=start_y,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertTrue(handled)
        self.assertTrue(controller.state_snapshot().capture_input)

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="EVT_TWEAK_L",
                value="CLICK_DRAG",
                mouse_region_x=start_x + 36,
                mouse_region_y=start_y + 20,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertTrue(handled)
        self.assertTrue(controller.state_snapshot().capture_input)
        snapshot = controller.state_snapshot()
        self.assertEqual(36, snapshot.overlay_offset_x)
        self.assertEqual(20, snapshot.overlay_offset_y)

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="EVT_TWEAK_L",
                value="RELEASE",
                mouse_region_x=start_x + 36,
                mouse_region_y=start_y + 20,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertTrue(handled)
        self.assertFalse(controller.state_snapshot().capture_input)

    def test_title_bar_drag_reroutes_unrecognized_mouse_events_until_release(self):
        core = import_module("avalonia_bridge.core")
        host = core.View3DOverlayHost()
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"), host=host)
        import bpy

        context = bpy.context
        rect = host.get_overlay_rect(context)
        start_x = rect["x"] + 24
        start_y = rect["title_bar_y"] + 8

        controller.handle_event(
            context,
            SimpleNamespace(
                type="LEFTMOUSE",
                value="PRESS",
                mouse_region_x=start_x,
                mouse_region_y=start_y,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="WINDOW_DEACTIVATE",
                value="NOTHING",
                mouse_region_x=start_x + 10,
                mouse_region_y=start_y + 10,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertTrue(handled)

        controller.handle_event(
            context,
            SimpleNamespace(
                type="LEFTMOUSE",
                value="RELEASE",
                mouse_region_x=start_x + 10,
                mouse_region_y=start_y + 10,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        handled = controller.handle_event(
            context,
            SimpleNamespace(
                type="WINDOW_DEACTIVATE",
                value="NOTHING",
                mouse_region_x=start_x + 10,
                mouse_region_y=start_y + 10,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertFalse(handled)

    def test_pointer_up_is_forwarded_after_pointer_leaves_overlay(self):
        core = import_module("avalonia_bridge.core")
        host = core.View3DOverlayHost()
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"), host=host)
        import bpy

        context = bpy.context
        rect = host.get_overlay_rect(context)
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

    def test_right_button_up_is_forwarded_after_pointer_leaves_overlay(self):
        core = import_module("avalonia_bridge.core")
        host = core.View3DOverlayHost()
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"), host=host)
        import bpy

        context = bpy.context
        rect = host.get_overlay_rect(context)
        sent = []
        controller.send_message = lambda header, payload=b"": sent.append(dict(header)) or True

        inside_x = rect["content_x"] + 10
        inside_y = rect["content_y"] + 10
        outside_x = rect["content_x"] - 10
        outside_y = rect["content_y"] + 10

        controller.handle_event(
            context,
            SimpleNamespace(
                type="RIGHTMOUSE",
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
                type="RIGHTMOUSE",
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
        self.assertEqual(["right", "right"], [packet["button"] for packet in sent])

    def test_middle_button_up_is_forwarded_after_pointer_leaves_overlay(self):
        core = import_module("avalonia_bridge.core")
        host = core.View3DOverlayHost()
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"), host=host)
        import bpy

        context = bpy.context
        rect = host.get_overlay_rect(context)
        sent = []
        controller.send_message = lambda header, payload=b"": sent.append(dict(header)) or True

        inside_x = rect["content_x"] + 10
        inside_y = rect["content_y"] + 10
        outside_x = rect["content_x"] - 10
        outside_y = rect["content_y"] + 10

        controller.handle_event(
            context,
            SimpleNamespace(
                type="MIDDLEMOUSE",
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
                type="MIDDLEMOUSE",
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
        self.assertEqual(["middle", "middle"], [packet["button"] for packet in sent])

    def test_controller_without_host_does_not_consume_events(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))
        import bpy

        handled = controller.handle_event(
            bpy.context,
            SimpleNamespace(
                type="MOUSEMOVE",
                value="NOTHING",
                mouse_region_x=10,
                mouse_region_y=10,
                shift=False,
                ctrl=False,
                alt=False,
                unicode="",
            ),
        )

        self.assertFalse(handled)


if __name__ == "__main__":
    unittest.main()
