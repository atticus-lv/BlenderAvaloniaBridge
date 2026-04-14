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

            args, cwd = process.build_command(
                str(executable),
                "127.0.0.1",
                34567,
                800,
                600,
                1.25,
                "headless",
                True,
                True,
                True,
            )

        self.assertEqual(str(executable), args[0])
        self.assertEqual(str(executable.parent), cwd)
        self.assertIn("--blender-bridge", args)
        self.assertIn("--blender-bridge-port", args)
        self.assertIn("34567", args)
        self.assertIn("--blender-bridge-render-scaling", args)
        self.assertIn("1.25", args)
        self.assertIn("--blender-bridge-window-mode", args)
        self.assertIn("headless", args)

    def test_validate_executable_path_accepts_executable_without_windows_suffix(self):
        process = import_module("avalonia_bridge.core.process")

        with tempfile.TemporaryDirectory() as temp_dir:
            executable = Path(temp_dir) / "bridge"
            executable.write_text("#!/bin/sh\nexit 0\n", encoding="utf-8")
            executable.chmod(0o755)

            resolved = process.validate_executable_path(str(executable))

        self.assertEqual(executable, resolved)

    def test_build_command_uses_resolved_dotnet_host_for_dll(self):
        process = import_module("avalonia_bridge.core.process")

        with tempfile.TemporaryDirectory() as temp_dir:
            executable = Path(temp_dir) / "bridge.dll"
            executable.write_bytes(b"")

            with unittest.mock.patch.object(process, "_resolve_dotnet_host", return_value="/custom/dotnet"):
                args, _cwd = process.build_command(
                    str(executable),
                    "127.0.0.1",
                    34567,
                    800,
                    600,
                    1.0,
                    "headless",
                    True,
                    True,
                    True,
                )

        self.assertEqual("/custom/dotnet", args[0])
        self.assertEqual(str(executable), args[1])


if __name__ == "__main__":
    unittest.main()
