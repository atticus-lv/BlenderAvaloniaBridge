import tempfile
import types
import unittest
from pathlib import Path

from _test_support import import_module


class _FakeServer:
    def __init__(self, host, on_packet, on_connect, on_disconnect, on_error):
        self.host = host
        self.port = 43123
        self.on_packet = on_packet
        self.on_connect = on_connect
        self.on_disconnect = on_disconnect
        self.on_error = on_error

    def start(self):
        return None

    def stop(self):
        return None

    def send(self, header, payload):
        return True


class _RecordingProcessManager:
    def __init__(self):
        self.calls = []

    def start(self, executable_path, host, port, width, height, render_scaling, window_mode, supports_business, supports_frames, supports_input):
        self.calls.append(
            {
                "executable_path": executable_path,
                "host": host,
                "port": port,
                "width": width,
                "height": height,
                "render_scaling": render_scaling,
                "window_mode": window_mode,
                "supports_business": supports_business,
                "supports_frames": supports_frames,
                "supports_input": supports_input,
            }
        )
        return types.SimpleNamespace(pid=2468)

    def stop(self):
        return None


class _RecordingSharedMemoryBridge:
    def __init__(self):
        self.frame_size = 0
        self.slot_count = 0
        self.name = "TestSharedMemory"

    def create(self, frame_size, slot_count=2):
        self.frame_size = frame_size
        self.slot_count = slot_count
        return self.name

    def close(self):
        return None


class ControllerRenderScalingTests(unittest.TestCase):
    def test_start_uses_scaled_render_size_for_shared_memory(self):
        core = import_module("avalonia_bridge.core")

        with tempfile.TemporaryDirectory() as temp_dir:
            executable = Path(temp_dir) / "bridge.exe"
            executable.write_bytes(b"")
            process_manager = _RecordingProcessManager()
            controller = core.BridgeController(
                core.BridgeConfig(
                    executable_path=str(executable),
                    width=1100,
                    height=760,
                    render_scaling=1.25,
                ),
                process_manager=process_manager,
                server_factory=_FakeServer,
            )
            shared_memory_bridge = _RecordingSharedMemoryBridge()
            controller.shared_memory_bridge = shared_memory_bridge
            controller.frame_pipeline.shared_memory_bridge = shared_memory_bridge

            controller.start()

        self.assertEqual(1, len(process_manager.calls))
        self.assertEqual(1100, controller.state_snapshot().width)
        self.assertEqual(760, controller.state_snapshot().height)
        self.assertEqual(1100, process_manager.calls[0]["width"])
        self.assertEqual(760, process_manager.calls[0]["height"])
        self.assertEqual(1.25, process_manager.calls[0]["render_scaling"])
        self.assertEqual(1375 * 950 * 16, controller.shared_memory_bridge.frame_size)


if __name__ == "__main__":
    unittest.main()
