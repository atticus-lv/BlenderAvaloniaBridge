import json
from dataclasses import asdict, dataclass


@dataclass(frozen=True)
class BridgeStateSnapshot:
    process_running: bool = False
    connected: bool = False
    capture_input: bool = False
    process_id: int = 0
    listen_port: int = 0
    last_error: str = ""
    last_message: str = ""
    width: int = 1100
    height: int = 760
    remote_window_mode: str = "unknown"
    remote_supports_business: bool = True
    remote_supports_frames: bool = True
    remote_supports_input: bool = True
    overlay_offset_x: int = 0
    overlay_offset_y: int = 0

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class BridgeDiagnosticsSnapshot:
    mode: str = "none"
    uptime_s: float = 0.0
    fps: float | None = None
    frame_cadence_ms: float | None = None
    last_frame_seq: int = 0
    last_input_type: str = "-"
    input_to_next_frame_ms: float | None = None
    input_to_apply_ms: float | None = None
    capture_to_blender_recv_ms: float | None = None
    capture_frame_ms: float | None = None
    convert_ms: float | None = None
    gpu_upload_ms: float | None = None
    overlay_draw_ms: float | None = None
    pointer_move_drop_pct: float = 0.0
    pending_pointer_move: bool = False

    def to_dict(self):
        payload = asdict(self)
        pretty_payload = dict(payload)
        pretty_payload.pop("pending_pointer_move", None)
        payload["json_pretty"] = json.dumps(pretty_payload, ensure_ascii=False, indent=2)
        return payload
