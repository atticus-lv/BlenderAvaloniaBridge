import json
import socket
import struct
import threading


def encode_packet(header, payload=b""):
    header_bytes = json.dumps(header, separators=(",", ":")).encode("utf-8")
    payload = payload or b""
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
        self._thread = threading.Thread(target=self._run, name="RenderBuilderBridgeServer", daemon=True)
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
                header_bytes = recv_exact(client, header_length)
                if header_bytes is None:
                    return
                payload = recv_exact(client, payload_length) if payload_length else b""
                if payload is None:
                    return
                header = json.loads(header_bytes.decode("utf-8"))
                if self.on_packet is not None:
                    self.on_packet(header, payload or b"")
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
