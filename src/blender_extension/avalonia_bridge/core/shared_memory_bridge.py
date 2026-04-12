import mmap
import os
import threading
import time


class SharedMemoryBridge:
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

    def create(self, frame_size, slot_count=2):
        self.close()
        mapping_name = f"RenderBuilderFrame_{os.getpid()}_{int(time.time() * 1000)}"
        mapping_size = frame_size * slot_count
        self._mapping = mmap.mmap(-1, mapping_size, tagname=mapping_name, access=mmap.ACCESS_WRITE)
        self._name = mapping_name
        self._frame_size = frame_size
        self._slot_count = slot_count
        return mapping_name

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
