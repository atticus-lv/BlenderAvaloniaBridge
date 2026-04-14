import tempfile
import unittest
from pathlib import Path

from _test_support import import_module


class ProcessTests(unittest.TestCase):
    def test_build_command_uses_executable_directly(self):
        process = import_module("avalonia_bridge.core.process")

        with tempfile.TemporaryDirectory() as temp_dir:
            executable = Path(temp_dir) / "bridge.exe"
            executable.write_bytes(b"")

            args, cwd = process.build_command(str(executable), "127.0.0.1", 34567, 800, 600, 1.25)

        self.assertEqual(str(executable), args[0])
        self.assertEqual(str(executable.parent), cwd)
        self.assertIn("--blender-bridge", args)
        self.assertIn("--blender-bridge-port", args)
        self.assertIn("34567", args)
        self.assertIn("--blender-bridge-render-scaling", args)
        self.assertIn("1.25", args)


if __name__ == "__main__":
    unittest.main()
