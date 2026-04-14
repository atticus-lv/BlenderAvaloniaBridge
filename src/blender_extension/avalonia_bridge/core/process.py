from __future__ import annotations

import os
import shutil
import subprocess
import threading
from collections import deque
from pathlib import Path
from typing import TypedDict


CommandLine = list[str]


class ProcessExitReport(TypedDict):
    returncode: int
    stderr: str


class ProcessLaunchError(RuntimeError):
    pass


def validate_executable_path(path_text: str) -> Path:
    path = Path(path_text).expanduser()
    if not path_text:
        raise ProcessLaunchError("Avalonia executable path is empty.")
    if not path.exists():
        raise ProcessLaunchError(f"Path does not exist: {path}")
    if path.is_dir():
        raise ProcessLaunchError(f"Path points to a directory, not a file: {path}")
    if path.suffix.lower() in {".exe", ".dll"}:
        return path
    if os.access(path, os.X_OK):
        return path
    raise ProcessLaunchError("Supported targets are .dll, .exe, or an executable file.")
    return path


def build_command(
    executable_path: str,
    host: str,
    port: int,
    width: int,
    height: int,
    render_scaling: float,
    window_mode: str,
    supports_business: bool,
    supports_frames: bool,
    supports_input: bool,
) -> tuple[CommandLine, str]:
    path = validate_executable_path(executable_path)
    bridge_args = [
        "--blender-bridge", "true",
        "--blender-bridge-host", host,
        "--blender-bridge-port", str(port),
        "--blender-bridge-width", str(width),
        "--blender-bridge-height", str(height),
        "--blender-bridge-render-scaling", str(render_scaling),
        "--blender-bridge-window-mode", str(window_mode),
        "--blender-bridge-supports-business", str(bool(supports_business)).lower(),
        "--blender-bridge-supports-frames", str(bool(supports_frames)).lower(),
        "--blender-bridge-supports-input", str(bool(supports_input)).lower(),
    ]
    args = [str(path), *bridge_args]
    if path.suffix.lower() == ".dll":
        args = [_resolve_dotnet_host(), str(path), *bridge_args]
    return args, str(path.parent)


def _resolve_dotnet_host() -> str:
    resolved = shutil.which("dotnet")
    if resolved:
        return resolved

    for candidate in (
        Path("/usr/local/share/dotnet/dotnet"),
        Path("/opt/homebrew/bin/dotnet"),
        Path("/usr/local/bin/dotnet"),
    ):
        if candidate.exists():
            return str(candidate)

    raise ProcessLaunchError("Unable to locate 'dotnet'. Install .NET or use a published executable path instead of a DLL.")


class ProcessManager:
    def __init__(self):
        self.process: subprocess.Popen[str] | None = None
        self._stderr_lines: deque[str] = deque(maxlen=200)
        self._stderr_lock = threading.Lock()
        self._stderr_thread: threading.Thread | None = None

    def start(
        self,
        executable_path: str,
        host: str,
        port: int,
        width: int,
        height: int,
        render_scaling: float,
        window_mode: str,
        supports_business: bool,
        supports_frames: bool,
        supports_input: bool,
    ) -> subprocess.Popen[str]:
        args, cwd = build_command(
            executable_path,
            host,
            port,
            width,
            height,
            render_scaling,
            window_mode,
            supports_business,
            supports_frames,
            supports_input,
        )
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        env = os.environ.copy()
        with self._stderr_lock:
            self._stderr_lines.clear()
        self._stderr_thread = None
        self.process = subprocess.Popen(
            args,
            cwd=cwd,
            env=env,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
            creationflags=creationflags,
        )
        self._start_stderr_drain(self.process)
        return self.process

    def stop(self):
        if self.process is None:
            return
        process = self.process
        self.process = None
        if process.poll() is not None:
            self._finalize_stderr_drain(process)
            return
        process.terminate()
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=3)
        finally:
            self._finalize_stderr_drain(process)

    def poll_exit(self) -> ProcessExitReport | None:
        process = self.process
        if process is None:
            return None
        if process.poll() is None:
            return None

        self.process = None
        self._finalize_stderr_drain(process)
        with self._stderr_lock:
            stderr_text = "\n".join(self._stderr_lines).strip()

        return {
            "returncode": process.returncode,
            "stderr": (stderr_text or "").strip(),
        }

    def _start_stderr_drain(self, process: subprocess.Popen[str]):
        if process.stderr is None:
            return

        def _drain():
            stderr = process.stderr
            if stderr is None:
                return
            try:
                for line in stderr:
                    with self._stderr_lock:
                        self._stderr_lines.append(line.rstrip())
            except Exception:
                pass

        self._stderr_thread = threading.Thread(target=_drain, name="AvaloniaBridgeStderrDrain", daemon=True)
        self._stderr_thread.start()

    def _finalize_stderr_drain(self, process: subprocess.Popen[str]):
        drain_thread = self._stderr_thread
        if drain_thread is not None and drain_thread.is_alive():
            drain_thread.join(timeout=0.3)
        self._stderr_thread = None

        try:
            if process.stderr is not None and not process.stderr.closed:
                remaining = process.stderr.read()
                if remaining:
                    with self._stderr_lock:
                        for line in remaining.splitlines():
                            self._stderr_lines.append(line.rstrip())
        except Exception:
            pass
        finally:
            try:
                if process.stderr is not None and not process.stderr.closed:
                    process.stderr.close()
            except Exception:
                pass
