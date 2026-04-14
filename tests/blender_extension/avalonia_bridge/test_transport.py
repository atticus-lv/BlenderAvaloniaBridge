import struct
import unittest

from _test_support import import_module


class TransportTests(unittest.TestCase):
    def test_encode_packet_preserves_length_prefixed_wire_format(self):
        transport = import_module("avalonia_bridge.core.transport")

        packet = transport.encode_packet({"type": "frame", "seq": 3}, b"\x01\x02\x03")
        header_length, payload_length = struct.unpack("<II", packet[:8])

        self.assertEqual(3, payload_length)
        self.assertGreater(header_length, 0)
        self.assertEqual(len(packet), 8 + header_length + payload_length)

    def test_validate_prefix_lengths_rejects_oversized_header(self):
        transport = import_module("avalonia_bridge.core.transport")

        with self.assertRaises(transport.ProtocolViolationError):
            transport._validate_prefix_lengths(transport.MAX_HEADER_BYTES + 1, 0)

    def test_encode_packet_rejects_oversized_control_payload(self):
        transport = import_module("avalonia_bridge.core.transport")

        with self.assertRaises(transport.ProtocolViolationError):
            transport.encode_packet(
                {"type": "init", "seq": 1},
                b"\x00" * (transport.MAX_CONTROL_PAYLOAD_BYTES + 1),
            )

    def test_validate_payload_length_rejects_frame_ready_payload(self):
        transport = import_module("avalonia_bridge.core.transport")

        with self.assertRaises(transport.ProtocolViolationError):
            transport._validate_payload_length({"type": "frame_ready", "seq": 2}, 4)

    def test_validate_payload_length_rejects_frame_stride_mismatch(self):
        transport = import_module("avalonia_bridge.core.transport")

        with self.assertRaises(transport.ProtocolViolationError):
            transport._validate_payload_length(
                {"type": "frame", "seq": 9, "width": 2, "height": 1, "stride": 8},
                4,
            )


if __name__ == "__main__":
    unittest.main()
