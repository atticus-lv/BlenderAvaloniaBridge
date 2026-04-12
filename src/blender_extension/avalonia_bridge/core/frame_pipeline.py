from .frame_store import FrameStore
from .image_bridge import ImageBridge
from .shared_memory_bridge import SharedMemoryBridge


class FramePipeline:
    def __init__(self):
        self.frame_store = FrameStore()
        self.image_bridge = ImageBridge()
        self.shared_memory_bridge = SharedMemoryBridge()

    def create_shared_memory(self, width, height, bytes_per_pixel=16, slot_count=2):
        frame_size = int(width) * int(height) * int(bytes_per_pixel)
        self.shared_memory_bridge.create(frame_size, slot_count=slot_count)
        return frame_size

    def clear(self):
        self.image_bridge.clear()
        self.shared_memory_bridge.close()

    def ingest_frame(self, header, payload):
        self.frame_store.update(header, payload)

    def ingest_shared_frame(self, header):
        slot = int(header.get("slot", 0))
        payload = self.shared_memory_bridge.read_slot(slot)
        self.frame_store.update(header, payload)
        return payload

    def pop_latest(self):
        return self.frame_store.pop_latest()
