import types
import unittest
import sys
import struct

from _test_support import import_module


class _RecordingBusinessEndpoint:
    def __init__(self):
        self.calls = []

    def invoke(self, request):
        self.calls.append(request)
        return request.__class__.response(
            reply_to=request.message_id,
            payload={"ok": True},
            protocol_version=request.protocol_version,
            schema_version=request.schema_version,
        )


class _BinaryRecordingBusinessEndpoint(_RecordingBusinessEndpoint):
    def invoke(self, request):
        self.calls.append(request)
        return request.__class__.response(
            reply_to=request.message_id,
            payload={"valueType": "array_buffer", "elementType": "uint8", "count": 4, "shape": [1, 1, 4]},
            protocol_version=request.protocol_version,
            schema_version=request.schema_version,
            raw_payload=b"\x01\x02\x03\x04",
        )


class _FakeEnumItem:
    def __init__(self, identifier):
        self.identifier = identifier


class _FakeProperty:
    def __init__(
        self,
        property_type,
        *,
        subtype="NONE",
        is_array=False,
        array_length=0,
        is_enum_flag=False,
        is_readonly=False,
        is_animatable=False,
        fixed_type=None,
        enum_items=None,
    ):
        self.type = property_type
        self.subtype = subtype
        self.is_array = is_array
        self.array_length = array_length
        self.is_enum_flag = is_enum_flag
        self.is_readonly = is_readonly
        self.is_animatable = is_animatable
        self.fixed_type = types.SimpleNamespace(identifier=fixed_type) if fixed_type else None
        self.enum_items = [_FakeEnumItem(item) for item in (enum_items or [])]


class ControllerBusinessTests(unittest.TestCase):
    def test_default_business_endpoint_serializes_vector_like_property_values(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]

        class FakeVector:
            def __init__(self, *values):
                self._values = values

            def __len__(self):
                return len(self._values)

            def __getitem__(self, index):
                return self._values[index]

        cube = types.SimpleNamespace(
            name="Cube",
            session_uid=11,
            location=FakeVector(1.0, 2.0, 3.0),
        )
        bpy.data.objects["Cube"] = cube

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=12,
                name="rna.get",
                payload={"path": 'bpy.data.objects["Cube"].location'},
            )
        )

        self.assertTrue(response.ok)
        self.assertEqual([1.0, 2.0, 3.0], response.payload["value"])
        self.assertEqual("float_array", response.payload["valueType"])

    def test_default_business_endpoint_reads_vector_like_array_as_binary_payload(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]

        class FakeVector:
            def __init__(self, *values):
                self._values = values

            def __len__(self):
                return len(self._values)

            def __getitem__(self, index):
                return self._values[index]

        cube = types.SimpleNamespace(
            name="Cube",
            session_uid=11,
            location=FakeVector(1.0, 2.0, 3.0),
            bl_rna=types.SimpleNamespace(
                properties={
                    "location": _FakeProperty("FLOAT", is_array=True, array_length=3),
                }
            ),
        )
        bpy.data.objects["Cube"] = cube

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=13,
                name="rna.read_array",
                payload={"path": 'bpy.data.objects["Cube"].location'},
            )
        )

        self.assertTrue(response.ok)
        self.assertEqual("array_buffer", response.payload["valueType"])
        self.assertEqual("float32", response.payload["elementType"])
        self.assertEqual([3], response.payload["shape"])
        self.assertEqual(12, len(response.raw_payload))
        self.assertEqual((1.0, 2.0, 3.0), struct.unpack("<3f", response.raw_payload))

    def test_default_business_endpoint_reads_preview_float_pixels_with_image_shape(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]

        class FakeArray:
            def __init__(self, values):
                self._values = list(values)

            def __len__(self):
                return len(self._values)

            def __getitem__(self, index):
                return self._values[index]

        preview = types.SimpleNamespace(
            image_size=FakeArray([2, 1]),
            image_pixels_float=FakeArray([1.0, 0.5, 0.25, 1.0, 0.0, 0.0, 0.0, 0.0]),
            bl_rna=types.SimpleNamespace(
                properties={
                    "image_size": _FakeProperty("INT", is_array=True, array_length=2),
                    "image_pixels_float": _FakeProperty("FLOAT", is_array=True, array_length=8),
                }
            ),
        )
        material = types.SimpleNamespace(
            name="Mat",
            session_uid=12,
            preview=preview,
            bl_rna=types.SimpleNamespace(
                properties={
                    "preview": _FakeProperty("POINTER", fixed_type="ImagePreview"),
                }
            ),
        )
        bpy.data.materials["Mat"] = material

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=14,
                name="rna.read_array",
                payload={"path": 'bpy.data.materials["Mat"].preview.image_pixels_float'},
            )
        )

        self.assertTrue(response.ok)
        self.assertEqual("float32", response.payload["elementType"])
        self.assertEqual([1, 2, 4], response.payload["shape"])
        self.assertEqual(32, len(response.raw_payload))
        self.assertEqual(
            (1.0, 0.5, 0.25, 1.0, 0.0, 0.0, 0.0, 0.0),
            struct.unpack("<8f", response.raw_payload),
        )

    def test_default_business_endpoint_describes_rna_metadata(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]

        cube = types.SimpleNamespace(
            name="Cube",
            session_uid=11,
            rotation_mode="XYZ",
            parent=types.SimpleNamespace(name="Parent", session_uid=12),
            matrix_world=[
                [1.0, 0.0, 0.0, 0.0],
                [0.0, 1.0, 0.0, 0.0],
                [0.0, 0.0, 1.0, 0.0],
                [0.0, 0.0, 0.0, 1.0],
            ],
            bl_rna=types.SimpleNamespace(
                properties={
                    "rotation_mode": _FakeProperty(
                        "ENUM",
                        enum_items=["QUATERNION", "XYZ", "ZYX"],
                        is_animatable=True,
                    ),
                    "parent": _FakeProperty("POINTER", fixed_type="Object"),
                    "matrix_world": _FakeProperty(
                        "FLOAT",
                        subtype="MATRIX",
                        is_array=True,
                        array_length=16,
                    ),
                }
            ),
        )
        bpy.data.objects["Cube"] = cube

        enum_response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=21,
                name="rna.describe",
                payload={"path": 'bpy.data.objects["Cube"].rotation_mode'},
            )
        )
        pointer_response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=22,
                name="rna.get",
                payload={"path": 'bpy.data.objects["Cube"].parent'},
            )
        )
        matrix_response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=23,
                name="rna.get",
                payload={"path": 'bpy.data.objects["Cube"].matrix_world'},
            )
        )

        self.assertTrue(enum_response.ok)
        self.assertEqual("enum", enum_response.payload["valueType"])
        self.assertEqual(["QUATERNION", "XYZ", "ZYX"], enum_response.payload["enumItems"])
        self.assertFalse(enum_response.payload["isEnumFlag"])
        self.assertTrue(enum_response.payload["animatable"])

        self.assertTrue(pointer_response.ok)
        self.assertEqual("rna_ref", pointer_response.payload["valueType"])
        self.assertEqual("Parent", pointer_response.payload["value"]["name"])

        self.assertTrue(matrix_response.ok)
        self.assertEqual("float_array", matrix_response.payload["valueType"])
        self.assertEqual(16, matrix_response.payload["arrayLength"])
        self.assertEqual(16, len(matrix_response.payload["value"]))

    def test_default_business_endpoint_rejects_collection_replacement(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]

        bpy.context.scene.my_objects = [types.SimpleNamespace(name="Cube", session_uid=5)]
        bpy.context.scene.bl_rna = types.SimpleNamespace(
            properties={
                "my_objects": _FakeProperty(
                    "COLLECTION",
                    is_readonly=True,
                    fixed_type="Object",
                )
            }
        )

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=24,
                name="rna.set",
                payload={"path": "bpy.context.scene.my_objects", "value": []},
            )
        )

        self.assertFalse(response.ok)
        self.assertEqual("invalid_payload", response.error.code)

    def test_controller_routes_business_packets_to_endpoint(self):
        core = import_module("avalonia_bridge.core")
        endpoint = _RecordingBusinessEndpoint()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            business_endpoint=endpoint,
        )
        sent = []
        controller.send_message = lambda header, payload=b"": sent.append((dict(header), payload)) or True

        controller._handle_packet(
            {
                "type": "business_request",
                "protocolVersion": 1,
                "schemaVersion": 1,
                "message_id": 9,
                "name": "rna.list",
                "payload": {"path": "bpy.data.materials"},
            },
            b"",
        )

        self.assertEqual(1, len(endpoint.calls))
        self.assertEqual("rna.list", endpoint.calls[0].name)
        self.assertEqual(1, len(sent))
        self.assertEqual("business_response", sent[0][0]["type"])
        self.assertEqual(9, sent[0][0]["reply_to"])

    def test_controller_routes_business_payload_bytes_to_transport(self):
        core = import_module("avalonia_bridge.core")
        endpoint = _BinaryRecordingBusinessEndpoint()
        controller = core.BridgeController(
            core.BridgeConfig(executable_path="C:/bridge.exe"),
            business_endpoint=endpoint,
        )
        sent = []
        controller.send_message = lambda header, payload=b"": sent.append((dict(header), payload)) or True

        controller._handle_packet(
            {
                "type": "business_request",
                "protocolVersion": 1,
                "schemaVersion": 1,
                "message_id": 10,
                "name": "rna.read_array",
                "payload": {"path": 'bpy.data.materials["Mat"].preview.icon_pixels'},
            },
            b"",
        )

        self.assertEqual(1, len(sent))
        self.assertEqual("business_response", sent[0][0]["type"])
        self.assertEqual(b"\x01\x02\x03\x04", sent[0][1])

    def test_controller_uses_nested_business_error_message_for_state(self):
        core = import_module("avalonia_bridge.core")
        controller = core.BridgeController(core.BridgeConfig(executable_path="C:/bridge.exe"))

        controller._handle_packet(
            {
                "type": "business_request",
                "protocolVersion": 1,
                "schemaVersion": 1,
                "message_id": 11,
                "name": "rna.list",
                "payload": {"path": "bpy.data.materials"},
            },
            b"",
        )

        snapshot = controller.state_snapshot()

        self.assertEqual("business_response: ok", snapshot.last_message)
        self.assertEqual("", snapshot.last_error)

        controller.business_handler = types.SimpleNamespace(
            handle_packet=lambda header, payload: (
                {
                    "type": "business_response",
                    "reply_to": header["message_id"],
                    "ok": False,
                    "error": {
                        "code": "unsupported_business_request",
                        "message": "Not allowed in current context",
                    },
                },
                b"",
            )
        )

        controller._handle_packet(
            {
                "type": "business_request",
                "protocolVersion": 1,
                "schemaVersion": 1,
                "message_id": 12,
                "name": "rna.list",
                "payload": {"path": "bpy.data.materials"},
            },
            b"",
        )

        snapshot = controller.state_snapshot()

        self.assertEqual("Not allowed in current context", snapshot.last_error)

    def test_default_business_endpoint_rejects_unsupported_protocol_version(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=99,
                schema_version=1,
                message_id=7,
                name="rna.list",
                payload={"path": "bpy.data.materials"},
            )
        )

        self.assertFalse(response.ok)
        self.assertEqual("unsupported_protocol_version", response.error.code)

    def test_default_business_endpoint_lists_materials(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        bpy = sys.modules["bpy"]
        bpy.data.materials["Mat"] = types.SimpleNamespace(name="Mat", session_uid=3)

        response = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=3,
                name="rna.list",
                payload={"path": "bpy.data.materials"},
            )
        )

        self.assertTrue(response.ok)
        self.assertEqual("Mat", response.payload["items"][0]["name"])

    def test_watch_service_emits_dirty_event(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()
        emitted = []
        endpoint.set_event_sender(lambda header, payload=b"": emitted.append((header, payload)))

        subscribe = endpoint.invoke(
            core.BusinessRequest(
                protocol_version=1,
                schema_version=1,
                message_id=1,
                name="watch.subscribe",
                payload={"watchId": "materials", "source": "depsgraph", "path": "bpy.data.materials"},
            )
        )

        self.assertTrue(subscribe.ok)
        endpoint._watch.mark_dirty("depsgraph")

        self.assertEqual(1, len(emitted))
        self.assertEqual("business_event", emitted[0][0]["type"])
        self.assertEqual("watch.dirty", emitted[0][0]["name"])


if __name__ == "__main__":
    unittest.main()
