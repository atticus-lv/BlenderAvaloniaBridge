import unittest

from _test_support import import_module


class ControllerStateTests(unittest.TestCase):
    def test_controller_publishes_state_callback_and_snapshot(self):
        core = import_module("avalonia_bridge.core")
        updates = []

        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            state_callback=updates.append,
        )

        snapshot = controller.state_snapshot()

        self.assertFalse(snapshot.process_running)
        self.assertEqual("", snapshot.last_error)
        self.assertGreaterEqual(len(updates), 1)

    def test_controller_starts_and_stops_host_when_present(self):
        core = import_module("avalonia_bridge.core")

        class RecordingHost:
            def __init__(self):
                self.attached = None
                self.started = 0
                self.stopped = 0

            def attach(self, controller):
                self.attached = controller

            def start(self, context=None):
                self.started += 1

            def stop(self):
                self.stopped += 1

            def handle_event(self, context, event):
                return False

            def tag_redraw(self):
                return None

            def diagnostics_snapshot(self):
                return {"draw_avg_ms": None}

        host = RecordingHost()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            host=host,
        )

        self.assertIs(controller, host.attached)
        controller.set_host(None)

        self.assertEqual(1, host.stopped)

    def test_view3d_host_registers_overlay_and_redraws_only_view3d(self):
        core = import_module("avalonia_bridge.core")
        import bpy

        host = core.View3DOverlayHost()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            host=host,
        )

        host.start()
        host.tag_redraw()
        host.stop()

        self.assertEqual(1, len(bpy._draw_handler_add_calls))
        self.assertEqual(1, len(bpy._draw_handler_remove_calls))
        self.assertTrue(bpy._redraw_calls)
        self.assertTrue(all(area_type == "VIEW_3D" for area_type in bpy._redraw_calls))

    def test_view3d_host_overlay_rect_is_stable_before_first_frame(self):
        core = import_module("avalonia_bridge.core")
        import bpy

        bpy.context.preferences.system.dpi = 192
        host = core.View3DOverlayHost()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            host=host,
        )

        rect_before_frame = host.get_overlay_rect(bpy.context)

        controller.image_bridge.update_from_rgba32f_linear(b"\x00" * (64 * 64 * 16), 64, 64)
        rect_after_pending_frame = host.get_overlay_rect(bpy.context)

        self.assertEqual(rect_before_frame["width"], rect_after_pending_frame["width"])
        self.assertEqual(rect_before_frame["height"], rect_after_pending_frame["height"])
        self.assertEqual(rect_before_frame["title_bar_height"], rect_after_pending_frame["title_bar_height"])

    def test_view3d_host_places_title_bar_above_content(self):
        core = import_module("avalonia_bridge.core")
        import bpy

        host = core.View3DOverlayHost()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            host=host,
        )

        rect = host.get_overlay_rect(bpy.context)

        self.assertEqual(rect["y"], rect["content_y"])
        self.assertEqual(rect["content_y"] + rect["content_height"], rect["title_bar_y"])


if __name__ == "__main__":
    unittest.main()
