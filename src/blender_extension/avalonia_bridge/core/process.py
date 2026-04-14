import os
import subprocess
from pathlib import Path


class ProcessLaunchError(RuntimeError):
    pass


def validate_executable_path(path_text):
    path = Path(path_text).expanduser()
    if not path_text:
        raise ProcessLaunchError("Avalonia executable path is empty.")
    if not path.exists():
        raise ProcessLaunchError(f"Path does not exist: {path}")
    if path.is_dir():
        raise ProcessLaunchError(f"Path points to a directory, not a file: {path}")
    if path.suffix.lower() not in {".exe", ".dll"}:
        raise ProcessLaunchError("Only .exe and .dll targets are supported in this MVP.")
    return path


def build_command(executable_path, host, port, width, height):
    path = validate_executable_path(executable_path)
    bridge_args = [
        "--blender-bridge", "true",
        "--blender-bridge-host", host,
        "--blender-bridge-port", str(port),
        "--blender-bridge-width", str(width),
        "--blender-bridge-height", str(height),
    ]
    args = [str(path), *bridge_args]
    if path.suffix.lower() == ".dll":
        args = ["dotnet", str(path), *bridge_args]
    return args, str(path.parent)


class ProcessManager:
    def __init__(self):
        self.process = None

    def start(self, executable_path, host, port, width, height):
        args, cwd = build_command(executable_path, host, port, width, height)
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        env = os.environ.copy()
        self.process = subprocess.Popen(
            args,
            cwd=cwd,
            env=env,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
            creationflags=creationflags,
        )
        return self.process

    def stop(self):
        if self.process is None:
            return
        process = self.process
        self.process = None
        if process.poll() is not None:
            return
        process.terminate()
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=3)

    def poll_exit(self):
        process = self.process
        if process is None:
            return None
        if process.poll() is None:
            return None

        self.process = None
        stderr_text = ""
        try:
            _, stderr_text = process.communicate(timeout=0.2)
        except Exception:
            pass

        return {
            "returncode": process.returncode,
            "stderr": (stderr_text or "").strip(),
        }
