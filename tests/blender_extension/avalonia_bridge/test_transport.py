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


if __name__ == "__main__":
    unittest.main()
