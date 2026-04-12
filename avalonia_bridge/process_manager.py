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
    args = [str(path), "--bridge", "true", "--host", host, "--port", str(port), "--width", str(width), "--height", str(height)]
    if path.suffix.lower() == ".dll":
        args = ["dotnet", str(path), "--bridge", "true", "--host", host, "--port", str(port), "--width", str(width), "--height", str(height)]
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
