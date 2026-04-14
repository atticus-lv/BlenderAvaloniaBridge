import unittest
from types import SimpleNamespace

from _test_support import import_module


class AddonAdapterTests(unittest.TestCase):
    def test_runtime_adapter_syncs_controller_snapshot_to_property_group(self):
        runtime = import_module("avalonia_bridge.runtime")

        state = SimpleNamespace(
            process_running=False,
            connected=False,
            capture_input=False,
            last_error="",
            last_message="",
            overlay_width=1100,
            overlay_height=760,
            remote_window_mode="unknown",
            remote_supports_business=True,
            remote_supports_frames=True,
            remote_supports_input=True,
            process_id=0,
            listen_port=0,
            overlay_offset_x=0,
            overlay_offset_y=0,
        )
        snapshot = runtime.BridgeStateSnapshot(
            process_running=True,
            connected=True,
            capture_input=True,
            process_id=123,
            listen_port=34567,
            last_error="",
            last_message="ready",
            width=1100,
            height=760,
            remote_window_mode="desktop",
            remote_supports_business=True,
            remote_supports_frames=False,
            remote_supports_input=False,
            overlay_offset_x=12,
            overlay_offset_y=34,
        )

        runtime.apply_state_snapshot(state, snapshot)

        self.assertTrue(state.process_running)
        self.assertTrue(state.connected)
        self.assertEqual(123, state.process_id)
        self.assertEqual(34567, state.listen_port)
        self.assertEqual("ready", state.last_message)
        self.assertEqual("desktop", state.remote_window_mode)
        self.assertFalse(state.remote_supports_frames)
        self.assertFalse(state.remote_supports_input)
        self.assertEqual(12, state.overlay_offset_x)
        self.assertEqual(34, state.overlay_offset_y)


if __name__ == "__main__":
    unittest.main()
