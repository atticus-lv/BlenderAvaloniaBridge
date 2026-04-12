import threading


class FrameStore:
    def __init__(self):
        self._lock = threading.Lock()
        self._latest = None
        self._last_applied_seq = -1

    def update(self, header, payload):
        with self._lock:
            self._latest = (dict(header), bytes(payload))

    def pop_latest(self):
        with self._lock:
            if self._latest is None:
                return None
            header, payload = self._latest
            seq = int(header.get("seq", 0))
            if seq == self._last_applied_seq:
                return None
            self._last_applied_seq = seq
            return header, payload
