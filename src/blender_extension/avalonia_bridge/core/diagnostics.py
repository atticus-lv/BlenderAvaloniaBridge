import time
from collections import deque
from typing import TYPE_CHECKING, Callable, TypedDict

from .state import BridgeDiagnosticsSnapshot

if TYPE_CHECKING:
    from .controller import PacketHeader
    from .image_bridge import ImageBridge
    from .overlay import OverlayDrawer


NowMs = Callable[[], float]


class DiagnosticsData(TypedDict):
    session_started_at_ms: float
    pointer_move_received: int
    pointer_move_sent: int
    pointer_move_coalesced: int
    events_sent: int
    event_send_failures: int
    frames_received: int
    frame_ready_received: int
    frames_displayed: int
    frame_intervals_ms: deque[float]
    last_frame_received_at_ms: float | None
    last_frame_presented_at_ms: float | None
    last_frame_seq: int
    last_input_sent_at_ms: float | None
    last_input_type: str
    awaiting_input_frame: bool
    last_input_to_frame_ms: float | None
    last_input_to_apply_ms: float | None
    last_frame_pipeline_ms: float | None
    last_frame_transport_ms: float | None
    capture_started_transport_ms: float | None
    capture_started_to_recv_ms: float | None
    shared_memory_read_ms: deque[float]
    frame_stage_ms: deque[float]
    ui_apply_ms: deque[float]
    capture_frame_ms: deque[float]
    copy_bgra_ms: deque[float]
    linear_convert_ms: deque[float]
    shared_write_ms: deque[float]
    frame_send_ms: deque[float]


class DiagnosticsRecorder:
    def __init__(self, now_ms: NowMs | None = None):
        self._now_ms: NowMs = now_ms or self._default_now_ms
        self._data: DiagnosticsData
        self.reset()

    def reset(self):
        self._data = {
            "session_started_at_ms": self._now_ms(),
            "pointer_move_received": 0,
            "pointer_move_sent": 0,
            "pointer_move_coalesced": 0,
            "events_sent": 0,
            "event_send_failures": 0,
            "frames_received": 0,
            "frame_ready_received": 0,
            "frames_displayed": 0,
            "frame_intervals_ms": deque(maxlen=60),
            "last_frame_received_at_ms": None,
            "last_frame_presented_at_ms": None,
            "last_frame_seq": 0,
            "last_input_sent_at_ms": None,
            "last_input_type": "",
            "awaiting_input_frame": False,
            "last_input_to_frame_ms": None,
            "last_input_to_apply_ms": None,
            "last_frame_pipeline_ms": None,
            "last_frame_transport_ms": None,
            "capture_started_transport_ms": None,
            "capture_started_to_recv_ms": None,
            "shared_memory_read_ms": deque(maxlen=60),
            "frame_stage_ms": deque(maxlen=60),
            "ui_apply_ms": deque(maxlen=60),
            "capture_frame_ms": deque(maxlen=60),
            "copy_bgra_ms": deque(maxlen=60),
            "linear_convert_ms": deque(maxlen=60),
            "shared_write_ms": deque(maxlen=60),
            "frame_send_ms": deque(maxlen=60),
        }

    def record_pointer_move_received(self, was_coalesced: bool):
        self._data["pointer_move_received"] += 1
        if was_coalesced:
            self._data["pointer_move_coalesced"] += 1

    def record_frame_presented(self):
        self._data["frames_displayed"] += 1
        self._data["last_frame_presented_at_ms"] = self._now_ms()

    def record_outgoing_message(self, header: "PacketHeader", sent: bool):
        message_type = header.get("type", "")
        input_types = {"pointer_move", "pointer_down", "pointer_up", "wheel", "key_down", "key_up", "text"}
        if message_type == "pointer_move" and sent:
            self._data["pointer_move_sent"] += 1
        if message_type in input_types:
            if sent:
                self._data["events_sent"] += 1
                self._data["last_input_sent_at_ms"] = self._now_ms()
                self._data["awaiting_input_frame"] = True
                self._data["last_input_type"] = message_type
            else:
                self._data["event_send_failures"] += 1

    def record_frame_packet(self, header: "PacketHeader", now_ms: float):
        packet_type = header.get("type", "")
        self._data["frames_received"] += 1
        if packet_type == "frame_ready":
            self._data["frame_ready_received"] += 1

        previous = self._data["last_frame_received_at_ms"]
        if previous is not None:
            self._data["frame_intervals_ms"].append(now_ms - previous)
        self._data["last_frame_received_at_ms"] = now_ms
        self._data["last_frame_seq"] = int(header.get("seq", 0))

        captured_at = header.get("captured_at_unix_ms")
        if captured_at is not None:
            self._data["last_frame_pipeline_ms"] = max(0.0, now_ms - float(captured_at))

        sent_at = header.get("sent_at_unix_ms")
        if sent_at is not None:
            self._data["last_frame_transport_ms"] = max(0.0, now_ms - float(sent_at))

        capture_started_at = header.get("capture_started_at_unix_ms")
        if capture_started_at is not None:
            self._data["capture_started_to_recv_ms"] = max(0.0, now_ms - float(capture_started_at))
            if sent_at is not None:
                self._data["capture_started_transport_ms"] = max(0.0, float(sent_at) - float(capture_started_at))

        input_applied_at = header.get("input_applied_at_unix_ms")
        if input_applied_at is not None and self._data["last_input_sent_at_ms"] is not None:
            self._data["last_input_to_apply_ms"] = max(0.0, float(input_applied_at) - self._data["last_input_sent_at_ms"])

        for key in ("ui_apply_ms", "capture_frame_ms", "copy_bgra_ms", "linear_convert_ms", "shared_write_ms", "frame_send_ms"):
            value = header.get(key)
            if value is not None and self._data.get(key) is not None:
                self._data[key].append(float(value))

        if self._data["awaiting_input_frame"] and self._data["last_input_sent_at_ms"] is not None:
            self._data["last_input_to_frame_ms"] = max(0.0, now_ms - self._data["last_input_sent_at_ms"])
            self._data["awaiting_input_frame"] = False

    def record_timing(self, key: str, value_ms: float):
        bucket = self._data.get(key)
        if bucket is not None:
            bucket.append(value_ms)

    def avg_timing(self, key: str) -> float | None:
        bucket = self._data.get(key)
        if not bucket:
            return None
        return sum(bucket) / len(bucket)

    def build_snapshot(
        self,
        image_bridge: "ImageBridge",
        drawer: "OverlayDrawer",
        pending_pointer_move: bool,
    ) -> BridgeDiagnosticsSnapshot:
        pointer_received = self._data["pointer_move_received"]
        pointer_coalesced = self._data["pointer_move_coalesced"]
        intervals = self._data["frame_intervals_ms"]
        frame_cadence_ms = sum(intervals) / len(intervals) if intervals else None
        fps = (1000.0 / frame_cadence_ms) if frame_cadence_ms and frame_cadence_ms > 0.0 else None
        uptime_s = max(0.0, (self._now_ms() - self._data["session_started_at_ms"]) / 1000.0)
        image_diag = image_bridge.diagnostics_snapshot()
        draw_diag = drawer.diagnostics_snapshot()
        convert_ms = self.avg_timing("linear_convert_ms")
        if convert_ms is None:
            convert_ms = self.avg_timing("copy_bgra_ms")
        return BridgeDiagnosticsSnapshot(
            mode=image_bridge.last_mode,
            uptime_s=uptime_s,
            fps=fps,
            frame_cadence_ms=frame_cadence_ms,
            last_frame_seq=self._data["last_frame_seq"],
            last_input_type=self._data["last_input_type"] or "-",
            input_to_next_frame_ms=self._data["last_input_to_frame_ms"],
            input_to_apply_ms=self._data["last_input_to_apply_ms"],
            capture_to_blender_recv_ms=self._data["last_frame_pipeline_ms"],
            capture_frame_ms=self.avg_timing("capture_frame_ms"),
            convert_ms=convert_ms,
            gpu_upload_ms=image_diag["texture_update_avg_ms"],
            overlay_draw_ms=draw_diag["draw_avg_ms"],
            pointer_move_drop_pct=(pointer_coalesced / pointer_received * 100.0) if pointer_received else 0.0,
            pending_pointer_move=pending_pointer_move,
        )

    @staticmethod
    def _default_now_ms():
        return time.time() * 1000.0
