import unittest

from _test_support import import_module


class CoreApiTests(unittest.TestCase):
    def test_core_package_exports_stable_public_api(self):
        core = import_module("avalonia_bridge.core")

        self.assertTrue(hasattr(core, "BridgeConfig"))
        self.assertTrue(hasattr(core, "BridgeController"))
        self.assertTrue(hasattr(core, "BridgeStateSnapshot"))
        self.assertTrue(hasattr(core, "BridgeDiagnosticsSnapshot"))
        self.assertTrue(hasattr(core, "BusinessRequest"))
        self.assertTrue(hasattr(core, "BusinessResponse"))
        self.assertTrue(hasattr(core, "BusinessError"))
        self.assertTrue(hasattr(core, "BusinessEndpoint"))
        self.assertTrue(hasattr(core, "DefaultBusinessEndpoint"))
        self.assertTrue(hasattr(core, "BusinessBridgeHandler"))
        self.assertTrue(hasattr(core, "DefaultBusinessBridgeHandler"))


if __name__ == "__main__":
    unittest.main()
