import unittest

from _test_support import import_module


class _RecordingBusinessEndpoint:
    def __init__(self):
        self.calls = []

    def invoke(self, request):
        self.calls.append(request)
        return request.__class__.response(
            reply_to=request.message_id,
            payload={"ok": True},
        )


class ControllerBusinessTests(unittest.TestCase):
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
                "business_version": 1,
                "message_id": 9,
                "name": "scene.objects.list",
                "payload": {},
            },
            b"",
        )

        self.assertEqual(1, len(endpoint.calls))
        self.assertEqual("scene.objects.list", endpoint.calls[0].name)
        self.assertEqual(1, len(sent))
        self.assertEqual("business_response", sent[0][0]["type"])
        self.assertEqual(9, sent[0][0]["reply_to"])

    def test_default_business_endpoint_returns_unsupported_request_error(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()

        response = endpoint.invoke(
            core.BusinessRequest(
                business_version=1,
                message_id=7,
                name="unknown.command",
                payload={},
            )
        )

        self.assertFalse(response.ok)
        self.assertEqual("unsupported_business_request", response.error.code)

    def test_default_business_endpoint_validates_payload(self):
        core = import_module("avalonia_bridge.core")
        endpoint = core.DefaultBusinessEndpoint()

        response = endpoint.invoke(
            core.BusinessRequest(
                business_version=1,
                message_id=3,
                name="object.property.get",
                payload={},
            )
        )

        self.assertFalse(response.ok)
        self.assertEqual("invalid_payload", response.error.code)


if __name__ == "__main__":
    unittest.main()
