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


if __name__ == "__main__":
    unittest.main()
