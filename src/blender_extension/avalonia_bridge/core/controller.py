from __future__ import annotations

import subprocess
import threading
import time
from collections import deque
from dataclasses import replace
from typing import Callable, Mapping

from .business import (
    BusinessBridgeHandler,
    BusinessEndpoint,
    DefaultBusinessEndpoint,
    EndpointBusinessBridgeHandler,
)
from .config import BridgeConfig
from .diagnostics import DiagnosticsRecorder
from .frame_pipeline import FramePipeline
from .process import ProcessManager, validate_executable_path
from .state import BridgeDiagnosticsSnapshot, BridgeStateSnapshot
from .transport import BridgeServer
from .view3d_overlay_host import BridgePresentationHost


PacketHeader = dict[str, object]
StateCallback = Callable[[BridgeStateSnapshot], None]
ServerFactory = Callable[..., BridgeServer]


class BridgeController:
    def __init__(
        self,
        config: BridgeConfig | Mapping[str, object],
        business_endpoint: BusinessEndpoint | None = None,
        business_handler: BusinessBridgeHandler | None = None,
        state_callback: StateCallback | None = None,
        process_manager: ProcessManager | None = None,
        server_factory: ServerFactory = BridgeServer,
        host: BridgePresentationHost | None = None,
    ) -> None:
        self._config = config if isinstance(config, BridgeConfig) else BridgeConfig(**dict(config))
        self._state_callback: StateCallback | None = state_callback
        self.business_endpoint: BusinessEndpoint = business_endpoint or DefaultBusinessEndpoint()
        self.business_handler: BusinessBridgeHandler = business_handler or EndpointBusinessBridgeHandler(self.business_endpoint)
        self.process_manager: ProcessManager = process_manager or ProcessManager()
        self._server_factory: ServerFactory = server_factory
        self.server: BridgeServer | None = None
        self.frame_pipeline = FramePipeline()
        self.frame_store = self.frame_pipeline.frame_store
        self.image_bridge = self.frame_pipeline.image_bridge
        self.shared_memory_bridge = self.frame_pipeline.shared_memory_bridge
        self.host = host
        if self.host is not None:
            self.host.attach(self)
        self._pending_pointer_move: PacketHeader | None = None
        self._pending_business_packets: deque[tuple[PacketHeader, bytes]] = deque()
        self._business_lock = threading.Lock()
        self._diagnostics: DiagnosticsRecorder = DiagnosticsRecorder(now_ms=self._now_ms)
        self._remote_window_mode = "unknown"
        self._remote_supports_business = bool(getattr(self._config, "supports_business", True))
        self._remote_supports_frames = bool(getattr(self._config, "supports_frames", True))
        self._remote_supports_input = bool(getattr(self._config, "supports_input", True))
        self._render_scaling = self._sanitize_render_scaling(getattr(self._config, "render_scaling", 1.25))
        self._render_width = self._scale_dimension(self._config.width, self._render_scaling)
        self._render_height = self._scale_dimension(self._config.height, self._render_scaling)
        self._state = BridgeStateSnapshot(
            width=max(64, int(self._config.width)),
            height=max(64, int(self._config.height)),
            remote_window_mode=self._remote_window_mode,
            remote_supports_business=self._remote_supports_business,
            remote_supports_frames=self._remote_supports_frames,
            remote_supports_input=self._remote_supports_input,
            overlay_offset_x=int(self._config.overlay_offset_x),
            overlay_offset_y=int(self._config.overlay_offset_y),
        )
        self._publish_state()

    @property
    def show_overlay_debug(self):
        return bool(self._config.show_overlay_debug)

    @property
    def capture_input(self) -> bool:
        return self._state.capture_input

    @property
    def remote_supports_input(self) -> bool:
        return self._remote_supports_input

    @property
    def has_pending_pointer_move(self) -> bool:
        return self._pending_pointer_move is not None

    def set_host(self, host: BridgePresentationHost | None) -> None:
        if self.host is host:
            return
        if self.host is not None:
            try:
                self.host.stop()
            except Exception:
                pass
        self.host = host
        if self.host is not None:
            self.host.attach(self)
            if self._state.process_running and self._config.supports_frames:
                self.host.start()

    def set_config(self, config: BridgeConfig | Mapping[str, object]):
        self._config = config if isinstance(config, BridgeConfig) else BridgeConfig(**dict(config))
        self._render_scaling = self._sanitize_render_scaling(getattr(self._config, "render_scaling", 1.25))
        self._render_width = self._scale_dimension(self._config.width, self._render_scaling)
        self._render_height = self._scale_dimension(self._config.height, self._render_scaling)
        if not self._state.process_running:
            self._replace_state(
                width=max(64, int(self._config.width)),
                height=max(64, int(self._config.height)),
                overlay_offset_x=int(self._config.overlay_offset_x),
                overlay_offset_y=int(self._config.overlay_offset_y),
            )

    def start(self) -> subprocess.Popen[str]:
        self.stop()
        path = validate_executable_path(self._config.executable_path)
        self._diagnostics.reset()
        display_width = max(64, int(self._config.width))
        display_height = max(64, int(self._config.height))
        self._remote_window_mode = "unknown"
        self._remote_supports_business = bool(getattr(self._config, "supports_business", True))
        self._remote_supports_frames = bool(getattr(self._config, "supports_frames", True))
        self._remote_supports_input = bool(getattr(self._config, "supports_input", True))
        self._render_scaling = self._sanitize_render_scaling(getattr(self._config, "render_scaling", 1.25))
        self._render_width = self._scale_dimension(display_width, self._render_scaling)
        self._render_height = self._scale_dimension(display_height, self._render_scaling)
        if self._config.supports_frames:
            self.frame_pipeline.create_shared_memory(self._render_width, self._render_height)
        else:
            self.shared_memory_bridge.close()
        self.server = self._server_factory(
            host=self._config.host,
            on_packet=self._on_packet,
            on_connect=self._on_connect,
            on_disconnect=self._on_disconnect,
            on_error=self._on_error,
        )
        self.server.start()
        self._configure_business_event_sender(self.send_message)
        process = self.process_manager.start(
            str(path),
            self._config.host,
            self.server.port,
            display_width,
            display_height,
            self._render_scaling,
            self._config.window_mode,
            self._config.supports_business,
            self._config.supports_frames,
            self._config.supports_input,
        )
        self._pending_pointer_move = None
        with self._business_lock:
            self._pending_business_packets.clear()
        self._replace_state(
            process_running=True,
            connected=False,
            capture_input=False,
            process_id=getattr(process, "pid", 0) or 0,
            listen_port=int(self.server.port),
            last_error="",
            last_message="Process launched",
            width=display_width,
            height=display_height,
            remote_window_mode=self._remote_window_mode,
            remote_supports_business=self._remote_supports_business,
            remote_supports_frames=self._remote_supports_frames,
            remote_supports_input=self._remote_supports_input,
            overlay_offset_x=int(self._config.overlay_offset_x),
            overlay_offset_y=int(self._config.overlay_offset_y),
        )
        if self.host is not None and self._config.supports_frames:
            try:
                self.host.start()
            except Exception as exc:
                self._replace_state(last_error=str(exc))
        self._request_redraw()
        return process

    def stop(self):
        self.release_capture_input(last_message="")
        self._configure_business_event_sender(None)
        if self.server is not None:
            self.server.stop()
            self.server = None
        self.process_manager.stop()
        try:
            if self.host is not None:
                self.host.stop()
        except Exception:
            pass
        self.frame_pipeline.clear()
        self._pending_pointer_move = None
        with self._business_lock:
            self._pending_business_packets.clear()
        self._remote_window_mode = "unknown"
        self._remote_supports_business = bool(getattr(self._config, "supports_business", True))
        self._remote_supports_frames = bool(getattr(self._config, "supports_frames", True))
        self._remote_supports_input = bool(getattr(self._config, "supports_input", True))
        self._render_scaling = self._sanitize_render_scaling(getattr(self._config, "render_scaling", 1.25))
        self._render_width = self._scale_dimension(self._config.width, self._render_scaling)
        self._render_height = self._scale_dimension(self._config.height, self._render_scaling)
        self._replace_state(
            process_running=False,
            connected=False,
            capture_input=False,
            process_id=0,
            listen_port=0,
            remote_window_mode=self._remote_window_mode,
            remote_supports_business=self._remote_supports_business,
            remote_supports_frames=self._remote_supports_frames,
            remote_supports_input=self._remote_supports_input,
        )
        self._request_redraw()

    def tick_once(self):
        self._check_process_health()
        self.flush_pointer_move()
        frame = self.frame_pipeline.pop_latest()
        frame_updated = False
        business_updated = False
        if frame is not None:
            header, payload = frame
            self._render_width = int(header.get("width", self._render_width))
            self._render_height = int(header.get("height", self._render_height))
            pixel_format = header.get("pixel_format", "")
            update_started = time.perf_counter()
            if pixel_format == "rgba32f_linear":
                self.image_bridge.update_from_rgba32f_linear(payload, self._render_width, self._render_height)
            else:
                self.image_bridge.update_from_bgra(payload, self._render_width, self._render_height)
            self._diagnostics.record_timing("frame_stage_ms", (time.perf_counter() - update_started) * 1000.0)
            self._diagnostics.record_frame_presented()
            frame_updated = True
        business_updated = self._process_business_packets()
        if frame_updated or business_updated:
            self._request_redraw()

    def handle_event(self, context, event) -> bool:
        if self.host is None:
            return False
        return self.host.handle_event(context, event)

    def release_capture_input(self, last_message: str = "Pointer left overlay"):
        self._pending_pointer_move = None
        self._replace_state(capture_input=False, last_message=last_message)
        self._request_redraw()

    def send_message(self, header: Mapping[str, object], payload: bytes = b"") -> bool:
        if self.server is None:
            self._diagnostics.record_outgoing_message(header, False)
            return False
        message: PacketHeader = dict(header)
        message.setdefault("payload_length", len(payload or b""))
        sent = self.server.send(message, payload)
        self._diagnostics.record_outgoing_message(message, sent)
        return sent

    def state_snapshot(self) -> BridgeStateSnapshot:
        return replace(self._state)

    def diagnostics_snapshot(self) -> BridgeDiagnosticsSnapshot:
        return self._diagnostics.build_snapshot(
            image_bridge=self.image_bridge,
            presentation_host=self.host,
            pending_pointer_move=self._pending_pointer_move is not None,
        )

    def status_line(self) -> str:
        connection = "connected" if self._state.connected else "waiting"
        frame_status = "frame streaming" if self._remote_supports_frames else "business only"
        input_status = "input enabled" if self._remote_supports_input else "input disabled"
        path = self.image_bridge.last_mode if self._remote_supports_frames else "no-frame"
        suffix = f" | {self.image_bridge.last_error}" if self.image_bridge.last_error else ""
        return (
            f"Display {self._state.width}x{self._state.height}"
            f" <- Render {self._render_width}x{self._render_height}"
            f" | {connection} | {frame_status} | {input_status} | {path}{suffix}"
        )

    def _on_connect(self, _server):
        self._replace_state(connected=True, last_message="Avalonia connected")
        init_message = {
            "type": "init",
            "seq": 1,
            "width": self._render_width,
            "height": self._render_height,
            "window_mode": self._config.window_mode,
            "supports_business": bool(self._config.supports_business),
            "supports_frames": bool(self._config.supports_frames),
            "supports_input": bool(self._config.supports_input),
            "payload_length": 0,
            "message": "blender-ready",
        }
        if self._config.supports_frames:
            init_message.update(
                {
                    "pixel_format": "rgba32f_linear",
                    "stride": self._render_width * 16,
                    "shm_name": self.shared_memory_bridge.name,
                    "frame_size": self.shared_memory_bridge.frame_size,
                    "slot_count": self.shared_memory_bridge.slot_count,
                }
            )
        self.send_message(init_message)
        self._request_redraw()

    def _on_disconnect(self):
        self.release_capture_input(last_message="")
        self._remote_window_mode = "unknown"
        self._remote_supports_business = bool(getattr(self._config, "supports_business", True))
        self._remote_supports_frames = bool(getattr(self._config, "supports_frames", True))
        self._remote_supports_input = bool(getattr(self._config, "supports_input", True))
        self._replace_state(connected=False, capture_input=False, last_message="Avalonia disconnected")
        self._request_redraw()

    def _on_error(self, exc):
        message = str(exc)
        if exc.__class__.__name__ == "ProtocolViolationError":
            message = f"Protocol violation: {message}"
        self._replace_state(last_error=message)
        self._request_redraw()

    def _on_packet(self, header: Mapping[str, object], payload: bytes):
        if self._is_business_packet(header):
            with self._business_lock:
                self._pending_business_packets.append((dict(header), payload or b""))
            self._replace_state(last_message=f"Queued {header.get('type', '')}")
            self._request_redraw()
            return
        self._handle_packet(header, payload)

    def _handle_packet(self, header: Mapping[str, object], payload: bytes):
        packet_type = header.get("type", "")
        now_ms = self._now_ms()
        if self._is_business_packet(header):
            response = self.business_handler.handle_packet(dict(header), payload or b"")
            if response is not None:
                response_header, response_payload = response
                self.send_message(response_header, response_payload)
                response_type = response_header.get("type", "response")
                if response_header.get("ok", False):
                    self._replace_state(last_error="", last_message=f"{response_type}: ok")
                else:
                    self._replace_state(last_error=self._business_error_message(response_header, response_type))
            return response
        if packet_type == "frame":
            self._diagnostics.record_frame_packet(header, now_ms)
            self.frame_pipeline.ingest_frame(header, payload)
            self._replace_state(last_message="Frame received")
        elif packet_type == "frame_ready":
            try:
                self._diagnostics.record_frame_packet(header, now_ms)
                read_started = time.perf_counter()
                self.frame_pipeline.ingest_shared_frame(header)
                self._diagnostics.record_timing("shared_memory_read_ms", (time.perf_counter() - read_started) * 1000.0)
                self._replace_state(last_message=f"Frame slot {int(header.get('slot', 0))} received")
            except Exception as exc:
                self._replace_state(last_error=f"Shared memory read failed: {exc}")
        elif packet_type == "init":
            self._remote_window_mode = header.get("window_mode", "unknown")
            self._remote_supports_business = bool(header.get("supports_business", True))
            self._remote_supports_frames = bool(header.get("supports_frames", True))
            self._remote_supports_input = bool(header.get("supports_input", True))
            self._replace_state(
                last_message=header.get("message", "Init acknowledged"),
                remote_window_mode=self._remote_window_mode,
                remote_supports_business=self._remote_supports_business,
                remote_supports_frames=self._remote_supports_frames,
                remote_supports_input=self._remote_supports_input,
            )
        elif packet_type == "error":
            self._replace_state(last_error=header.get("message", "Avalonia reported an error"))
        if packet_type not in {"frame", "frame_ready"}:
            self._request_redraw()
        return None

    def _process_business_packets(self):
        processed = False
        while True:
            packet = self._pop_business_packet()
            if packet is None:
                return processed
            self._handle_packet(*packet)
            processed = True

    def _pop_business_packet(self) -> tuple[PacketHeader, bytes] | None:
        with self._business_lock:
            if not self._pending_business_packets:
                return None
            return self._pending_business_packets.popleft()

    def _replace_state(self, **changes):
        next_state = replace(self._state, **changes)
        if next_state == self._state:
            return
        self._state = next_state
        self._publish_state()

    def _publish_state(self):
        if self._state_callback is not None:
            self._state_callback(self.state_snapshot())

    def flush_pointer_move(self) -> bool:
        if not self._pending_pointer_move:
            return False
        message = self._pending_pointer_move
        self._pending_pointer_move = None
        return self.send_message(message)

    def queue_pointer_move(self, message: PacketHeader) -> None:
        self._pending_pointer_move = dict(message)

    def clear_pending_pointer_move(self) -> None:
        self._pending_pointer_move = None

    def record_pointer_move_received(self, was_coalesced: bool) -> None:
        self._diagnostics.record_pointer_move_received(was_coalesced)

    def update_state(self, **changes) -> None:
        self._replace_state(**changes)

    @staticmethod
    def _is_business_packet(header):
        return header.get("type") == "business_request"

    @staticmethod
    def _business_error_message(header: Mapping[str, object], response_type: str) -> str:
        error = header.get("error")
        if isinstance(error, dict):
            message = error.get("message")
            if isinstance(message, str) and message.strip():
                return message.strip()
            code = error.get("code")
            if isinstance(code, str) and code.strip():
                return code.strip()
        message = header.get("message")
        if isinstance(message, str) and message.strip():
            return message.strip()
        return f"{response_type} failed"

    def _configure_business_event_sender(self, sender):
        for candidate in (self.business_endpoint, getattr(self.business_handler, "endpoint", None)):
            setter = getattr(candidate, "set_event_sender", None)
            if callable(setter):
                setter(sender)

    @staticmethod
    def _now_ms():
        return time.time() * 1000.0

    def _check_process_health(self):
        poll_exit = getattr(self.process_manager, "poll_exit", None)
        if poll_exit is None:
            return
        report = poll_exit()
        if report is None:
            return

        return_code = report.get("returncode")
        stderr_text = (report.get("stderr") or "").strip()
        if stderr_text:
            last_line = stderr_text.splitlines()[-1].strip()
            message = f"Avalonia exited ({return_code}): {last_line[:300]}"
        else:
            message = f"Avalonia exited ({return_code})"
        self.release_capture_input(last_message="")
        self._replace_state(
            process_running=False,
            connected=False,
            capture_input=False,
            process_id=0,
            last_error=message,
            last_message="Process exited",
        )
        self._request_redraw()

    def _request_redraw(self) -> None:
        if self.host is not None:
            self.host.tag_redraw()

    @staticmethod
    def _sanitize_render_scaling(value):
        try:
            scaling = float(value)
        except (TypeError, ValueError):
            return 1.25
        return scaling if scaling > 0.0 else 1.25

    @staticmethod
    def _scale_dimension(value, render_scaling):
        return max(64, int(round(float(value) * float(render_scaling))))
