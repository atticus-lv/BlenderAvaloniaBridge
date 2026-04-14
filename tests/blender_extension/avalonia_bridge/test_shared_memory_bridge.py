import sys
import unittest
from unittest.mock import patch

from _test_support import import_module


class _FakeMapping:
    def __init__(self):
        self.closed = False

    def close(self):
        self.closed = True

    def seek(self, _offset):
        return None

    def read(self, size):
        return b"\x00" * size


class SharedMemoryBridgeTests(unittest.TestCase):
    def test_backend_selection_uses_windows_backend_on_win32(self):
        module = import_module("avalonia_bridge.core.shared_memory_bridge")

        with patch.object(module.sys, "platform", "win32"):
            bridge = module.SharedMemoryBridge()
            self.assertEqual("_WindowsSharedMemoryBackend", bridge._backend.__class__.__name__)

    @unittest.skipUnless(sys.platform == "darwin", "POSIX shared memory test requires macOS")
    def test_darwin_backend_creates_temp_mapped_file(self):
        module = import_module("avalonia_bridge.core.shared_memory_bridge")

        backend = module._DarwinSharedMemoryBackend()

        with patch.object(module.tempfile, "mkstemp", return_value=(123, "/tmp/avb_test.bin")), \
                patch.object(module.os, "ftruncate"), \
                patch.object(module.mmap, "mmap", return_value=_FakeMapping()), \
                patch.object(module.os, "close"), \
                patch.object(module.os, "unlink"):
            name = backend.create(64, slot_count=2)

            self.assertEqual("/tmp/avb_test.bin", name)
            self.assertEqual(64, backend.frame_size)
            self.assertEqual(2, backend.slot_count)
            backend.close()
