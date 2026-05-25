from __future__ import annotations

import ctypes
import json
import os
import sys
import threading
from pathlib import Path
from typing import Any

_DLL: Any | None = None
_STATUS = "Native GPU hook not loaded."
_AVAILABLE = False
_LOAD_LOCK = threading.RLock()


def available(context=None) -> bool:
    if not ensure_loaded(context):
        return False
    try:
        return bool(_DLL.ava_blender_native_available())
    except Exception:
        return False


def ensure_loaded(context=None) -> bool:
    global _DLL, _STATUS, _AVAILABLE
    with _LOAD_LOCK:
        if _DLL is not None:
            return _AVAILABLE

        path = _find_library(context)
        if path is None:
            _STATUS = "avalonia_bridge_native was not found. Build src/blender_native first or configure its path."
            return False

        try:
            dll = ctypes.CDLL(str(path))
            dll.ava_blender_native_install.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p]
            dll.ava_blender_native_install.restype = ctypes.c_int
            dll.ava_blender_native_available.argtypes = []
            dll.ava_blender_native_available.restype = ctypes.c_int
            dll.ava_blender_native_copy_iosurface_to_texture.argtypes = [
                ctypes.c_uint32,
                ctypes.c_uint64,
                ctypes.c_int,
                ctypes.c_int,
            ]
            dll.ava_blender_native_copy_iosurface_to_texture.restype = ctypes.c_int
            dll.ava_blender_native_texture_info.argtypes = [
                ctypes.c_uint64,
                ctypes.POINTER(ctypes.c_int),
                ctypes.POINTER(ctypes.c_int),
                ctypes.c_char_p,
                ctypes.c_uint32,
            ]
            dll.ava_blender_native_texture_info.restype = ctypes.c_int
            dll.ava_blender_native_status_json.argtypes = [ctypes.c_char_p, ctypes.c_uint32]
            dll.ava_blender_native_status_json.restype = ctypes.c_uint32

            blender_path = _blender_binary_path()
            result = dll.ava_blender_native_install(
                str(blender_path) if blender_path is not None else "",
                str(blender_path.with_suffix(".pdb")) if blender_path is not None else "",
            )
            _DLL = dll
            _AVAILABLE = bool(result)
            _STATUS = status_data().get("message", "ready" if _AVAILABLE else "Native GPU hook unavailable.")
            return _AVAILABLE
        except Exception as exc:
            _DLL = None
            _AVAILABLE = False
            _STATUS = f"Native GPU hook load failed: {exc}"
            return False


def copy_iosurface_to_texture(surface_id: int, texture: Any, width: int, height: int, context=None) -> bool:
    global _STATUS
    if not ensure_loaded(context):
        return False
    try:
        return bool(
            _DLL.ava_blender_native_copy_iosurface_to_texture(
                ctypes.c_uint32(int(surface_id)),
                ctypes.c_uint64(id(texture)),
                ctypes.c_int(int(width)),
                ctypes.c_int(int(height)),
            )
        )
    except Exception as exc:
        _STATUS = f"Native IOSurface copy failed: {exc}"
        return False


def texture_info(texture: Any) -> dict[str, Any]:
    if not ensure_loaded():
        return {}
    width = ctypes.c_int(0)
    height = ctypes.c_int(0)
    texture_format = ctypes.create_string_buffer(64)
    ok = bool(
        _DLL.ava_blender_native_texture_info(
            ctypes.c_uint64(id(texture)),
            ctypes.byref(width),
            ctypes.byref(height),
            texture_format,
            ctypes.c_uint32(len(texture_format)),
        )
    )
    if not ok:
        return {}
    return {
        "width": width.value,
        "height": height.value,
        "format": texture_format.value.decode("utf-8", "replace"),
    }


def status() -> str:
    data = status_data()
    return str(data.get("message", _STATUS)) if data else _STATUS


def status_data() -> dict[str, Any]:
    if _DLL is None:
        return {"available": False, "message": _STATUS}
    size = 4096
    while size <= 65536:
        buffer = ctypes.create_string_buffer(size)
        required = int(_DLL.ava_blender_native_status_json(buffer, ctypes.c_uint32(size)))
        if required <= size:
            payload = buffer.value.decode("utf-8", "replace")
            try:
                parsed = json.loads(payload)
            except Exception:
                return {"available": False, "message": payload or _STATUS}
            return parsed if isinstance(parsed, dict) else {"available": False, "message": _STATUS}
        size = required
    return {"available": False, "message": _STATUS}


def _find_library(context=None) -> Path | None:
    candidates: list[Path] = []
    env_path = os.environ.get("AVALONIA_BRIDGE_NATIVE_PATH")
    if env_path:
        candidates.append(Path(env_path))

    preferences = _preferences(context)
    if preferences is not None and getattr(preferences, "native_library_path", ""):
        candidates.append(Path(preferences.native_library_path))

    name = _native_library_name()
    package_dir = Path(__file__).resolve().parents[1]
    repo_root = package_dir.parents[2]
    candidates.extend(
        (
            package_dir / "native" / name,
            repo_root / "src" / "blender_native" / "build" / name,
            repo_root / "src" / "blender_native" / "build" / f"lib{name}",
        )
    )

    for candidate in candidates:
        if candidate.exists():
            return candidate
    return None


def _native_library_name() -> str:
    if sys.platform == "darwin":
        return "avalonia_bridge_native.dylib"
    if sys.platform == "win32":
        return "avalonia_bridge_native.dll"
    return "avalonia_bridge_native.so"


def _blender_binary_path() -> Path | None:
    try:
        import bpy

        binary_path = getattr(getattr(bpy, "app", None), "binary_path", "")
    except Exception:
        binary_path = ""
    return Path(binary_path) if binary_path else None


def _preferences(context=None):
    try:
        from ..preferences import get_preferences
    except Exception:
        return None
    if context is None:
        try:
            import bpy

            context = getattr(bpy, "context", None)
        except Exception:
            context = None
    if context is None:
        return None
    try:
        return get_preferences(context)
    except Exception:
        return None
