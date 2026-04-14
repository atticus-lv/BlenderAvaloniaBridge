import json
import socket
import struct
import threading

MAX_HEADER_BYTES = 64 * 1024
MAX_CONTROL_PAYLOAD_BYTES = 1 * 1024 * 1024
MAX_FRAME_PAYLOAD_BYTES = 64 * 1024 * 1024
MAX_PAYLOAD_BYTES_HARD = MAX_FRAME_PAYLOAD_BYTES


class ProtocolViolationError(RuntimeError):
    pass


def _payload_limit_for_type(message_type):
    if message_type == "frame":
        return MAX_FRAME_PAYLOAD_BYTES
    if message_type == "frame_ready":
        return 0
    return MAX_CONTROL_PAYLOAD_BYTES


def _validate_prefix_lengths(header_length, payload_length):
    if header_length <= 0 or header_length > MAX_HEADER_BYTES:
        raise ProtocolViolationError(
            f"Header length {header_length} exceeds protocol limit ({MAX_HEADER_BYTES} bytes)."
        )
    if payload_length < 0 or payload_length > MAX_PAYLOAD_BYTES_HARD:
        raise ProtocolViolationError(
            f"Payload length {payload_length} exceeds protocol hard limit ({MAX_PAYLOAD_BYTES_HARD} bytes)."
        )


def _validate_payload_length(header, payload_length):
    message_type = str(header.get("type", "") or "").lower()
    payload_limit = _payload_limit_for_type(message_type)
    if payload_length > payload_limit:
        raise ProtocolViolationError(
            f"Payload length {payload_length} exceeds limit ({payload_limit} bytes) for message type '{message_type}'."
        )

    if message_type != "frame":
        return

    stride = header.get("stride")
    height = header.get("height")
    try:
        stride_value = int(stride)
        height_value = int(height)
    except (TypeError, ValueError):
        return

    if stride_value <= 0 or height_value <= 0:
        return

    expected = stride_value * height_value
    if expected <= 0 or expected > MAX_FRAME_PAYLOAD_BYTES:
        raise ProtocolViolationError(
            f"Frame metadata implies payload size {expected}, outside supported frame payload range."
        )
    if payload_length != expected:
        raise ProtocolViolationError(
            f"Frame payload length mismatch: got {payload_length}, expected {expected} from stride*height."
        )


def encode_packet(header, payload=b""):
    header_bytes = json.dumps(header, separators=(",", ":")).encode("utf-8")
    payload = payload or b""
    _validate_prefix_lengths(len(header_bytes), len(payload))
    _validate_payload_length(header, len(payload))
    prefix = struct.pack("<II", len(header_bytes), len(payload))
    return prefix + header_bytes + payload


def recv_exact(sock, size):
    chunks = []
    remaining = size
    while remaining > 0:
        chunk = sock.recv(remaining)
        if not chunk:
            return None
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


class BridgeServer:
    def __init__(self, host="127.0.0.1", port=0, on_packet=None, on_connect=None, on_disconnect=None, on_error=None):
        self.host = host
        self.port = port
        self.on_packet = on_packet
        self.on_connect = on_connect
        self.on_disconnect = on_disconnect
        self.on_error = on_error
        self._server_socket = None
        self._client_socket = None
        self._send_lock = threading.Lock()
        self._thread = None
        self._running = False

    def start(self):
        self._server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._server_socket.bind((self.host, self.port))
        self._server_socket.listen(1)
        self._server_socket.settimeout(0.2)
        self.port = self._server_socket.getsockname()[1]
        self._running = True
        self._thread = threading.Thread(target=self._run, name="AvaloniaBridgeServer", daemon=True)
        self._thread.start()

    def stop(self):
        self._running = False
        for sock in (self._client_socket, self._server_socket):
            if sock is None:
                continue
            try:
                sock.close()
            except OSError:
                pass
        self._client_socket = None
        self._server_socket = None

    def send(self, header, payload=b""):
        if self._client_socket is None:
            return False
        try:
            packet = encode_packet(header, payload)
            with self._send_lock:
                self._client_socket.sendall(packet)
            return True
        except OSError as exc:
            self._handle_error(exc)
            return False

    def _run(self):
        while self._running:
            try:
                client, _address = self._server_socket.accept()
            except socket.timeout:
                continue
            except OSError:
                return

            self._client_socket = client
            client.settimeout(0.2)
            if self.on_connect is not None:
                self.on_connect(self)
            try:
                self._read_loop(client)
            finally:
                try:
                    client.close()
                except OSError:
                    pass
                self._client_socket = None
                if self.on_disconnect is not None:
                    self.on_disconnect()

    def _read_loop(self, client):
        while self._running:
            try:
                prefix = recv_exact(client, 8)
                if prefix is None:
                    return
                header_length, payload_length = struct.unpack("<II", prefix)
                _validate_prefix_lengths(header_length, payload_length)
                header_bytes = recv_exact(client, header_length)
                if header_bytes is None:
                    return
                try:
                    header = json.loads(header_bytes.decode("utf-8"))
                except (UnicodeDecodeError, json.JSONDecodeError) as exc:
                    raise ProtocolViolationError(f"Invalid protocol header JSON: {exc}") from exc
                if not isinstance(header, dict):
                    raise ProtocolViolationError("Protocol header must be a JSON object.")

                _validate_payload_length(header, payload_length)

                payload = recv_exact(client, payload_length) if payload_length else b""
                if payload is None:
                    return
                if self.on_packet is not None:
                    self.on_packet(header, payload or b"")
            except ProtocolViolationError as exc:
                self._handle_error(exc)
                return
            except socket.timeout:
                continue
            except OSError as exc:
                self._handle_error(exc)
                return
            except Exception as exc:
                self._handle_error(exc)
                return

    def _handle_error(self, exc):
        if self.on_error is not None:
            self.on_error(exc)
