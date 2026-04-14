import mmap
import os
import sys
import tempfile
import threading
import time


def _build_mapping_name(prefix):
    return f"{prefix}_{os.getpid()}_{int(time.time() * 1000) & 0xFFFFFFFF:x}"


class _SharedMemoryBackend:
    def __init__(self):
        self._mapping = None
        self._lock = threading.Lock()
        self._name = ""
        self._frame_size = 0
        self._slot_count = 0

    @property
    def name(self):
        return self._name

    @property
    def frame_size(self):
        return self._frame_size

    @property
    def slot_count(self):
        return self._slot_count

    def close(self):
        with self._lock:
            if self._mapping is not None:
                self._mapping.close()
        self._mapping = None
        self._name = ""
        self._frame_size = 0
        self._slot_count = 0

    def read_slot(self, slot):
        if self._mapping is None:
            raise RuntimeError("Shared memory mapping is not available.")
        if slot < 0 or slot >= self._slot_count:
            raise ValueError(f"Shared memory slot out of range: {slot}")
        offset = slot * self._frame_size
        with self._lock:
            self._mapping.seek(offset)
            return self._mapping.read(self._frame_size)


class _WindowsSharedMemoryBackend(_SharedMemoryBackend):
    def create(self, frame_size, slot_count=2):
        self.close()
        mapping_name = _build_mapping_name("AvaloniaBridgeFrame")
        mapping_size = frame_size * slot_count
        self._mapping = mmap.mmap(-1, mapping_size, tagname=mapping_name, access=mmap.ACCESS_WRITE)
        self._name = mapping_name
        self._frame_size = frame_size
        self._slot_count = slot_count
        return mapping_name


class _DarwinSharedMemoryBackend(_SharedMemoryBackend):
    def __init__(self):
        super().__init__()
        self._fd = None

    def create(self, frame_size, slot_count=2):
        self.close()
        mapping_size = frame_size * slot_count
        fd, mapping_path = tempfile.mkstemp(prefix="avb_", suffix=".bin")
        try:
            os.ftruncate(fd, mapping_size)
            self._mapping = mmap.mmap(
                fd,
                mapping_size,
                flags=mmap.MAP_SHARED,
                prot=mmap.PROT_READ | mmap.PROT_WRITE,
            )
        except Exception:
            os.close(fd)
            os.unlink(mapping_path)
            raise

        self._fd = fd
        self._name = mapping_path
        self._frame_size = frame_size
        self._slot_count = slot_count
        return mapping_path

    def close(self):
        path = self._name
        super().close()
        fd = self._fd
        self._fd = None
        if fd is not None:
            os.close(fd)
        if path:
            try:
                os.unlink(path)
            except FileNotFoundError:
                pass


def _create_backend():
    if sys.platform == "win32":
        return _WindowsSharedMemoryBackend()
    if sys.platform == "darwin":
        return _DarwinSharedMemoryBackend()
    raise RuntimeError(f"Shared memory mapping is not supported on platform '{sys.platform}'.")


class SharedMemoryBridge:
    def __init__(self):
        self._backend = _create_backend()

    @property
    def name(self):
        return self._backend.name

    @property
    def frame_size(self):
        return self._backend.frame_size

    @property
    def slot_count(self):
        return self._backend.slot_count

    def create(self, frame_size, slot_count=2):
        return self._backend.create(frame_size, slot_count=slot_count)

    def close(self):
        self._backend.close()

    def read_slot(self, slot):
        return self._backend.read_slot(slot)
